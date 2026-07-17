package com.fleetops.driver

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.flatMapLatest
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch

@OptIn(ExperimentalCoroutinesApi::class)
class DriverAppViewModel(
    private val repository: DriverRepository,
) : ViewModel() {
    private val selectedMissionId = MutableStateFlow<String?>(null)
    private val isBusy = MutableStateFlow(false)
    private val errorMessage = MutableStateFlow<String?>(null)
    private val infoMessage = MutableStateFlow<String?>(null)

    private val selectedMissionFlow = selectedMissionId.flatMapLatest { missionId ->
        if (missionId == null) {
            flowOf(null)
        } else {
            repository.observeMission(missionId)
        }
    }

    private val snapshotFlow = combine(
        repository.observeSession(),
        repository.observeMissions(),
        selectedMissionFlow,
        repository.observeComplianceCampaignTasks(),
    ) { session, missions, selectedMission, complianceCampaignTasks ->
        DriverSnapshot(session, missions, selectedMission, complianceCampaignTasks)
    }

    private val signalFlow = combine(
        isBusy,
        errorMessage,
        infoMessage,
    ) { busy, error, info ->
        DriverSignals(busy, error, info)
    }

    val state: StateFlow<DriverAppUiState> = combine(snapshotFlow, signalFlow) { snapshot, signals ->
        DriverAppUiState(
            session = snapshot.session,
            missions = snapshot.missions,
            selectedMission = snapshot.selectedMission,
            complianceCampaignTasks = snapshot.complianceCampaignTasks,
            isBusy = signals.isBusy,
            errorMessage = signals.errorMessage,
            infoMessage = signals.infoMessage,
        )
    }.stateIn(
        scope = viewModelScope,
        started = SharingStarted.WhileSubscribed(5_000),
        initialValue = DriverAppUiState(),
    )

    fun login(email: String, password: String) = runTask {
        repository.login(email, password)
        infoMessage.value = "Driver session ready for offline work."
    }

    fun pair(code: String) = runTask {
        repository.pair(code)
        infoMessage.value = "Device paired and ready for offline work."
    }

    fun refresh() = runTask {
        repository.refresh()
        infoMessage.value = "Missions refreshed."
    }

    fun openMission(missionId: String) {
        selectedMissionId.value = missionId
    }

    fun closeMission() {
        selectedMissionId.value = null
    }

    fun executeAction(action: DriverMissionAction) = runTask {
        val missionId = selectedMissionId.value ?: return@runTask
        repository.queueMissionAction(missionId, action)
        infoMessage.value = "${action.name} queued for sync."
    }

    fun submitInspection(hasCriticalDefect: Boolean, evidence: List<CapturedEvidence> = emptyList()) = runTask {
        val missionId = selectedMissionId.value ?: return@runTask
        repository.queueInspection(missionId, hasCriticalDefect, evidence)
        infoMessage.value =
            if (hasCriticalDefect) {
                "Inspection queued with a critical defect."
            } else {
                "Inspection queued and ready to sync."
            }
    }

    fun submitDeliveryProof(stopId: String, recipientName: String, signatureName: String, evidence: List<CapturedEvidence>) = runTask {
        val missionId = selectedMissionId.value ?: return@runTask
        repository.queueDeliveryProof(missionId, stopId, recipientName, signatureName, evidence)
        infoMessage.value = "Delivery proof queued for sync."
    }

    fun submitComplianceCampaignTask(taskId: String, notes: String?) = runTask {
        repository.queueComplianceCampaignTask(taskId, notes)
        infoMessage.value = "Campaign inspection queued for sync."
    }

    fun signOut() = runTask {
        repository.signOut()
        selectedMissionId.value = null
        infoMessage.value = "Signed out from this device."
    }

    fun dismissMessages() {
        errorMessage.value = null
        infoMessage.value = null
    }

    fun reportCaptureError(message: String) {
        errorMessage.value = message
    }

    private fun runTask(action: suspend () -> Unit) {
        viewModelScope.launch {
            isBusy.value = true
            errorMessage.value = null
            try {
                action()
            } catch (throwable: Throwable) {
                errorMessage.value = throwable.toUserMessage()
            } finally {
                isBusy.value = false
            }
        }
    }
}

private data class DriverSnapshot(
    val session: DriverSession?,
    val missions: List<DriverMission>,
    val selectedMission: DriverMission?,
    val complianceCampaignTasks: List<DriverComplianceCampaignTask>,
)

private data class DriverSignals(
    val isBusy: Boolean,
    val errorMessage: String?,
    val infoMessage: String?,
)

class DriverAppViewModelFactory(
    private val repository: DriverRepository,
) : ViewModelProvider.Factory {
    @Suppress("UNCHECKED_CAST")
    override fun <T : ViewModel> create(modelClass: Class<T>): T =
        DriverAppViewModel(repository) as T
}
