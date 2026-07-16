package com.fleetops.driver

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.AssistChip
import androidx.compose.material3.AssistChipDefaults
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.lifecycle.viewmodel.compose.viewModel

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            val app = application as DriverApplication
            val viewModel: DriverAppViewModel = viewModel(
                factory = DriverAppViewModelFactory(app.container.repository),
            )
            FleetOpsDriverApp(viewModel)
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun FleetOpsDriverApp(viewModel: DriverAppViewModel) {
    val state by viewModel.state.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }

    LaunchedEffect(state.errorMessage, state.infoMessage) {
        val message = state.errorMessage ?: state.infoMessage
        if (!message.isNullOrBlank()) {
            snackbarHostState.showSnackbar(message)
            viewModel.dismissMessages()
        }
    }

    MaterialTheme {
        Scaffold(
            topBar = {
                TopAppBar(
                    title = { Text(if (state.isLoggedIn) "FleetOps Driver" else "Driver sign in") },
                    actions = {
                        if (state.isLoggedIn) {
                            TextButton(onClick = viewModel::signOut) {
                                Text("Sign out")
                            }
                        }
                    },
                )
            },
            snackbarHost = { SnackbarHost(snackbarHostState) },
        ) { padding ->
            when {
                !state.isLoggedIn -> LoginScreen(
                    isBusy = state.isBusy,
                    onLogin = viewModel::login,
                    modifier = Modifier.padding(padding),
                )
                state.selectedMission == null -> MissionListScreen(
                    state = state,
                    onRefresh = viewModel::refresh,
                    onMissionClick = viewModel::openMission,
                    modifier = Modifier.padding(padding),
                )
                else -> MissionDetailScreen(
                    mission = state.selectedMission,
                    isBusy = state.isBusy,
                    onBack = viewModel::closeMission,
                    onAction = viewModel::executeAction,
                    onSubmitInspection = viewModel::submitInspection,
                    onSubmitDeliveryProof = viewModel::submitDeliveryProof,
                    modifier = Modifier.padding(padding),
                )
            }
        }
    }
}

@Composable
private fun LoginScreen(
    isBusy: Boolean,
    onLogin: (String, String) -> Unit,
    modifier: Modifier = Modifier,
) {
    var email by rememberSaveable { mutableStateOf("driver@northwind.local") }
    var password by rememberSaveable { mutableStateOf("Driver123!") }

    Surface(
        modifier = modifier
            .fillMaxSize()
            .background(
                Brush.verticalGradient(
                    colors = listOf(Color(0xFFF1F5F9), Color(0xFFDCEAF3)),
                ),
            ),
    ) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(24.dp),
            verticalArrangement = Arrangement.Center,
        ) {
            Text("Offline-first driver cockpit", style = MaterialTheme.typography.headlineMedium, fontWeight = FontWeight.Bold)
            Text(
                "Sign in once, cache your missions on device, and sync route events when connectivity comes back.",
                style = MaterialTheme.typography.bodyLarge,
                modifier = Modifier.padding(top = 12.dp, bottom = 24.dp),
            )
            OutlinedTextField(
                value = email,
                onValueChange = { email = it },
                label = { Text("Email") },
                modifier = Modifier.fillMaxWidth(),
                enabled = !isBusy,
            )
            OutlinedTextField(
                value = password,
                onValueChange = { password = it },
                label = { Text("Password") },
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(top = 12.dp),
                enabled = !isBusy,
            )
            Button(
                onClick = { onLogin(email, password) },
                enabled = !isBusy,
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(top = 20.dp),
            ) {
                if (isBusy) {
                    CircularProgressIndicator(strokeWidth = 2.dp, modifier = Modifier.padding(end = 12.dp))
                }
                Text("Sign in")
            }
        }
    }
}

