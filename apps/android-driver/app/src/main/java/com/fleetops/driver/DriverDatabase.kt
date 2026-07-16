package com.fleetops.driver

import android.content.Context
import androidx.room.Dao
import androidx.room.Database
import androidx.room.Embedded
import androidx.room.Entity
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.PrimaryKey
import androidx.room.Query
import androidx.room.Relation
import androidx.room.Room
import androidx.room.RoomDatabase
import androidx.room.Transaction
import androidx.room.withTransaction
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map

@Entity(tableName = "driver_session")
data class DriverSessionEntity(
    @PrimaryKey val id: Int = 0,
    val accessToken: String,
    val expiresAtUtc: String,
    val userId: String,
    val email: String,
    val fullName: String,
    val organizationName: String,
    val driverId: String,
    val rolesCsv: String,
)

@Entity(tableName = "driver_missions")
data class DriverMissionEntity(
    @PrimaryKey val id: String,
    val reference: String,
    val title: String,
    val status: String,
    val scheduledStartUtc: String,
    val scheduledEndUtc: String,
    val vehicleRegistrationNumber: String?,
    val stopCount: Int,
    val simulatedDelayMinutes: Int,
    val rowVersion: Long,
    val syncState: String,
)

@Entity(tableName = "driver_mission_stops", primaryKeys = ["missionId", "sequence"])
data class DriverMissionStopEntity(
    val missionId: String,
    val sequence: Int,
    val id: String,
    val name: String,
    val address: String,
    val plannedArrivalUtc: String,
)

@Entity(tableName = "driver_mission_timeline")
data class DriverMissionTimelineEventEntity(
    @PrimaryKey val id: String,
    val missionId: String,
    val eventType: String,
    val description: String,
    val occurredAtUtc: String,
)

@Entity(tableName = "driver_outbox")
data class PendingMissionCommandEntity(
    @PrimaryKey val commandId: String,
    val missionId: String,
    val action: String,
    val rowVersion: Long,
    val occurredAtUtc: String,
)

@Entity(tableName = "driver_workflow_outbox")
data class PendingWorkflowOperationEntity(
    @PrimaryKey val commandId: String,
    val missionId: String,
    val operationType: String,
    val payloadJson: String,
    val photosJson: String,
    val createdAtUtc: String,
)

data class DriverMissionAggregate(
    @Embedded val mission: DriverMissionEntity,
    @Relation(parentColumn = "id", entityColumn = "missionId")
    val stops: List<DriverMissionStopEntity>,
    @Relation(parentColumn = "id", entityColumn = "missionId")
    val timeline: List<DriverMissionTimelineEventEntity>,
)

@Dao
interface DriverSessionDao {
    @Query("SELECT * FROM driver_session WHERE id = 0")
    fun observe(): Flow<DriverSessionEntity?>

    @Query("SELECT * FROM driver_session WHERE id = 0")
    suspend fun get(): DriverSessionEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(session: DriverSessionEntity)

    @Query("DELETE FROM driver_session")
    suspend fun clear()
}

@Dao
interface DriverMissionDao {
    @Transaction
    @Query("SELECT * FROM driver_missions ORDER BY scheduledStartUtc")
    fun observeMissionAggregates(): Flow<List<DriverMissionAggregate>>

    @Transaction
    @Query("SELECT * FROM driver_missions ORDER BY scheduledStartUtc")
    suspend fun listMissionAggregates(): List<DriverMissionAggregate>

    @Transaction
    @Query("SELECT * FROM driver_missions WHERE id = :missionId")
    fun observeMissionAggregate(missionId: String): Flow<DriverMissionAggregate?>

    @Transaction
    @Query("SELECT * FROM driver_missions WHERE id = :missionId")
    suspend fun getMissionAggregate(missionId: String): DriverMissionAggregate?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsertMission(entity: DriverMissionEntity)

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsertStops(entities: List<DriverMissionStopEntity>)

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsertTimeline(entities: List<DriverMissionTimelineEventEntity>)

    @Query("DELETE FROM driver_mission_stops WHERE missionId = :missionId")
    suspend fun deleteStopsForMission(missionId: String)

