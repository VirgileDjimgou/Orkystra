package com.fleetops.driver

import com.google.gson.JsonParser
import org.junit.Assert.assertEquals
import org.junit.Test

class DriverApiMappingTest {
    @Test
    fun detailMappingAcceptsNumericEnumsFromBackend() {
        val mission = DriverMissionDetailDto(
            id = "mission-1",
            reference = "NW-PILOT-001",
            title = "Pilot delivery loop",
            status = JsonParser.parseString("5"),
            scheduledStartUtc = "2026-07-16T20:47:43Z",
            scheduledEndUtc = "2026-07-16T22:47:43Z",
            vehicleRegistrationNumber = "NW-100",
            simulatedDelayMinutes = 18,
            rowVersion = 5,
            stops = listOf(
                DriverMissionStopDto(
                    id = "stop-1",
                    sequence = 1,
                    name = "Northwind Depot",
                    address = "1 Dispatch Way",
                    plannedArrivalUtc = "2026-07-16T21:17:43Z",
                ),
            ),
            timeline = listOf(
                DriverMissionTimelineEventDto(
                    id = "event-1",
                    eventType = JsonParser.parseString("4"),
                    description = "Mission delay simulated: 18 minutes.",
                    occurredAtUtc = "2026-07-16T20:52:34Z",
                ),
            ),
        )

        val mapped = mission.toDomain(
            fallbackStopCount = 1,
            syncState = MissionSyncState.Synced,
        )

        assertEquals(DriverMissionStatus.Delayed, mapped.status)
        assertEquals("Delay simulated", mapped.timeline.single().eventType)
    }
}
