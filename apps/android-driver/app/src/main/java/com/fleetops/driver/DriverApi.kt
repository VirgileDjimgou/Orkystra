package com.fleetops.driver

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
    val status: String,
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
    val eventType: String,
    val description: String,
    val occurredAtUtc: String,
)

data class DriverMissionDetailDto(
    val id: String,
    val reference: String,
    val title: String,
    val status: String,
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

interface DriverApiService {
    @POST("/api/auth/login")
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
}

interface DriverRemoteDataSource {
    suspend fun login(email: String, password: String): DriverSession
    suspend fun listMissionDetails(session: DriverSession): List<DriverMission>
    suspend fun syncMissionCommand(session: DriverSession, command: PendingMissionCommand): DriverMission
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

private fun DriverMissionDetailDto.toDomain(
    fallbackStopCount: Int,
    syncState: MissionSyncState,
): DriverMission =
    DriverMission(
        id = id,
        reference = reference,
        title = title,
        status = enumValueOf(status),
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
                eventType = it.eventType,
                description = it.description,
                occurredAtUtc = it.occurredAtUtc,
            )
        },
    )

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
