package com.fleetops.driver

import android.content.Context
import androidx.work.BackoffPolicy
import androidx.work.Constraints
import androidx.work.CoroutineWorker
import androidx.work.ExistingPeriodicWorkPolicy
import androidx.work.NetworkType
import androidx.work.PeriodicWorkRequestBuilder
import androidx.work.WorkManager
import androidx.work.WorkerParameters
import kotlinx.coroutines.flow.Flow
import retrofit2.HttpException
import java.io.IOException
import java.time.Duration
import java.time.Instant
import java.util.Base64
import java.util.UUID

interface DriverRepository {
    fun observeSession(): Flow<DriverSession?>
    fun observeMissions(): Flow<List<DriverMission>>
    fun observeMission(missionId: String): Flow<DriverMission?>
    fun observeComplianceCampaignTasks(): Flow<List<DriverComplianceCampaignTask>>
    suspend fun login(email: String, password: String)
    suspend fun pair(code: String)
    suspend fun refresh()
    suspend fun queueMissionAction(missionId: String, action: DriverMissionAction)
    suspend fun queueInspection(missionId: String, hasCriticalDefect: Boolean, evidence: List<CapturedEvidence> = emptyList())
    suspend fun queueDeliveryProof(missionId: String, stopId: String, recipientName: String, signatureName: String, evidence: List<CapturedEvidence>)
    suspend fun queueComplianceCampaignTask(taskId: String, notes: String?)
    suspend fun flushPendingCommands()
    suspend fun signOut()
}

interface DriverSyncScheduler {
    fun ensureScheduled()
    fun cancel()
}

class WorkManagerDriverSyncScheduler(
    private val workManager: WorkManager,
) : DriverSyncScheduler {
    override fun ensureScheduled() {
        val request = PeriodicWorkRequestBuilder<DriverSyncWorker>(Duration.ofMinutes(15))
            .setConstraints(
                Constraints.Builder()
                    .setRequiredNetworkType(NetworkType.CONNECTED)
                    .build(),
            )
            .setBackoffCriteria(BackoffPolicy.EXPONENTIAL, Duration.ofMinutes(1))
            .build()

        workManager.enqueueUniquePeriodicWork(
            DRIVER_SYNC_WORK_NAME,
            ExistingPeriodicWorkPolicy.UPDATE,
            request,
        )
    }

    override fun cancel() {
        workManager.cancelUniqueWork(DRIVER_SYNC_WORK_NAME)
    }
}