    @Query("DELETE FROM driver_mission_timeline WHERE missionId = :missionId")
    suspend fun deleteTimelineForMission(missionId: String)

    @Query("UPDATE driver_missions SET syncState = :syncState WHERE id = :missionId")
    suspend fun updateSyncState(missionId: String, syncState: String)

    @Query("DELETE FROM driver_missions")
    suspend fun clearMissions()

    @Query("DELETE FROM driver_mission_stops")
    suspend fun clearStops()

    @Query("DELETE FROM driver_mission_timeline")
    suspend fun clearTimeline()
}

@Dao
interface PendingMissionCommandDao {
    @Query("SELECT * FROM driver_outbox ORDER BY occurredAtUtc")
    suspend fun listPending(): List<PendingMissionCommandEntity>

    @Query("SELECT COUNT(*) FROM driver_outbox WHERE missionId = :missionId")
    suspend fun countPendingForMission(missionId: String): Int

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun enqueue(command: PendingMissionCommandEntity)

    @Query("DELETE FROM driver_outbox WHERE commandId = :commandId")
    suspend fun delete(commandId: String)

    @Query("DELETE FROM driver_outbox")
    suspend fun clear()
}

@Dao
interface PendingWorkflowOperationDao {
    @Query("SELECT * FROM driver_workflow_outbox ORDER BY createdAtUtc")
    suspend fun listPending(): List<PendingWorkflowOperationEntity>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun enqueue(operation: PendingWorkflowOperationEntity)

    @Query("DELETE FROM driver_workflow_outbox WHERE commandId = :commandId")
    suspend fun delete(commandId: String)

    @Query("DELETE FROM driver_workflow_outbox")
    suspend fun clear()
}

@Database(
    entities = [
        DriverSessionEntity::class,
        DriverMissionEntity::class,
        DriverMissionStopEntity::class,
        DriverMissionTimelineEventEntity::class,
        PendingMissionCommandEntity::class,
        PendingWorkflowOperationEntity::class,
    ],
    version = 2,
    exportSchema = false,
)
abstract class DriverDatabase : RoomDatabase() {
    abstract fun sessionDao(): DriverSessionDao
    abstract fun missionDao(): DriverMissionDao
    abstract fun outboxDao(): PendingMissionCommandDao
    abstract fun workflowOutboxDao(): PendingWorkflowOperationDao

    companion object {
        fun build(context: Context): DriverDatabase =
            Room.databaseBuilder(context, DriverDatabase::class.java, "fleetops-driver.db")
                .fallbackToDestructiveMigration(dropAllTables = true)
                .build()
    }
}

interface DriverLocalStore {
    fun observeSession(): Flow<DriverSession?>
    suspend fun getSession(): DriverSession?
    suspend fun saveSession(session: DriverSession)
    suspend fun clearSession()
    fun observeMissions(): Flow<List<DriverMission>>
    suspend fun listMissions(): List<DriverMission>
    fun observeMission(missionId: String): Flow<DriverMission?>
    suspend fun getMission(missionId: String): DriverMission?
    suspend fun saveMission(mission: DriverMission)
    suspend fun replaceMissions(missions: List<DriverMission>)
    suspend fun updateMissionSyncState(missionId: String, syncState: MissionSyncState)
    suspend fun enqueue(command: PendingMissionCommand)
    suspend fun pendingCommands(): List<PendingMissionCommand>
    suspend fun removePendingCommand(commandId: String)
    suspend fun enqueueWorkflowOperation(operation: PendingWorkflowOperation)
    suspend fun pendingWorkflowOperations(): List<PendingWorkflowOperation>
    suspend fun saveWorkflowOperation(operation: PendingWorkflowOperation)
    suspend fun removeWorkflowOperation(commandId: String)
    suspend fun clearAll()
}

