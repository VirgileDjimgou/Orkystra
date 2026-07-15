package com.fleetops.driver

data class DriverMission(
    val id: String,
    val reference: String,
    val destination: String,
    val status: String,
)

data class DriverDashboardState(
    val missions: List<DriverMission> = emptyList(),
    val isRefreshing: Boolean = false,
) {
    val isEmpty: Boolean
        get() = missions.isEmpty()
}
