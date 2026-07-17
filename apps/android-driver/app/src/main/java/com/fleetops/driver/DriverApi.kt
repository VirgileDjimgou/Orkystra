package com.fleetops.driver

import com.google.gson.JsonElement
import okhttp3.OkHttpClient
import okhttp3.logging.HttpLoggingInterceptor
import retrofit2.HttpException
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.Header
import retrofit2.http.POST
import retrofit2.http.Path

data class LoginRequestDto(
    val email: String,
    val password: String,
)

data class AuthenticatedUserDto(
    val userId: String,
    val email: String,
    val fullName: String,
    val organizationName: String,
    val driverId: String?,
    val roles: List<String>,
)

data class LoginResponseDto(
    val accessToken: String,
    val expiresAtUtc: String,
    val user: AuthenticatedUserDto,
)

data class DriverMissionSummaryDto(
    val id: String,
    val reference: String,
    val title: String,
    val status: JsonElement,
    val scheduledStartUtc: String,
    val scheduledEndUtc: String,
    val vehicleRegistrationNumber: String?,
    val stopCount: Int,
    val pendingCommandCount: Int,
    val rowVersion: Long,
)

data class DriverMissionStopDto(
    val id: String,
    val sequence: Int,
    val name: String,
    val address: String,
    val plannedArrivalUtc: String,
)

data class DriverMissionTimelineEventDto(
    val id: String,
    val eventType: JsonElement,
    val description: String,
    val occurredAtUtc: String,
)

data class DriverMissionDetailDto(
    val id: String,
    val reference: String,
    val title: String,
    val status: JsonElement,
    val scheduledStartUtc: String,
    val scheduledEndUtc: String,
    val vehicleRegistrationNumber: String?,
    val simulatedDelayMinutes: Int,
    val rowVersion: Long,
    val stops: List<DriverMissionStopDto>,
    val timeline: List<DriverMissionTimelineEventDto>,
)

data class SyncMissionCommandRequestDto(
    val commandId: String,
    val action: String,
    val rowVersion: Long,
    val occurredAtUtc: String,
)

data class SyncMissionCommandResponseDto(
    val mission: DriverMissionDetailDto,
    val wasDuplicate: Boolean,
)

data class UploadSessionRequestDto(
    val fileName: String,
    val contentType: String,
    val totalBytes: Long,
    val purpose: String,
)

data class UploadSessionResponseDto(
    val uploadSessionId: String,
    val uploadedBytes: Long,
    val totalBytes: Long,
    val expiresAtUtc: String,
    val isCompleted: Boolean,
    val mediaAssetId: String?,
)

data class AppendUploadChunkRequestDto(
    val offset: Long,
    val base64Content: String,
)

data class MediaAssetResponseDto(
    val assetId: String,
    val fileName: String,
    val contentType: String,
    val sizeBytes: Long,
    val readUrl: String,
)

data class InspectionItemResultRequestDto(
    val sequence: Int,
    val code: String,
    val label: String,
    val isPass: Boolean,
    val defectSeverity: String,
    val notes: String?,
    val photoAssetId: String?,
)

data class SubmitPreDepartureInspectionRequestDto(
    val commandId: String,
    val completedAtUtc: String,
    val notes: String?,
    val items: List<InspectionItemResultRequestDto>,
)

data class DeliveryProofPhotoRequestDto(
    val mediaAssetId: String,
    val caption: String?,
)

data class SubmitDeliveryProofRequestDto(
    val commandId: String,
    val recipientName: String,
    val signatureName: String,
    val deliveredAtUtc: String,
    val notes: String?,
    val photos: List<DeliveryProofPhotoRequestDto>,
)

interface DriverApiService {
    @POST("/api/v1/auth/login")
    suspend fun login(@Body request: LoginRequestDto): LoginResponseDto

    @GET("/api/v1/driver/missions")
    suspend fun listMissions(@Header("Authorization") authorization: String): List<DriverMissionSummaryDto>

    @GET("/api/v1/driver/missions/{missionId}")
    suspend fun getMission(
        @Header("Authorization") authorization: String,
        @Path("missionId") missionId: String,
    ): DriverMissionDetailDto

    @POST("/api/v1/driver/missions/{missionId}/commands")
    suspend fun syncMissionCommand(
        @Header("Authorization") authorization: String,
        @Path("missionId") missionId: String,
        @Body request: SyncMissionCommandRequestDto,
    ): SyncMissionCommandResponseDto

