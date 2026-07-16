package com.fleetops.driver

import android.app.Application
import androidx.work.WorkManager

class DriverApplication : Application() {
    lateinit var container: DriverAppContainer
        private set

    override fun onCreate() {
        super.onCreate()
        val database = DriverDatabase.build(this)
        container = DriverAppContainer(
            repository = OfflineFirstDriverRepository(
                localStore = RoomDriverLocalStore(database),
                remoteDataSource = RetrofitDriverRemoteDataSource(buildDriverApiService()),
                syncScheduler = WorkManagerDriverSyncScheduler(WorkManager.getInstance(this)),
            ),
        )
    }
}

data class DriverAppContainer(
    val repository: DriverRepository,
)
