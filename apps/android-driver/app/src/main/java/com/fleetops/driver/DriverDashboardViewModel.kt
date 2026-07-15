package com.fleetops.driver

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

class DriverDashboardViewModel(
    private val repository: MissionRepository = DemoMissionRepository(),
) : ViewModel() {
    private val mutableState = MutableStateFlow(DriverDashboardState())
    val state: StateFlow<DriverDashboardState> = mutableState.asStateFlow()

    init {
        refresh()
    }

    fun refresh() {
        mutableState.value = mutableState.value.copy(isRefreshing = true)
        viewModelScope.launch {
            mutableState.value = DriverDashboardState(
                missions = repository.listAssignedMissions(),
                isRefreshing = false,
            )
        }
    }
}