class RoomDriverLocalStore(
    private val database: DriverDatabase,
) : DriverLocalStore {
    private val sessionDao = database.sessionDao()
    private val missionDao = database.missionDao()
    private val outboxDao = database.outboxDao()
    private val workflowOutboxDao = database.workflowOutboxDao()

    override fun observeSession(): Flow<DriverSession?> = sessionDao.observe().map { it?.toDomain() }

    override suspend fun getSession(): DriverSession? = sessionDao.get()?.toDomain()

    override suspend fun saveSession(session: DriverSession) {
        sessionDao.upsert(session.toEntity())
    }

    override suspend fun clearSession() {
        sessionDao.clear()
    }

    override fun observeMissions(): Flow<List<DriverMission>> =
        missionDao.observeMissionAggregates().map { aggregates ->
            aggregates.map { aggregate ->
                val pendingCount = outboxDao.countPendingForMission(aggregate.mission.id)
                aggregate.toDomain(pendingCount)
            }
        }

    override suspend fun listMissions(): List<DriverMission> =
        missionDao.listMissionAggregates().map { aggregate ->
            aggregate.toDomain(outboxDao.countPendingForMission(aggregate.mission.id))
        }

    override fun observeMission(missionId: String): Flow<DriverMission?> =
        missionDao.observeMissionAggregate(missionId).map { aggregate ->
            aggregate?.toDomain(outboxDao.countPendingForMission(missionId))
        }

    override suspend fun getMission(missionId: String): DriverMission? =
        missionDao.getMissionAggregate(missionId)?.toDomain(outboxDao.countPendingForMission(missionId))

    override suspend fun saveMission(mission: DriverMission) {
        database.withTransaction {
            missionDao.upsertMission(mission.toEntity())
            missionDao.deleteStopsForMission(mission.id)
            missionDao.deleteTimelineForMission(mission.id)
            if (mission.stops.isNotEmpty()) {
                missionDao.upsertStops(mission.stops.map { it.toEntity(mission.id) })
            }
            if (mission.timeline.isNotEmpty()) {
                missionDao.upsertTimeline(mission.timeline.map { it.toEntity(mission.id) })
            }
        }
    }

    override suspend fun replaceMissions(missions: List<DriverMission>) {
        database.withTransaction {
            missionDao.clearTimeline()
            missionDao.clearStops()
            missionDao.clearMissions()
            missions.forEach { mission ->
                missionDao.upsertMission(mission.toEntity())
                if (mission.stops.isNotEmpty()) {
                    missionDao.upsertStops(mission.stops.map { it.toEntity(mission.id) })
                }
                if (mission.timeline.isNotEmpty()) {
                    missionDao.upsertTimeline(mission.timeline.map { it.toEntity(mission.id) })
                }
            }
        }
    }

    override suspend fun updateMissionSyncState(missionId: String, syncState: MissionSyncState) {
        missionDao.updateSyncState(missionId, syncState.name)
    }

    override suspend fun enqueue(command: PendingMissionCommand) {
        outboxDao.enqueue(command.toEntity())
    }

    override suspend fun pendingCommands(): List<PendingMissionCommand> =
        outboxDao.listPending().map { it.toDomain() }

    override suspend fun removePendingCommand(commandId: String) {
        outboxDao.delete(commandId)
    }

    override suspend fun enqueueWorkflowOperation(operation: PendingWorkflowOperation) {
        workflowOutboxDao.enqueue(operation.toEntity())
    }

    override suspend fun pendingWorkflowOperations(): List<PendingWorkflowOperation> =
        workflowOutboxDao.listPending().map { it.toDomain() }

    override suspend fun saveWorkflowOperation(operation: PendingWorkflowOperation) {
        workflowOutboxDao.enqueue(operation.toEntity())
    }

    override suspend fun removeWorkflowOperation(commandId: String) {
        workflowOutboxDao.delete(commandId)
    }

    override suspend fun clearAll() {
        database.withTransaction {
            outboxDao.clear()
            workflowOutboxDao.clear()
            missionDao.clearTimeline()
            missionDao.clearStops()
            missionDao.clearMissions()
            sessionDao.clear()
        }
    }
}

private fun DriverSessionEntity.toDomain(): DriverSession =
    DriverSession(
        accessToken = accessToken,
        expiresAtUtc = expiresAtUtc,
        userId = userId,
        email = email,
        fullName = fullName,
        organizationName = organizationName,
        driverId = driverId,
        roles = rolesCsv.split(",").filter { it.isNotBlank() },
    )

