package com.fleetops.driver

import android.content.Context
import androidx.room.Room
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import kotlinx.coroutines.runBlocking
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertFalse
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class DriverDatabaseInstrumentationTest {
    private lateinit var database: DriverDatabase
    private lateinit var store: RoomDriverLocalStore
    private lateinit var context: Context
    private lateinit var credentialStore: InMemoryDriverCredentialStore

    @Before
    fun setUp() {
        context = ApplicationProvider.getApplicationContext()
        context.deleteDatabase("fleetops-driver-instrumentation")
        credentialStore = InMemoryDriverCredentialStore()
        database = buildDatabase("fleetops-driver-instrumentation")
        store = RoomDriverLocalStore(database, credentialStore)
    }

    @After
    fun tearDown() {
        database.close()
        context.deleteDatabase("fleetops-driver-instrumentation")
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
        store = RoomDriverLocalStore(database, credentialStore)

        val reopenedMission = store.getMission("mission-1")
        val pendingCommands = store.pendingCommands()

        assertNotNull(reopenedMission)
        assertEquals(MissionSyncState.Pending, reopenedMission?.syncState)
        assertEquals(1, pendingCommands.size)
        assertEquals("cmd-1", pendingCommands.single().commandId)
    }

    @Test
    fun accessTokenIsKeptOutsideRoom() = runBlocking {
        store.saveSession(
            DriverSession(
                accessToken = "sensitive-access-token",
                expiresAtUtc = "2026-07-18T08:00:00Z",
                userId = "user-1",
                email = "driver@northwind.local",
                fullName = "Northwind Driver",
                organizationName = "Northwind Logistics",
                driverId = "driver-1",
                roles = listOf("Driver"),
            ),
        )

        val columns = mutableListOf<String>()
        database.openHelper.readableDatabase.query("PRAGMA table_info(driver_session)").use { cursor ->
            val nameIndex = cursor.getColumnIndexOrThrow("name")
            while (cursor.moveToNext()) {
                columns += cursor.getString(nameIndex)
            }
        }

        assertFalse(columns.contains("accessToken"))
        assertEquals("sensitive-access-token", store.getSession()?.accessToken)
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

    @Test
    fun capturedEvidenceAndUploadProgressSurviveDatabaseReopen() = runBlocking {
        val operation = PendingWorkflowOperation(
            commandId = "workflow-resume-1",
            missionId = "mission-1",
            operationType = DriverWorkflowOperationType.DeliveryProof,
            payloadJson = """{"recipientName":"Taylor Receiver"}""",
            photos = listOf(
                PendingPhotoUpload(
                    localId = "proof-1",
                    fileName = "delivery.jpg",
                    contentType = "image/jpeg",
                    base64Content = SAMPLE_PNG_BASE64,
                    caption = DELIVERY_PHOTO_CAPTION,
                    uploadSessionId = "session-1",
                    uploadedBytes = 24,
                ),
                PendingPhotoUpload(
                    localId = "signature-1",
                    fileName = "signature.png",
                    contentType = "image/png",
                    base64Content = SAMPLE_PNG_BASE64,
                    caption = SIGNATURE_CAPTION,
                ),
            ),
            createdAtUtc = "2026-07-17T09:00:00Z",
        )
        store.enqueueWorkflowOperation(operation)

        database.close()
        database = buildDatabase("fleetops-driver-instrumentation")
        store = RoomDriverLocalStore(database, credentialStore)

        val recovered = store.pendingWorkflowOperations().single()
        assertEquals("session-1", recovered.photos.first().uploadSessionId)
        assertEquals(24, recovered.photos.first().uploadedBytes)
        assertEquals(SIGNATURE_CAPTION, recovered.photos[1].caption)
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
