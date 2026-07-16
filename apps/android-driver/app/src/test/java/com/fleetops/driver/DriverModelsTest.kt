package com.fleetops.driver

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class DriverModelsTest {
    @Test
    fun availableActionsFollowDriverFlow() {
        assertEquals(listOf(DriverMissionAction.Start), sampleMission(DriverMissionStatus.Assigned).availableActions())
        assertEquals(listOf(DriverMissionAction.Arrive), sampleMission(DriverMissionStatus.EnRoute).availableActions())
        assertEquals(listOf(DriverMissionAction.Complete), sampleMission(DriverMissionStatus.Arrived).availableActions())
        assertTrue(sampleMission(DriverMissionStatus.Completed).availableActions().isEmpty())
    }

    @Test
    fun localActionMovesMissionToPending() {
        val updated = sampleMission(DriverMissionStatus.Assigned).applyLocalAction(DriverMissionAction.Start)

        assertEquals(DriverMissionStatus.EnRoute, updated.status)
        assertEquals(MissionSyncState.Pending, updated.syncState)
    }

    private fun sampleMission(status: DriverMissionStatus): DriverMission =
        DriverMission(
            id = "mission-1",
            reference = "NW-100",
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
}