private fun DriverSession.toEntity(): DriverSessionEntity =
    DriverSessionEntity(
        accessToken = accessToken,
        expiresAtUtc = expiresAtUtc,
        userId = userId,
        email = email,
        fullName = fullName,
        organizationName = organizationName,
        driverId = driverId,
        rolesCsv = roles.joinToString(","),
    )

private fun DriverMissionAggregate.toDomain(pendingCount: Int): DriverMission =
    DriverMission(
        id = mission.id,
        reference = mission.reference,
        title = mission.title,
        status = enumValueOf(mission.status),
        scheduledStartUtc = mission.scheduledStartUtc,
        scheduledEndUtc = mission.scheduledEndUtc,
        vehicleRegistrationNumber = mission.vehicleRegistrationNumber,
        stopCount = maxOf(mission.stopCount, stops.size),
        simulatedDelayMinutes = mission.simulatedDelayMinutes,
        rowVersion = mission.rowVersion,
        syncState = if (pendingCount > 0) MissionSyncState.Pending else mission.syncState.toMissionSyncState(),
        stops = stops.sortedBy { it.sequence }.map {
            DriverMissionStop(
                id = it.id,
                sequence = it.sequence,
                name = it.name,
                address = it.address,
                plannedArrivalUtc = it.plannedArrivalUtc,
            )
        },
        timeline = timeline.sortedBy { it.occurredAtUtc }.map {
            DriverMissionTimelineEvent(
                id = it.id,
                eventType = it.eventType,
                description = it.description,
                occurredAtUtc = it.occurredAtUtc,
            )
        },
    )

private fun DriverMission.toEntity(): DriverMissionEntity =
    DriverMissionEntity(
        id = id,
        reference = reference,
        title = title,
        status = status.name,
        scheduledStartUtc = scheduledStartUtc,
        scheduledEndUtc = scheduledEndUtc,
        vehicleRegistrationNumber = vehicleRegistrationNumber,
        stopCount = stopCount,
        simulatedDelayMinutes = simulatedDelayMinutes,
        rowVersion = rowVersion,
        syncState = syncState.name,
    )

private fun DriverMissionStop.toEntity(missionId: String): DriverMissionStopEntity =
    DriverMissionStopEntity(
        missionId = missionId,
        sequence = sequence,
        id = id,
        name = name,
        address = address,
        plannedArrivalUtc = plannedArrivalUtc,
    )

private fun DriverMissionTimelineEvent.toEntity(missionId: String): DriverMissionTimelineEventEntity =
    DriverMissionTimelineEventEntity(
        id = id,
        missionId = missionId,
        eventType = eventType,
        description = description,
        occurredAtUtc = occurredAtUtc,
    )

private fun PendingMissionCommand.toEntity(): PendingMissionCommandEntity =
    PendingMissionCommandEntity(
        commandId = commandId,
        missionId = missionId,
        action = action.name,
        rowVersion = rowVersion,
        occurredAtUtc = occurredAtUtc,
    )

private fun PendingMissionCommandEntity.toDomain(): PendingMissionCommand =
    PendingMissionCommand(
        commandId = commandId,
        missionId = missionId,
        action = enumValueOf(action),
        rowVersion = rowVersion,
        occurredAtUtc = occurredAtUtc,
    )

private fun PendingWorkflowOperation.toEntity(): PendingWorkflowOperationEntity =
    PendingWorkflowOperationEntity(
        commandId = commandId,
        missionId = missionId,
        operationType = operationType.name,
        payloadJson = payloadJson,
        photosJson = driverJson.encodeToString(photos),
        createdAtUtc = createdAtUtc,
    )

private fun PendingWorkflowOperationEntity.toDomain(): PendingWorkflowOperation =
    PendingWorkflowOperation(
        commandId = commandId,
        missionId = missionId,
        operationType = enumValueOf(operationType),
        payloadJson = payloadJson,
        photos = driverJson.decodePendingPhotos(photosJson),
        createdAtUtc = createdAtUtc,
    )

private fun String.toMissionSyncState(): MissionSyncState =
    runCatching { enumValueOf<MissionSyncState>(this) }.getOrElse { MissionSyncState.Synced }
