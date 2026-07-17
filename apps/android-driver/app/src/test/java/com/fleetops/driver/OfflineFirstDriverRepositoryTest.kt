package com.fleetops.driver

import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Assert.assertThrows
import org.junit.Test
import java.io.IOException
import java.util.Base64

class OfflineFirstDriverRepositoryTest {
    @Test
    fun loginStoresSessionAndDownloadsMissions() = runTest {
        val localStore = FakeDriverLocalStore()
        val repository = OfflineFirstDriverRepository(
            localStore = localStore,
            remoteDataSource = FakeDriverRemoteDataSource(),
            syncScheduler = FakeDriverSyncScheduler(),
        )

        repository.login("driver@northwind.local", "Driver123!")

        assertEquals("driver@northwind.local", localStore.session.value?.email)
        assertEquals(1, localStore.missions.value.size)
    }

    @Test
    fun offlineActionRemainsQueuedAndMarksMissionOffline() = runTest {
        val localStore = FakeDriverLocalStore().apply {
            session.value = fakeSession()
            missions.value = listOf(fakeMission())
        }
        val repository = OfflineFirstDriverRepository(
            localStore = localStore,
            remoteDataSource = FakeDriverRemoteDataSource(throwOfflineOnSync = true),
            syncScheduler = FakeDriverSyncScheduler(),
        )

        repository.queueMissionAction("mission-1", DriverMissionAction.Start)

        assertEquals(1, localStore.pending.size)
        assertEquals(MissionSyncState.Offline, localStore.missions.value.single().syncState)
    }

    @Test
    fun inspectionSyncsBeforeMissionStart() = runTest {
        val localStore = FakeDriverLocalStore().apply {
            session.value = fakeSession()
            missions.value = listOf(fakeMission())
        }
        val remote = FakeDriverRemoteDataSource()
        val repository = OfflineFirstDriverRepository(
            localStore = localStore,
            remoteDataSource = remote,
            syncScheduler = FakeDriverSyncScheduler(),
        )

        repository.queueInspection("mission-1", hasCriticalDefect = false)
        repository.queueMissionAction("mission-1", DriverMissionAction.Start)

        assertTrue(remote.events.indexOf("inspection:mission-1") < remote.events.indexOf("command:Start"))
        assertTrue(localStore.workflowOperations.isEmpty())
        assertTrue(localStore.pending.isEmpty())
    }

    @Test
    fun deliveryProofUploadResumesAcrossFlushes() = runTest {
        val localStore = FakeDriverLocalStore().apply {
            session.value = fakeSession()
            missions.value = listOf(fakeMission(status = DriverMissionStatus.Arrived))
        }
        val remote = FakeDriverRemoteDataSource()
        val repository = OfflineFirstDriverRepository(
            localStore = localStore,
            remoteDataSource = remote,
            syncScheduler = FakeDriverSyncScheduler(),
        )

        repository.queueDeliveryProof("mission-1", "stop-2", "Taylor Receiver", "Taylor Receiver", proofEvidence())

        assertEquals(1, localStore.workflowOperations.size)
        assertEquals(0, remote.completedUploadCount)

        repository.flushPendingCommands()

        assertTrue(localStore.workflowOperations.isEmpty())
        assertEquals(2, remote.completedUploadCount)
        assertTrue(remote.events.contains("proof:mission-1:stop-2"))
    }

    @Test
    fun deliveryProofRejectsMissingSignatureBeforeItEntersTheOfflineQueue() = runTest {
        val localStore = FakeDriverLocalStore().apply {
            session.value = fakeSession()
            missions.value = listOf(fakeMission(status = DriverMissionStatus.Arrived))
        }
        val repository = OfflineFirstDriverRepository(localStore, FakeDriverRemoteDataSource(), FakeDriverSyncScheduler())

        assertThrows(IllegalArgumentException::class.java) {
            runBlocking { repository.queueDeliveryProof("mission-1", "stop-2", "Taylor", "Taylor", listOf(proofEvidence().first())) }
        }
        assertTrue(localStore.workflowOperations.isEmpty())
    }
}

