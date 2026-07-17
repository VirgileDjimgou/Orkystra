package com.fleetops.driver

import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.work.Configuration
import androidx.work.WorkManager
import androidx.work.testing.SynchronousExecutor
import androidx.work.testing.WorkManagerTestInitHelper
import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class DriverSyncSchedulerInstrumentationTest {
    private lateinit var workManager: WorkManager
    private lateinit var scheduler: WorkManagerDriverSyncScheduler

    @Before
    fun setUp() {
        val context = ApplicationProvider.getApplicationContext<android.content.Context>()
        val configuration = Configuration.Builder()
            .setMinimumLoggingLevel(android.util.Log.DEBUG)
            .setExecutor(SynchronousExecutor())
            .build()

        WorkManagerTestInitHelper.initializeTestWorkManager(context, configuration)
        workManager = WorkManager.getInstance(context)
        scheduler = WorkManagerDriverSyncScheduler(workManager)
    }

    @Test
    fun ensureScheduledKeepsSingleUniquePeriodicWork() {
        scheduler.ensureScheduled()
        scheduler.ensureScheduled()

        val workInfos = workManager.getWorkInfosForUniqueWork(DRIVER_SYNC_WORK_NAME).get()

        assertEquals(1, workInfos.size)
        assertEquals(androidx.work.WorkInfo.State.ENQUEUED, workInfos.single().state)
    }
}