@Composable
private fun MissionListScreen(
    state: DriverAppUiState,
    onRefresh: () -> Unit,
    onMissionClick: (String) -> Unit,
    modifier: Modifier = Modifier,
) {
    LazyColumn(
        modifier = modifier
            .fillMaxSize()
            .padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        item {
            Text(
                text = state.session?.organizationName ?: "FleetOps",
                style = MaterialTheme.typography.labelLarge,
                color = Color(0xFF475569),
            )
            Text(
                text = "Assigned missions",
                style = MaterialTheme.typography.headlineSmall,
                fontWeight = FontWeight.Bold,
            )
            Text(
                text = "Routes stay readable offline and pending actions sync in the background.",
                style = MaterialTheme.typography.bodyMedium,
                modifier = Modifier.padding(top = 4.dp, bottom = 12.dp),
            )
        }

        if (state.missions.isEmpty()) {
            item {
                Card {
                    Column(modifier = Modifier.padding(20.dp)) {
                        Text("No assigned mission yet", style = MaterialTheme.typography.titleMedium)
                        Text(
                            "Refresh after dispatch assigns the first route to this driver account.",
                            modifier = Modifier.padding(top = 8.dp),
                        )
                    }
                }
            }
        }

        items(state.missions, key = { it.id }) { mission ->
            Card(
                modifier = Modifier
                    .fillMaxWidth()
                    .clickable { onMissionClick(mission.id) },
                colors = CardDefaults.cardColors(containerColor = Color(0xFFF8FAFC)),
            ) {
                Column(modifier = Modifier.padding(18.dp)) {
                    Text(mission.reference, style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.SemiBold)
                    Text(mission.title, modifier = Modifier.padding(top = 4.dp))
                    Text(
                        mission.destination ?: "Final stop unavailable",
                        style = MaterialTheme.typography.bodySmall,
                        modifier = Modifier.padding(top = 4.dp),
                    )
                    Row(
                        modifier = Modifier.padding(top = 12.dp),
                        horizontalArrangement = Arrangement.spacedBy(8.dp),
                    ) {
                        StatusChip(mission.status.label())
                        StatusChip(mission.syncState.label(), muted = mission.syncState == MissionSyncState.Synced)
                    }
                    Text(
                        "Starts ${mission.scheduledStartUtc.toFriendlyDateTime()}",
                        style = MaterialTheme.typography.bodySmall,
                        modifier = Modifier.padding(top = 10.dp),
                    )
                }
            }
        }

        item {
            Button(
                onClick = onRefresh,
                enabled = !state.isBusy,
                modifier = Modifier
                    .fillMaxWidth()
                    .navigationBarsPadding(),
            ) {
                Text(if (state.isBusy) "Refreshing..." else "Refresh from server")
            }
        }
    }
}

@Composable
private fun MissionDetailScreen(
    mission: DriverMission?,
    isBusy: Boolean,
    onBack: () -> Unit,
    onAction: (DriverMissionAction) -> Unit,
    onSubmitInspection: (Boolean) -> Unit,
    onSubmitDeliveryProof: (String, String, String) -> Unit,
    modifier: Modifier = Modifier,
) {
    if (mission == null) {
        Surface(modifier = modifier.fillMaxSize()) { }
        return
    }

    LazyColumn(
        modifier = modifier
            .fillMaxSize()
            .padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp),
    ) {
        item {
            TextButton(onClick = onBack) {
                Text("Back to mission list")
            }
            Text(mission.reference, style = MaterialTheme.typography.labelLarge, color = Color(0xFF64748B))
            Text(mission.title, style = MaterialTheme.typography.headlineSmall, fontWeight = FontWeight.Bold)
            Row(
                modifier = Modifier.padding(top = 10.dp),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                StatusChip(mission.status.label())
                StatusChip(mission.syncState.label(), muted = mission.syncState == MissionSyncState.Synced)
            }
            Text("Vehicle: ${mission.vehicleRegistrationNumber ?: "Unassigned"}", modifier = Modifier.padding(top = 12.dp))
            Text("Window: ${mission.scheduledStartUtc.toFriendlyDateTime()} to ${mission.scheduledEndUtc.toFriendlyDateTime()}")
        }

        item {
            Card(colors = CardDefaults.cardColors(containerColor = Color(0xFFF8FAFC))) {
                Column(modifier = Modifier.padding(18.dp)) {
                    Text("Stops", style = MaterialTheme.typography.titleMedium)
                    mission.stops.sortedBy { it.sequence }.forEachIndexed { index, stop ->
                        Text("${stop.sequence}. ${stop.name}", modifier = Modifier.padding(top = if (index == 0) 12.dp else 10.dp))
                        Text(stop.address, style = MaterialTheme.typography.bodySmall)
                        Text(stop.plannedArrivalUtc.toFriendlyDateTime(), style = MaterialTheme.typography.bodySmall, color = Color(0xFF64748B))
                        if (index < mission.stops.lastIndex) {
                            HorizontalDivider(modifier = Modifier.padding(top = 10.dp))
                        }
                    }
                }
            }
        }

        item {
            Card(colors = CardDefaults.cardColors(containerColor = Color(0xFFFFFBEB))) {
                Column(modifier = Modifier.padding(18.dp)) {
                    Text("Recent timeline", style = MaterialTheme.typography.titleMedium)
                    mission.timeline.takeLast(5).reversed().forEach { event ->
                        Text(event.description, modifier = Modifier.padding(top = 10.dp))
                        Text(event.occurredAtUtc.toFriendlyDateTime(), style = MaterialTheme.typography.bodySmall, color = Color(0xFF92400E))
                    }
                }
            }
        }

        item {
            Column(
                modifier = Modifier.navigationBarsPadding(),
                verticalArrangement = Arrangement.spacedBy(10.dp),
            ) {
                    mission.availableActions().forEach { action ->
                    Button(
                        onClick = { onAction(action) },
                        enabled = !isBusy,
                        modifier = Modifier.fillMaxWidth(),
                    ) {
                        Text(
                            when (action) {
                                DriverMissionAction.Start -> "Start mission"
                                DriverMissionAction.Arrive -> "Confirm arrival"
                                DriverMissionAction.Complete -> "Complete mission"
                            },
                        )
                    }
                }
                if (mission.availableActions().isEmpty()) {
                    OutlinedButton(onClick = onBack, modifier = Modifier.fillMaxWidth()) {
                        Text("Mission has no remaining driver action")
                    }
                }
            }
        }

        item {
            InspectionPanel(
                isBusy = isBusy,
                onCleanInspection = { onSubmitInspection(false) },
                onCriticalInspection = { onSubmitInspection(true) },
            )
        }

        item {
            DeliveryProofPanel(
                mission = mission,
                isBusy = isBusy,
                onSubmitDeliveryProof = onSubmitDeliveryProof,
            )
        }
    }
}