class OfflineFirstDriverRepository(
    private val localStore: DriverLocalStore,
    private val remoteDataSource: DriverRemoteDataSource,
    private val syncScheduler: DriverSyncScheduler,
) : DriverRepository {
    override fun observeSession(): Flow<DriverSession?> = localStore.observeSession()

    override fun observeMissions(): Flow<List<DriverMission>> = localStore.observeMissions()

    override fun observeMission(missionId: String): Flow<DriverMission?> = localStore.observeMission(missionId)

    override fun observeComplianceCampaignTasks(): Flow<List<DriverComplianceCampaignTask>> =
        localStore.observeComplianceCampaignTasks()

    override suspend fun login(email: String, password: String) {
        val session = remoteDataSource.login(email, password)
        startSession(session)
    }

    override suspend fun pair(code: String) {
        val session = remoteDataSource.pair(code)
        startSession(session)
    }

    private suspend fun startSession(session: DriverSession) {
        localStore.saveSession(session)
        syncScheduler.ensureScheduled()
        refresh()
    }

    override suspend fun refresh() {
        val session = requireSession()
        try {
            localStore.replaceMissions(remoteDataSource.listMissionDetails(session))
            localStore.replaceComplianceCampaignTasks(remoteDataSource.listComplianceCampaignTasks(session))
        } catch (_: IOException) {
            localStore.listMissions().forEach { localStore.updateMissionSyncState(it.id, MissionSyncState.Offline) }
            return
        }

        try {
            flushPendingCommands()
        } catch (_: IOException) {
            // Keep cached state available while the worker retries later.
        }
    }

    override suspend fun queueMissionAction(missionId: String, action: DriverMissionAction) {
        val mission = localStore.getMission(missionId)
            ?: throw IllegalArgumentException("Mission $missionId is not available offline.")

        localStore.saveMission(mission.applyLocalAction(action))
        localStore.enqueue(
            PendingMissionCommand(
                commandId = UUID.randomUUID().toString(),
                missionId = missionId,
                action = action,
                rowVersion = mission.rowVersion,
                occurredAtUtc = Instant.now().toString(),
            ),
        )

        try {
            flushPendingCommands()
        } catch (_: IOException) {
            localStore.updateMissionSyncState(missionId, MissionSyncState.Offline)
        }
    }

    override suspend fun queueInspection(missionId: String, hasCriticalDefect: Boolean, evidence: List<CapturedEvidence>) {
        val photos = evidence.mapIndexed { index, capture -> capture.toPendingPhoto("inspection-$index") }
        val operation = PendingWorkflowOperation(
            commandId = UUID.randomUUID().toString(),
            missionId = missionId,
            operationType = DriverWorkflowOperationType.Inspection,
            payloadJson = driverJson.encodeInspectionOperation(
                PendingInspectionOperation(
                    notes = if (hasCriticalDefect) "Critical brake defect detected." else "Vehicle ready for departure.",
                    completedAtUtc = Instant.now().toString(),
                    items = listOf(
                        PendingInspectionItem(1, "brakes", "Brakes and steering", !hasCriticalDefect, if (hasCriticalDefect) DriverDefectSeverity.Critical else DriverDefectSeverity.None, if (hasCriticalDefect) "Brake pedal unstable." else null, photos.firstOrNull()?.localId),
                        PendingInspectionItem(2, "lights", "Lights and signals", true, DriverDefectSeverity.None, null, null),
                        PendingInspectionItem(3, "cargo-secured", "Cargo and doors secured", true, DriverDefectSeverity.None, null, null),
                    ),
                ),
            ),
            photos = photos,
            createdAtUtc = Instant.now().toString(),
        )
        localStore.enqueueWorkflowOperation(operation)
        localStore.updateMissionSyncState(missionId, MissionSyncState.Pending)
        try {
            flushPendingCommands()
        } catch (_: IOException) {
            localStore.updateMissionSyncState(missionId, MissionSyncState.Offline)
        }
    }

    override suspend fun queueDeliveryProof(missionId: String, stopId: String, recipientName: String, signatureName: String, evidence: List<CapturedEvidence>) {
        require(evidence.any { it.caption == SIGNATURE_CAPTION }) { "A handwritten signature is required." }
        require(evidence.any { it.caption == DELIVERY_PHOTO_CAPTION }) { "A delivery photo is required." }
        val photos = evidence.mapIndexed { index, capture -> capture.toPendingPhoto("proof-$index") }
        val operation = PendingWorkflowOperation(
            commandId = UUID.randomUUID().toString(),
            missionId = missionId,
            operationType = DriverWorkflowOperationType.DeliveryProof,
            payloadJson = driverJson.encodeDeliveryProofOperation(
                PendingDeliveryProofOperation(
                    recipientName = recipientName.trim(),
                    signatureName = signatureName.trim(),
                    deliveredAtUtc = Instant.now().toString(),
                    notes = "Delivered on site.",
                    stopId = stopId,
                    photoLocalIds = photos.map { it.localId },
                ),
            ),
            photos = photos,
            createdAtUtc = Instant.now().toString(),
        )
        localStore.enqueueWorkflowOperation(operation)
        localStore.updateMissionSyncState(missionId, MissionSyncState.Pending)
        try {
            flushPendingCommands()
        } catch (_: IOException) {
            localStore.updateMissionSyncState(missionId, MissionSyncState.Offline)
        }
    }

    override suspend fun queueComplianceCampaignTask(taskId: String, notes: String?) {
        require(notes.isNullOrBlank() || notes.trim().length <= 500) { "Campaign notes must not exceed 500 characters." }
        localStore.queueComplianceCampaignTask(taskId, UUID.randomUUID().toString(), Instant.now().toString(), notes?.trim()?.takeIf { it.isNotEmpty() })
        try {
            flushPendingCommands()
        } catch (_: IOException) {
            // The task and idempotency key are durable in Room; WorkManager will retry it.
        }
    }

    override suspend fun flushPendingCommands() {
        val session = requireSession()
        for (task in localStore.pendingComplianceCampaignTasks()) {
            try {
                remoteDataSource.submitComplianceCampaignTask(session, task)
                localStore.markComplianceCampaignTaskSubmitted(task.id)
            } catch (exception: HttpException) {
                if (exception.code() == 401) localStore.clearSession()
                throw exception
            } catch (exception: IOException) {
                throw exception
            }
        }
        val workflowOperations = localStore.pendingWorkflowOperations()
        for (operation in workflowOperations) {
            try {
                val processed = flushWorkflowOperation(session, operation)
                localStore.saveWorkflowOperation(processed)
                if (processed.photos.all { it.remoteAssetId != null }) {
                    when (processed.operationType) {
                        DriverWorkflowOperationType.Inspection -> {
                            val payload = driverJson.decodeInspectionOperation(processed.payloadJson)
                            remoteDataSource.submitInspection(
                                session,
                                processed.missionId,
                                SubmitPreDepartureInspectionRequestDto(
                                    commandId = processed.commandId,
                                    completedAtUtc = payload.completedAtUtc,
                                    notes = payload.notes,
                                    items = payload.items.map { item ->
                                        InspectionItemResultRequestDto(
                                            sequence = item.sequence,
                                            code = item.code,
                                            label = item.label,
                                            isPass = item.isPass,
                                            defectSeverity = item.defectSeverity.name,
                                            notes = item.notes,
                                            photoAssetId = item.photoLocalId?.let { localId ->
                                                processed.photos.firstOrNull { it.localId == localId }?.remoteAssetId
                                            },
                                        )
                                    },
                                ),
                            )
                        }
                        DriverWorkflowOperationType.DeliveryProof -> {
                            val payload = driverJson.decodeDeliveryProofOperation(processed.payloadJson)
                            remoteDataSource.submitDeliveryProof(
                                session,
                                processed.missionId,
                                payload.stopId,
                                SubmitDeliveryProofRequestDto(
                                    commandId = processed.commandId,
                                    recipientName = payload.recipientName,
                                    signatureName = payload.signatureName,
                                    deliveredAtUtc = payload.deliveredAtUtc,
                                    notes = payload.notes,
                                    photos = payload.photoLocalIds.mapNotNull { localId ->
                                        processed.photos.firstOrNull { it.localId == localId }?.remoteAssetId?.let { remoteAssetId ->
                                            DeliveryProofPhotoRequestDto(remoteAssetId, processed.photos.firstOrNull { it.localId == localId }?.caption)
                                        }
                                    },
                                ),
                            )
                        }
                    }

                    localStore.removeWorkflowOperation(processed.commandId)
                    refresh()
                }
            } catch (exception: HttpException) {
                if (exception.code() == 401) {
                    localStore.clearSession()
                }
                throw exception
            } catch (exception: IOException) {
                localStore.updateMissionSyncState(operation.missionId, MissionSyncState.Offline)
                throw exception
            }
        }

        val pending = localStore.pendingCommands()
        for (command in pending) {
            try {
                val updatedMission = remoteDataSource.syncMissionCommand(session, command)
                localStore.saveMission(updatedMission.copy(syncState = MissionSyncState.Synced))
                localStore.removePendingCommand(command.commandId)
            } catch (exception: HttpException) {
                when (exception.code()) {
                    401 -> {
                        localStore.clearSession()
                        throw exception
                    }
                    409 -> {
                        localStore.updateMissionSyncState(command.missionId, MissionSyncState.Conflict)
                        throw exception
                    }
                    else -> throw exception
                }
            } catch (exception: IOException) {
                localStore.updateMissionSyncState(command.missionId, MissionSyncState.Offline)
                throw exception
            }
        }
    }

    override suspend fun signOut() {
        localStore.clearAll()
        syncScheduler.cancel()
    }

    private suspend fun requireSession(): DriverSession =
        localStore.getSession() ?: throw IllegalStateException("No driver session available.")

    private suspend fun flushWorkflowOperation(
        session: DriverSession,
        operation: PendingWorkflowOperation,
    ): PendingWorkflowOperation {
        val updatedPhotos = operation.photos.map { photo ->
            if (photo.remoteAssetId != null) {
                photo
            } else {
                uploadPhoto(session, photo, operation.operationType)
            }
        }

        return operation.copy(photos = updatedPhotos)
    }

    private suspend fun uploadPhoto(
        session: DriverSession,
        photo: PendingPhotoUpload,
        operationType: DriverWorkflowOperationType,
    ): PendingPhotoUpload {
        val bytes = Base64.getDecoder().decode(photo.base64Content)
        val uploadSessionId = photo.uploadSessionId
            ?: remoteDataSource.createUploadSession(
                session,
                photo.fileName,
                photo.contentType,
                bytes.size.toLong(),
                when (operationType) {
                    DriverWorkflowOperationType.Inspection -> "InspectionPhoto"
                    DriverWorkflowOperationType.DeliveryProof -> "DeliveryProofPhoto"
                },
            ).uploadSessionId

        val chunkSize = maxOf(1, bytes.size / 2)
        val remaining = bytes.size.toLong() - photo.uploadedBytes
        if (remaining > 0) {
            val start = photo.uploadedBytes.toInt()
            val endExclusive = minOf(bytes.size, start + chunkSize)
            val chunk = bytes.copyOfRange(start, endExclusive)
            val response = remoteDataSource.appendUploadChunk(
                session,
                uploadSessionId,
                photo.uploadedBytes,
                Base64.getEncoder().encodeToString(chunk),
            )

            val progressed = photo.copy(
                uploadSessionId = uploadSessionId,
                uploadedBytes = response.uploadedBytes,
            )

            if (response.uploadedBytes < response.totalBytes) {
                return progressed
            }
        }

        val asset = remoteDataSource.completeUploadSession(session, uploadSessionId)
        return photo.copy(
            uploadSessionId = uploadSessionId,
            uploadedBytes = bytes.size.toLong(),
            remoteAssetId = asset.assetId,
        )
    }
}

private fun CapturedEvidence.toPendingPhoto(localId: String): PendingPhotoUpload =
    PendingPhotoUpload(localId, fileName, contentType, base64Content, caption)

const val DELIVERY_PHOTO_CAPTION = "Delivery photo"
const val SIGNATURE_CAPTION = "Recipient signature"

class DriverSyncWorker(
    appContext: Context,
    workerParameters: WorkerParameters,
) : CoroutineWorker(appContext, workerParameters) {
    override suspend fun doWork(): Result {
        val repository = (applicationContext as DriverApplication).container.repository
        return try {
            repository.refresh()
            repository.flushPendingCommands()
            Result.success()
        } catch (_: IOException) {
            Result.retry()
        } catch (_: HttpException) {
            Result.retry()
        } catch (_: IllegalStateException) {
            Result.success()
        }
    }
}

const val DRIVER_SYNC_WORK_NAME = "fleetops-driver-sync"
