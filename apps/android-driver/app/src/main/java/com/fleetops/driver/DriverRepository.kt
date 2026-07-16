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
import java.util.UUID

interface DriverRepository {
    fun observeSession(): Flow<DriverSession?>
    fun observeMissions(): Flow<List<DriverMission>>
    fun observeMission(missionId: String): Flow<DriverMission?>
    suspend fun login(email: String, password: String)
    suspend fun refresh()
    suspend fun queueMissionAction(missionId: String, action: DriverMissionAction)
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

    override suspend fun login(email: String, password: String) {
        val session = remoteDataSource.login(email, password)
        localStore.saveSession(session)
        syncScheduler.ensureScheduled()
        refresh()
    }

    override suspend fun refresh() {
        val session = requireSession()
        try {
            localStore.replaceMissions(remoteDataSource.listMissionDetails(session))
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

    override suspend fun flushPendingCommands() {
        val session = requireSession()
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
}

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