private fun proofEvidence(): List<CapturedEvidence> = listOf(
    CapturedEvidence("proof.jpg", "image/jpeg", SAMPLE_PNG_BASE64, DELIVERY_PHOTO_CAPTION),
    CapturedEvidence("signature.png", "image/png", SAMPLE_PNG_BASE64, SIGNATURE_CAPTION),
)

private class FakeDriverSyncScheduler : DriverSyncScheduler {
    override fun ensureScheduled() {}
    override fun cancel() {}
}

private class FakeDriverLocalStore : DriverLocalStore {
    val session = MutableStateFlow<DriverSession?>(null)
    val missions = MutableStateFlow<List<DriverMission>>(emptyList())
    val pending = mutableListOf<PendingMissionCommand>()
    val workflowOperations = mutableListOf<PendingWorkflowOperation>()

    override fun observeSession(): Flow<DriverSession?> = session

    override suspend fun getSession(): DriverSession? = session.value

    override suspend fun saveSession(session: DriverSession) {
        this.session.value = session
    }

    override suspend fun clearSession() {
        session.value = null
    }

    override fun observeMissions(): Flow<List<DriverMission>> = missions

    override suspend fun listMissions(): List<DriverMission> = missions.value

    override fun observeMission(missionId: String): Flow<DriverMission?> =
        missions.map { items -> items.firstOrNull { it.id == missionId } }

    override suspend fun getMission(missionId: String): DriverMission? =
        missions.value.firstOrNull { it.id == missionId }

    override suspend fun saveMission(mission: DriverMission) {
        missions.value = missions.value.filterNot { it.id == mission.id } + mission
    }

    override suspend fun replaceMissions(missions: List<DriverMission>) {
        this.missions.value = missions
    }

    override suspend fun updateMissionSyncState(missionId: String, syncState: MissionSyncState) {
        missions.value = missions.value.map { mission ->
            if (mission.id == missionId) {
                mission.copy(syncState = syncState)
            } else {
                mission
            }
        }
    }

    override suspend fun enqueue(command: PendingMissionCommand) {
        pending += command
    }

    override suspend fun pendingCommands(): List<PendingMissionCommand> = pending.toList()

    override suspend fun removePendingCommand(commandId: String) {
        pending.removeAll { it.commandId == commandId }
    }

    override suspend fun enqueueWorkflowOperation(operation: PendingWorkflowOperation) {
        workflowOperations.removeAll { it.commandId == operation.commandId }
        workflowOperations += operation
    }

    override suspend fun pendingWorkflowOperations(): List<PendingWorkflowOperation> = workflowOperations.toList()

    override suspend fun saveWorkflowOperation(operation: PendingWorkflowOperation) {
        workflowOperations.removeAll { it.commandId == operation.commandId }
        workflowOperations += operation
    }

    override suspend fun removeWorkflowOperation(commandId: String) {
        workflowOperations.removeAll { it.commandId == commandId }
    }

    override suspend fun clearAll() {
        session.value = null
        missions.value = emptyList()
        pending.clear()
        workflowOperations.clear()
    }
}

