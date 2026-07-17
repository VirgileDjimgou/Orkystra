package com.fleetops.driver

import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter

enum class DriverMissionStatus {
    Draft,
    Planned,
    Assigned,
    EnRoute,
    Arrived,
    Delayed,
    Completed,
    Cancelled,
}

enum class DriverMissionAction {
    Start,
    Arrive,
    Complete,
}

enum class MissionSyncState {
    Synced,
    Pending,
    Conflict,
    Offline,
}

data class DriverSession(
    val accessToken: String,
    val expiresAtUtc: String,
    val userId: String,
    val email: String,
    val fullName: String,
    val organizationName: String,
    val driverId: String,
    val roles: List<String>,
)

data class DriverMissionStop(
    val id: String,
    val sequence: Int,
    val name: String,
    val address: String,
    val plannedArrivalUtc: String,
)

data class DriverMissionTimelineEvent(
    val id: String,
    val eventType: String,
    val description: String,
    val occurredAtUtc: String,
)

data class DriverMission(
    val id: String,
    val reference: String,
    val title: String,
    val status: DriverMissionStatus,
    val scheduledStartUtc: String,
    val scheduledEndUtc: String,
    val vehicleRegistrationNumber: String?,
    val stopCount: Int,
    val simulatedDelayMinutes: Int,
    val rowVersion: Long,
    val syncState: MissionSyncState,
    val stops: List<DriverMissionStop>,
    val timeline: List<DriverMissionTimelineEvent>,
) {
    val destination: String?
        get() = stops.maxByOrNull { it.sequence }?.name
}

data class PendingMissionCommand(
    val commandId: String,
    val missionId: String,
    val action: DriverMissionAction,
    val rowVersion: Long,
    val occurredAtUtc: String,
)

enum class DriverWorkflowOperationType {
    Inspection,
    DeliveryProof,
}

enum class DriverDefectSeverity {
    None,
    Minor,
    Major,
    Critical,
}

data class PendingPhotoUpload(
    val localId: String,
    val fileName: String,
    val contentType: String,
    val base64Content: String,
    val caption: String? = null,
    val uploadSessionId: String? = null,
    val uploadedBytes: Long = 0,
    val remoteAssetId: String? = null,
)

data class CapturedEvidence(
    val fileName: String,
    val contentType: String,
    val base64Content: String,
    val caption: String,
)

data class PendingInspectionOperation(
    val notes: String?,
    val completedAtUtc: String,
    val items: List<PendingInspectionItem>,
)

data class PendingInspectionItem(
    val sequence: Int,
    val code: String,
    val label: String,
    val isPass: Boolean,
    val defectSeverity: DriverDefectSeverity,
    val notes: String?,
    val photoLocalId: String?,
)

data class PendingDeliveryProofOperation(
    val recipientName: String,
    val signatureName: String,
    val deliveredAtUtc: String,
    val notes: String?,
    val stopId: String,
    val photoLocalIds: List<String>,
)

data class PendingWorkflowOperation(
    val commandId: String,
    val missionId: String,
    val operationType: DriverWorkflowOperationType,
    val payloadJson: String,
    val photos: List<PendingPhotoUpload>,
    val createdAtUtc: String,
)

data class DriverAppUiState(
    val session: DriverSession? = null,
    val missions: List<DriverMission> = emptyList(),
    val selectedMission: DriverMission? = null,
    val isBusy: Boolean = false,
    val errorMessage: String? = null,
    val infoMessage: String? = null,
) {
    val isLoggedIn: Boolean
        get() = session != null
}

fun DriverMission.availableActions(): List<DriverMissionAction> =
    when (status) {
        DriverMissionStatus.Assigned -> listOf(DriverMissionAction.Start)
        DriverMissionStatus.EnRoute,
        DriverMissionStatus.Delayed -> listOf(DriverMissionAction.Arrive)
        DriverMissionStatus.Arrived -> listOf(DriverMissionAction.Complete)
        else -> emptyList()
    }

fun DriverMission.applyLocalAction(action: DriverMissionAction): DriverMission =
    copy(
        status = when (action) {
            DriverMissionAction.Start -> DriverMissionStatus.EnRoute
            DriverMissionAction.Arrive -> DriverMissionStatus.Arrived
            DriverMissionAction.Complete -> DriverMissionStatus.Completed
        },
        syncState = MissionSyncState.Pending,
    )

fun DriverMissionStatus.label(): String =
    when (this) {
        DriverMissionStatus.Draft -> "Draft"
        DriverMissionStatus.Planned -> "Planned"
        DriverMissionStatus.Assigned -> "Assigned"
        DriverMissionStatus.EnRoute -> "En route"
        DriverMissionStatus.Arrived -> "Arrived"
        DriverMissionStatus.Delayed -> "Delayed"
        DriverMissionStatus.Completed -> "Completed"
        DriverMissionStatus.Cancelled -> "Cancelled"
    }

fun MissionSyncState.label(): String =
    when (this) {
        MissionSyncState.Synced -> "Synced"
        MissionSyncState.Pending -> "Pending sync"
        MissionSyncState.Conflict -> "Needs reload"
        MissionSyncState.Offline -> "Offline"
    }

fun DriverMission.nextActionLabel(): String =
    when (availableActions().firstOrNull()) {
        DriverMissionAction.Start -> "Complete inspection, then start"
        DriverMissionAction.Arrive -> "Confirm arrival at the active stop"
        DriverMissionAction.Complete -> "Capture delivery proof"
        null -> "No driver action is waiting"
    }

fun DriverMission.routeProgressLabel(): String =
    when (status) {
        DriverMissionStatus.Assigned -> "0 of 3 steps"
        DriverMissionStatus.EnRoute, DriverMissionStatus.Delayed -> "1 of 3 steps"
        DriverMissionStatus.Arrived -> "2 of 3 steps"
        DriverMissionStatus.Completed -> "3 of 3 steps"
        else -> "Route not started"
    }

fun String.toFriendlyDateTime(): String =
    runCatching {
        DateTimeFormatter.ofPattern("dd MMM HH:mm")
            .withZone(ZoneId.systemDefault())
            .format(Instant.parse(this))
    }.getOrElse { this }

const val SAMPLE_PNG_BASE64 =
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9pN96ZQAAAAASUVORK5CYII="
