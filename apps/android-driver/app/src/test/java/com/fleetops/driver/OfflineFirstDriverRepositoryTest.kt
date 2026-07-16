package com.fleetops.driver

import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Test
import java.io.IOException

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
}

private class FakeDriverSyncScheduler : DriverSyncScheduler {
    override fun ensureScheduled() {}
    override fun cancel() {}
}

private class FakeDriverLocalStore : DriverLocalStore {
    val session = MutableStateFlow<DriverSession?>(null)
    val missions = MutableStateFlow<List<DriverMission>>(emptyList())
    val pending = mutableListOf<PendingMissionCommand>()

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

    override suspend fun clearAll() {
        session.value = null
        missions.value = emptyList()
        pending.clear()
    }
}

private class FakeDriverRemoteDataSource(
    private val throwOfflineOnSync: Boolean = false,
) : DriverRemoteDataSource {
    override suspend fun login(email: String, password: String): DriverSession = fakeSession()

    override suspend fun listMissionDetails(session: DriverSession): List<DriverMission> = listOf(fakeMission())

    override suspend fun syncMissionCommand(session: DriverSession, command: PendingMissionCommand): DriverMission {
        if (throwOfflineOnSync) {
            throw IOException("offline")
        }

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

private fun fakeMission(): DriverMission =
    DriverMission(
        id = "mission-1",
        reference = "NW-M-1",
        title = "Morning route",
        status = DriverMissionStatus.Assigned,
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