@Composable
private fun InspectionPanel(
    isBusy: Boolean,
    onCleanInspection: () -> Unit,
    onCriticalInspection: () -> Unit,
) {
    Card(colors = CardDefaults.cardColors(containerColor = Color(0xFFEFF6FF))) {
        Column(modifier = Modifier.padding(18.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Text("Sprint 06 demo inspection", style = MaterialTheme.typography.titleMedium)
            Text(
                "Queue a clean or blocking pre-departure inspection locally. Sync uploads the evidence first, then sends the inspection before any mission start command.",
                style = MaterialTheme.typography.bodySmall,
            )
            Button(onClick = onCleanInspection, enabled = !isBusy, modifier = Modifier.fillMaxWidth()) {
                Text("Queue ready-to-drive inspection")
            }
            OutlinedButton(onClick = onCriticalInspection, enabled = !isBusy, modifier = Modifier.fillMaxWidth()) {
                Text("Queue blocking defect")
            }
        }
    }
}

@Composable
private fun DeliveryProofPanel(
    mission: DriverMission,
    isBusy: Boolean,
    onSubmitDeliveryProof: (String, String, String) -> Unit,
) {
    var recipientName by rememberSaveable(mission.id) { mutableStateOf("Taylor Receiver") }
    var signatureName by rememberSaveable(mission.id) { mutableStateOf("Taylor Receiver") }
    val targetStop = mission.stops.maxByOrNull { it.sequence }

    Card(colors = CardDefaults.cardColors(containerColor = Color(0xFFECFDF5))) {
        Column(modifier = Modifier.padding(18.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Text("Delivery proof", style = MaterialTheme.typography.titleMedium)
            Text(
                "Capture a simple offline recipient proof. The demo media upload resumes from the stored offset until the server finalizes the asset.",
                style = MaterialTheme.typography.bodySmall,
            )
            Text(
                "Target stop: ${targetStop?.name ?: "No delivery stop available"}",
                style = MaterialTheme.typography.bodySmall,
                color = Color(0xFF166534),
            )
            OutlinedTextField(
                value = recipientName,
                onValueChange = { recipientName = it },
                label = { Text("Recipient name") },
                modifier = Modifier.fillMaxWidth(),
                enabled = !isBusy && targetStop != null,
            )
            OutlinedTextField(
                value = signatureName,
                onValueChange = { signatureName = it },
                label = { Text("Signature name") },
                modifier = Modifier.fillMaxWidth(),
                enabled = !isBusy && targetStop != null,
            )
            Button(
                onClick = {
                    val stopId = targetStop?.id ?: return@Button
                    onSubmitDeliveryProof(stopId, recipientName, signatureName)
                },
                enabled = !isBusy && targetStop != null && recipientName.isNotBlank() && signatureName.isNotBlank(),
                modifier = Modifier
                    .fillMaxWidth()
                    .navigationBarsPadding(),
            ) {
                Text("Queue delivery proof")
            }
        }
    }
}

@Composable
private fun StatusChip(text: String, muted: Boolean = false) {
    AssistChip(
        onClick = {},
        label = { Text(text) },
        colors = AssistChipDefaults.assistChipColors(
            containerColor = if (muted) Color(0xFFE2E8F0) else Color(0xFFDBEAFE),
            labelColor = if (muted) Color(0xFF475569) else Color(0xFF1D4ED8),
        ),
    )
}
