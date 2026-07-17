package com.fleetops.driver

import android.app.Application
import androidx.work.WorkManager

class DriverApplication : Application() {
    lateinit var container: DriverAppContainer
        private set

    override fun onCreate() {
        super.onCreate()
        val credentialStore = AndroidKeystoreDriverCredentialStore(this)
        val database = DriverDatabase.build(this, credentialStore)
        container = DriverAppContainer(
            repository = OfflineFirstDriverRepository(
                localStore = RoomDriverLocalStore(database, credentialStore),
                remoteDataSource = RetrofitDriverRemoteDataSource(buildDriverApiService()),
                syncScheduler = WorkManagerDriverSyncScheduler(WorkManager.getInstance(this)),
            ),
        )
    }
}

data class DriverAppContainer(
    val repository: DriverRepository,
)