    @POST("/api/v1/driver/uploads/sessions")
    suspend fun createUploadSession(
        @Header("Authorization") authorization: String,
        @Body request: UploadSessionRequestDto,
    ): UploadSessionResponseDto

    @POST("/api/v1/driver/uploads/sessions/{sessionId}/chunks")
    suspend fun appendUploadChunk(
        @Header("Authorization") authorization: String,
        @Path("sessionId") sessionId: String,
        @Body request: AppendUploadChunkRequestDto,
    ): UploadSessionResponseDto

    @POST("/api/v1/driver/uploads/sessions/{sessionId}/complete")
    suspend fun completeUploadSession(
        @Header("Authorization") authorization: String,
        @Path("sessionId") sessionId: String,
    ): MediaAssetResponseDto

    @POST("/api/v1/driver/missions/{missionId}/inspection")
    suspend fun submitInspection(
        @Header("Authorization") authorization: String,
        @Path("missionId") missionId: String,
        @Body request: SubmitPreDepartureInspectionRequestDto,
    )

    @POST("/api/v1/driver/missions/{missionId}/stops/{stopId}/proof")
    suspend fun submitDeliveryProof(
        @Header("Authorization") authorization: String,
        @Path("missionId") missionId: String,
        @Path("stopId") stopId: String,
        @Body request: SubmitDeliveryProofRequestDto,
    )
}

interface DriverRemoteDataSource {
    suspend fun login(email: String, password: String): DriverSession
    suspend fun listMissionDetails(session: DriverSession): List<DriverMission>
    suspend fun syncMissionCommand(session: DriverSession, command: PendingMissionCommand): DriverMission
    suspend fun createUploadSession(
        session: DriverSession,
        fileName: String,
        contentType: String,
        totalBytes: Long,
        purpose: String,
    ): UploadSessionResponseDto
    suspend fun appendUploadChunk(
        session: DriverSession,
        sessionId: String,
        offset: Long,
        base64Content: String,
    ): UploadSessionResponseDto
    suspend fun completeUploadSession(session: DriverSession, sessionId: String): MediaAssetResponseDto
    suspend fun submitInspection(session: DriverSession, missionId: String, request: SubmitPreDepartureInspectionRequestDto)
    suspend fun submitDeliveryProof(session: DriverSession, missionId: String, stopId: String, request: SubmitDeliveryProofRequestDto)
}

class RetrofitDriverRemoteDataSource(
    private val service: DriverApiService,
) : DriverRemoteDataSource {
    override suspend fun login(email: String, password: String): DriverSession {
        val response = service.login(LoginRequestDto(email.trim(), password))
        val driverId = response.user.driverId
            ?: throw IllegalStateException("This account is not linked to a driver profile.")

        return DriverSession(
            accessToken = response.accessToken,
            expiresAtUtc = response.expiresAtUtc,
            userId = response.user.userId,
            email = response.user.email,
            fullName = response.user.fullName,
            organizationName = response.user.organizationName,
            driverId = driverId,
            roles = response.user.roles,
        )
    }

    override suspend fun listMissionDetails(session: DriverSession): List<DriverMission> {
        val authorization = session.asAuthorization()
        return service.listMissions(authorization).map { summary ->
            service.getMission(authorization, summary.id).toDomain(
                fallbackStopCount = summary.stopCount,
                syncState = if (summary.pendingCommandCount > 0) MissionSyncState.Pending else MissionSyncState.Synced,
            )
        }
    }

    override suspend fun syncMissionCommand(session: DriverSession, command: PendingMissionCommand): DriverMission {
        val response = service.syncMissionCommand(
            session.asAuthorization(),
            command.missionId,
            SyncMissionCommandRequestDto(
                commandId = command.commandId,
                action = command.action.name,
                rowVersion = command.rowVersion,
                occurredAtUtc = command.occurredAtUtc,
            ),
        )

        return response.mission.toDomain(
            fallbackStopCount = response.mission.stops.size,
            syncState = MissionSyncState.Synced,
        )
    }

    override suspend fun createUploadSession(
        session: DriverSession,
        fileName: String,
        contentType: String,
        totalBytes: Long,
        purpose: String,
    ): UploadSessionResponseDto =
        service.createUploadSession(
            session.asAuthorization(),
            UploadSessionRequestDto(fileName, contentType, totalBytes, purpose),
        )

    override suspend fun appendUploadChunk(
        session: DriverSession,
        sessionId: String,
        offset: Long,
        base64Content: String,
    ): UploadSessionResponseDto =
        service.appendUploadChunk(
            session.asAuthorization(),
            sessionId,
            AppendUploadChunkRequestDto(offset, base64Content),
        )

    override suspend fun completeUploadSession(session: DriverSession, sessionId: String): MediaAssetResponseDto =
        service.completeUploadSession(session.asAuthorization(), sessionId)

    override suspend fun submitInspection(
        session: DriverSession,
        missionId: String,
        request: SubmitPreDepartureInspectionRequestDto,
    ) {
        service.submitInspection(session.asAuthorization(), missionId, request)
    }

    override suspend fun submitDeliveryProof(
        session: DriverSession,
        missionId: String,
        stopId: String,
        request: SubmitDeliveryProofRequestDto,
    ) {
        service.submitDeliveryProof(session.asAuthorization(), missionId, stopId, request)
    }
}

