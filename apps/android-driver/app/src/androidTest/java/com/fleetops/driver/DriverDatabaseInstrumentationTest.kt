package com.fleetops.driver

import android.content.Context
import androidx.room.Room
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import kotlinx.coroutines.runBlocking
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class DriverDatabaseInstrumentationTest {
    private lateinit var database: DriverDatabase
    private lateinit var store: RoomDriverLocalStore
    private lateinit var context: Context

    @Before
    fun setUp() {
        context = ApplicationProvider.getApplicationContext()
        database = buildDatabase("fleetops-driver-instrumentation")
        store = RoomDriverLocalStore(database)
    }

    @After
    fun tearDown() {
        database.close()
    }

    @Test
    fun queuedCommandPersistsAcrossDatabaseReopen() = runBlocking {
        store.saveMission(testMission())
        store.enqueue(
            PendingMissionCommand(
                commandId = "cmd-1",
                missionId = "mission-1",
                action = DriverMissionAction.Start,
                rowVersion = 3,
                occurredAtUtc = "2026-07-16T08:15:00Z",
            ),
        )

        database.close()
        database = buildDatabase("fleetops-driver-instrumentation")
        store = RoomDriverLocalStore(database)

        val reopenedMission = store.getMission("mission-1")
        val pendingCommands = store.pendingCommands()

        assertNotNull(reopenedMission)
        assertEquals(MissionSyncState.Pending, reopenedMission?.syncState)
        assertEquals(1, pendingCommands.size)
        assertEquals("cmd-1", pendingCommands.single().commandId)
    }

    @Test
    fun duplicateWorkflowCommandIdDoesNotCreateDuplicateRows() = runBlocking {
        val operation = PendingWorkflowOperation(
            commandId = "workflow-1",
            missionId = "mission-1",
            operationType = DriverWorkflowOperationType.DeliveryProof,
            payloadJson = """{"recipientName":"Taylor Receiver"}""",
            photos = listOf(
                PendingPhotoUpload(
                    localId = "photo-1",
                    fileName = "proof-demo.png",
                    contentType = "image/png",
                    base64Content = SAMPLE_PNG_BASE64,
                ),
            ),
            createdAtUtc = "2026-07-16T09:00:00Z",
        )

        store.enqueueWorkflowOperation(operation)
        store.enqueueWorkflowOperation(operation.copy(createdAtUtc = "2026-07-16T09:05:00Z"))

        val pending = store.pendingWorkflowOperations()
        assertEquals(1, pending.size)
        assertEquals("workflow-1", pending.single().commandId)
        assertEquals("2026-07-16T09:05:00Z", pending.single().createdAtUtc)
    }

    private fun buildDatabase(name: String): DriverDatabase =
        Room.databaseBuilder(context, DriverDatabase::class.java, name)
            .allowMainThreadQueries()
            .build()

    private fun testMission() = DriverMission(
        id = "mission-1",
        reference = "NW-M-ROOM-1",
        title = "Room persistence proof",
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
            DriverMissionStop("stop-2", 2, "Customer", "22 Fleet Street", "2026-07-16T09:10:00Z"),
        ),
        timeline = listOf(
            DriverMissionTimelineEvent(
                id = "timeline-1",
                eventType = "Created",
                description = "Mission NW-M-ROOM-1 created.",
                occurredAtUtc = "2026-07-16T07:45:00Z",
            ),
        ),
    )
}
