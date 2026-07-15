package com.fleetops.driver

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class DriverDashboardStateTest {
    @Test
    fun isEmptyReflectsMissionList() {
        assertTrue(DriverDashboardState().isEmpty)

        val state = DriverDashboardState(
            missions = listOf(
                DriverMission(
                    id = "demo",
                    reference = "ZN-1001",
                    destination = "Depot Est",
                    status = "Ready",
                ),
            ),
        )

        assertFalse(state.isEmpty)
    }
}