fun buildDriverApiService(): DriverApiService {
    val logging = HttpLoggingInterceptor().apply { level = HttpLoggingInterceptor.Level.BASIC }
    val client = OkHttpClient.Builder()
        .addInterceptor(logging)
        .build()

    return Retrofit.Builder()
        .baseUrl(BuildConfig.API_BASE_URL)
        .client(client)
        .addConverterFactory(GsonConverterFactory.create())
        .build()
        .create(DriverApiService::class.java)
}

private fun DriverSession.asAuthorization(): String = "Bearer $accessToken"

internal fun DriverMissionDetailDto.toDomain(
    fallbackStopCount: Int,
    syncState: MissionSyncState,
): DriverMission =
    DriverMission(
        id = id,
        reference = reference,
        title = title,
        status = status.toMissionStatus(),
        scheduledStartUtc = scheduledStartUtc,
        scheduledEndUtc = scheduledEndUtc,
        vehicleRegistrationNumber = vehicleRegistrationNumber,
        stopCount = maxOf(fallbackStopCount, stops.size),
        simulatedDelayMinutes = simulatedDelayMinutes,
        rowVersion = rowVersion,
        syncState = syncState,
        stops = stops.map {
            DriverMissionStop(
                id = it.id,
                sequence = it.sequence,
                name = it.name,
                address = it.address,
                plannedArrivalUtc = it.plannedArrivalUtc,
            )
        },
        timeline = timeline.map {
            DriverMissionTimelineEvent(
                id = it.id,
                eventType = it.eventType.toTimelineEventLabel(),
                description = it.description,
                occurredAtUtc = it.occurredAtUtc,
            )
        },
    )

internal fun JsonElement.toMissionStatus(): DriverMissionStatus {
    val rawValue = toRawString()
    val fromName = DriverMissionStatus.values().firstOrNull { it.name.equals(rawValue, ignoreCase = true) }
    if (fromName != null) {
        return fromName
    }

    val ordinal = rawValue.toIntOrNull()
    if (ordinal != null) {
        return DriverMissionStatus.values().getOrElse(ordinal) {
            throw IllegalArgumentException("Unsupported mission status '$rawValue'.")
        }
    }

    throw IllegalArgumentException("Unsupported mission status '$rawValue'.")
}

internal fun JsonElement.toTimelineEventLabel(): String {
    val rawValue = toRawString()
    val ordinal = rawValue.toIntOrNull()
    return when (ordinal) {
        0 -> "Created"
        1 -> "Updated"
        2 -> "Assignment changed"
        3 -> "Status changed"
        4 -> "Delay simulated"
        null -> rawValue
        else -> "Event $ordinal"
    }
}

internal fun JsonElement.toRawString(): String =
    when {
        isJsonNull -> stringValueOrEmpty()
        isJsonPrimitive -> asJsonPrimitive.run {
            when {
                isString -> asString
                isNumber -> asNumber.toInt().toString()
                isBoolean -> asBoolean.toString()
                else -> toString()
            }
        }
        else -> toString()
    }

internal fun JsonElement.stringValueOrEmpty(): String = ""

fun Throwable.toUserMessage(): String =
    when (this) {
        is HttpException -> when (code()) {
            401 -> "Your session expired. Please sign in again."
            403 -> "This account is not authorized for the driver app."
            409 -> "Mission changed on the server. Reload before retrying."
            else -> "The server rejected this request (${code()})."
        }
        else -> message ?: "Unexpected mobile sync error."
    }
