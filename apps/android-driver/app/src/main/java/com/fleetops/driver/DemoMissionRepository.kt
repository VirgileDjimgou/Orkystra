package com.fleetops.driver

interface MissionRepository {
    suspend fun listAssignedMissions(): List<DriverMission>
}

class DemoMissionRepository : MissionRepository {
    override suspend fun listAssignedMissions(): List<DriverMission> =
        listOf(
            DriverMission(
                id = "demo-1",
                reference = "ZN-1001",
                destination = "Depot Est",
                status = "Ready",
            ),
            DriverMission(
                id = "demo-2",
                reference = "ZN-1002",
                destination = "Client Nord",
                status = "Queued",
            ),
        )
}
