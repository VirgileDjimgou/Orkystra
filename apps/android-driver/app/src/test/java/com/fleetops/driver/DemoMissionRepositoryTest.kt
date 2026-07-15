package com.fleetops.driver

import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class DemoMissionRepositoryTest {
    @Test
    fun listAssignedMissionsReturnsDemoMissions() = runTest {
        val missions = DemoMissionRepository().listAssignedMissions()

        assertEquals(2, missions.size)
        assertEquals("ZN-1001", missions.first().reference)
        assertTrue(missions.all { it.destination.isNotBlank() })
    }
}