private class FakeDriverRemoteDataSource(
    private val throwOfflineOnSync: Boolean = false,
) : DriverRemoteDataSource {
    val events = mutableListOf<String>()
    var completedUploadCount = 0
    private val uploadProgress = mutableMapOf<String, Long>()
    private val totalDemoBytes = Base64.getDecoder().decode(SAMPLE_PNG_BASE64).size.toLong()

    override suspend fun login(email: String, password: String): DriverSession = fakeSession()

    override suspend fun pair(code: String): DriverSession = fakeSession()

    override suspend fun listMissionDetails(session: DriverSession): List<DriverMission> = listOf(fakeMission())

    override suspend fun syncMissionCommand(session: DriverSession, command: PendingMissionCommand): DriverMission {
        if (throwOfflineOnSync) {
            throw IOException("offline")
        }

        events += "command:${command.action.name}"
        return fakeMission().copy(
            status = when (command.action) {
                DriverMissionAction.Start -> DriverMissionStatus.EnRoute
                DriverMissionAction.Arrive -> DriverMissionStatus.Arrived
                DriverMissionAction.Complete -> DriverMissionStatus.Completed
            },
            syncState = MissionSyncState.Synced,
            rowVersion = fakeMission().rowVersion + 1,
        )
    }

    override suspend fun createUploadSession(
        session: DriverSession,
        fileName: String,
        contentType: String,
        totalBytes: Long,
        purpose: String,
    ): UploadSessionResponseDto =
        UploadSessionResponseDto(
            uploadSessionId = "upload-1",
            uploadedBytes = uploadProgress["upload-1"] ?: 0,
            totalBytes = totalBytes,
            expiresAtUtc = "2026-07-16T12:30:00Z",
            isCompleted = false,
            mediaAssetId = null,
        )

    override suspend fun appendUploadChunk(
        session: DriverSession,
        sessionId: String,
        offset: Long,
        base64Content: String,
    ): UploadSessionResponseDto {
        val bytes = Base64.getDecoder().decode(base64Content)
        val uploaded = offset + bytes.size
        uploadProgress[sessionId] = uploaded
        return UploadSessionResponseDto(
            uploadSessionId = sessionId,
            uploadedBytes = uploaded,
            totalBytes = totalDemoBytes,
            expiresAtUtc = "2026-07-16T12:30:00Z",
            isCompleted = uploaded >= totalDemoBytes,
            mediaAssetId = null,
        )
    }

    override suspend fun completeUploadSession(session: DriverSession, sessionId: String): MediaAssetResponseDto {
        completedUploadCount += 1
        return MediaAssetResponseDto(
            assetId = "asset-1",
            fileName = "demo.png",
            contentType = "image/png",
            sizeBytes = totalDemoBytes,
            readUrl = "https://example.test/media/asset-1",
        )
    }

    override suspend fun submitInspection(
        session: DriverSession,
        missionId: String,
        request: SubmitPreDepartureInspectionRequestDto,
    ) {
        events += "inspection:$missionId"
    }

    override suspend fun submitDeliveryProof(
        session: DriverSession,
        missionId: String,
        stopId: String,
        request: SubmitDeliveryProofRequestDto,
    ) {
        events += "proof:$missionId:$stopId"
    }
}

private fun fakeSession(): DriverSession =
    DriverSession(
        accessToken = "token",
        expiresAtUtc = "2026-07-16T12:00:00Z",
        userId = "user-1",
        email = "driver@northwind.local",
        fullName = "Northwind Driver",
        organizationName = "Northwind Logistics",
        driverId = "driver-1",
        roles = listOf("Driver"),
    )

private fun fakeMission(status: DriverMissionStatus = DriverMissionStatus.Assigned): DriverMission =
    DriverMission(
        id = "mission-1",
        reference = "NW-M-1",
        title = "Morning route",
        status = status,
        scheduledStartUtc = "2026-07-16T08:00:00Z",
        scheduledEndUtc = "2026-07-16T10:00:00Z",
        vehicleRegistrationNumber = "NW-100",
        stopCount = 2,
        simulatedDelayMinutes = 0,
        rowVersion = 3,
        syncState = MissionSyncState.Synced,
        stops = listOf(
            DriverMissionStop("stop-1", 1, "Depot", "1 Dispatch Way", "2026-07-16T08:15:00Z"),
            DriverMissionStop("stop-2", 2, "Client", "22 Fleet Street", "2026-07-16T09:10:00Z"),
        ),
        timeline = emptyList(),
    )
