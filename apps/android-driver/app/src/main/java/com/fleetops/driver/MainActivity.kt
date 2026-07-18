package com.fleetops.driver

import android.os.Bundle
import android.content.Intent
import android.net.Uri
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.isSystemInDarkTheme
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
import androidx.compose.material3.Checkbox
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
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.material3.lightColorScheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import androidx.lifecycle.viewmodel.compose.viewModel

private val DriverColorScheme = lightColorScheme(
    primary = Color(0xFF0B6B5D),
    onPrimary = Color.White,
    primaryContainer = Color(0xFFD7F2E9),
    onPrimaryContainer = Color(0xFF083D36),
    secondary = Color(0xFF315E6B),
    background = Color(0xFFF4F7F8),
    surface = Color.White,
    surfaceVariant = Color(0xFFEAF0F2),
)

private val DriverDarkColorScheme = darkColorScheme(
    primary = Color(0xFF8CE0C7),
    onPrimary = Color(0xFF00382F),
    primaryContainer = Color(0xFF075144),
    onPrimaryContainer = Color(0xFFD7F2E9),
    secondary = Color(0xFFB4D7E1),
)

private val LightCardContent = Color(0xFF0F172A)

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

    MaterialTheme(colorScheme = if (isSystemInDarkTheme()) DriverDarkColorScheme else DriverColorScheme) {
        Scaffold(
            containerColor = MaterialTheme.colorScheme.background,
            topBar = {
                TopAppBar(
                    title = {
                        Column {
                            Text(
                                "ORKYSTRA",
                                style = MaterialTheme.typography.labelSmall,
                                color = MaterialTheme.colorScheme.primary,
                                fontWeight = FontWeight.Bold,
                            )
                            Text(if (state.isLoggedIn) "Driver workspace" else "Secure sign in")
                        }
                    },
                    actions = {
                        if (state.isLoggedIn) {
                            TextButton(onClick = viewModel::signOut) {
                                Text("Sign out")
                            }
                        }
                    },
                    colors = TopAppBarDefaults.topAppBarColors(
                        containerColor = MaterialTheme.colorScheme.surface,
                        titleContentColor = MaterialTheme.colorScheme.onSurface,
                        actionIconContentColor = MaterialTheme.colorScheme.primary,
                    ),
                )
            },
            snackbarHost = { SnackbarHost(snackbarHostState) },
        ) { padding ->
            when {
                !state.isLoggedIn -> LoginScreen(
                    isBusy = state.isBusy,
                    onLogin = viewModel::login,
                    onPair = viewModel::pair,
                    modifier = Modifier.padding(padding),
                )
                state.selectedMission == null -> MissionListScreen(
                    state = state,
                    onRefresh = viewModel::refresh,
                    onMissionClick = viewModel::openMission,
                    onSubmitCampaignTask = viewModel::submitComplianceCampaignTask,
                    modifier = Modifier.padding(padding),
                )
                else -> MissionDetailScreen(
                    mission = state.selectedMission,
                    isBusy = state.isBusy,
                    onBack = viewModel::closeMission,
                    onAction = viewModel::executeAction,
                    onSubmitInspection = viewModel::submitInspection,
                    onSubmitDeliveryProof = viewModel::submitDeliveryProof,
                    onCaptureError = viewModel::reportCaptureError,
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
    onPair: (String) -> Unit,
    modifier: Modifier = Modifier,
) {
    var email by rememberSaveable {
        mutableStateOf(if (BuildConfig.DEBUG) "driver@northwind.local" else "")
    }
    var password by rememberSaveable {
        mutableStateOf(if (BuildConfig.DEBUG) "Driver123!" else "")
    }
    var pairingCode by rememberSaveable { mutableStateOf("") }

    Surface(
        modifier = modifier
            .fillMaxSize()
            .background(
                Brush.verticalGradient(
                    colors = listOf(Color(0xFFF4F7F8), Color(0xFFE3F2EE)),
                ),
            ),
    ) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(24.dp),
            verticalArrangement = Arrangement.Center,
        ) {
            Text("Your workday, ready offline", style = MaterialTheme.typography.headlineMedium, fontWeight = FontWeight.Bold)
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
                visualTransformation = PasswordVisualTransformation(),
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
            HorizontalDivider(modifier = Modifier.padding(vertical = 20.dp))
            Text("New device? Enter the six-digit code from your administrator.", style = MaterialTheme.typography.bodyMedium)
            OutlinedTextField(value = pairingCode, onValueChange = { pairingCode = it.filter(Char::isDigit).take(6) }, label = { Text("Pairing code") }, modifier = Modifier.fillMaxWidth().padding(top = 12.dp), enabled = !isBusy)
            OutlinedButton(onClick = { onPair(pairingCode) }, enabled = !isBusy && pairingCode.length == 6, modifier = Modifier.fillMaxWidth().padding(top = 12.dp)) { Text("Pair this device") }
        }
    }
}

@Composable
private fun MissionListScreen(
    state: DriverAppUiState,
    onRefresh: () -> Unit,
    onMissionClick: (String) -> Unit,
    onSubmitCampaignTask: (String, String?) -> Unit,
    modifier: Modifier = Modifier,
) {
    val nextMission = state.missions.firstOrNull { it.availableActions().isNotEmpty() }
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
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Text(
                text = "Assigned missions",
                style = MaterialTheme.typography.headlineSmall,
                fontWeight = FontWeight.Bold,
            )
            Text(
                text = "Open the next mission, complete each action, and keep working when coverage drops.",
                style = MaterialTheme.typography.bodyMedium,
                modifier = Modifier.padding(top = 4.dp, bottom = 12.dp),
            )
        }

        if (state.complianceCampaignTasks.isNotEmpty()) {
            item {
                Card(colors = CardDefaults.cardColors(containerColor = Color(0xFFFFFBEB), contentColor = LightCardContent)) {
                    Column(modifier = Modifier.padding(18.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        Text("Inspection campaign", style = MaterialTheme.typography.titleMedium)
                        Text("These assigned checks remain on this device and sync when coverage returns.", style = MaterialTheme.typography.bodySmall)
                        state.complianceCampaignTasks.forEach { task ->
                            CampaignTaskCard(task, state.isBusy, onSubmitCampaignTask)
                        }
                    }
                }
            }
        }

        if (nextMission != null) {
            item {
                Card(
                    modifier = Modifier.fillMaxWidth().clickable { onMissionClick(nextMission.id) },
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.primaryContainer),
                ) {
                    Column(modifier = Modifier.padding(18.dp)) {
                        Text("NEXT STEP", style = MaterialTheme.typography.labelLarge, color = MaterialTheme.colorScheme.primary)
                        Text(nextMission.nextActionLabel(), style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold, modifier = Modifier.padding(top = 4.dp))
                        Text("${nextMission.reference} · ${nextMission.destination ?: "Route stop"}", modifier = Modifier.padding(top = 6.dp))
                        Text("Route progress: ${nextMission.routeProgressLabel()} · ${nextMission.syncState.label()}", style = MaterialTheme.typography.bodySmall, modifier = Modifier.padding(top = 8.dp))
                    }
                }
            }
        }

        item {
            val pendingCount = state.missions.count { it.syncState != MissionSyncState.Synced }
            Card(
                colors = CardDefaults.cardColors(
                    containerColor = if (pendingCount == 0) Color(0xFFE8F5F0) else Color(0xFFFFF7E0),
                    contentColor = LightCardContent,
                ),
            ) {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(horizontal = 16.dp, vertical = 13.dp),
                    horizontalArrangement = Arrangement.SpaceBetween,
                ) {
                    Column {
                        Text(
                            if (pendingCount == 0) "Ready for offline work" else "$pendingCount item(s) waiting to sync",
                            fontWeight = FontWeight.SemiBold,
                        )
                        Text(
                            if (pendingCount == 0) "All visible missions are stored on this device." else "FleetOps will retry automatically.",
                            style = MaterialTheme.typography.bodySmall,
                        )
                    }
                    Text(if (pendingCount == 0) "SYNCED" else "PENDING", style = MaterialTheme.typography.labelSmall)
                }
            }
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
                colors = CardDefaults.cardColors(containerColor = Color(0xFFF8FAFC), contentColor = LightCardContent),
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
                    Text(
                        "Open mission  →",
                        color = MaterialTheme.colorScheme.primary,
                        fontWeight = FontWeight.SemiBold,
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
private fun CampaignTaskCard(
    task: DriverComplianceCampaignTask,
    isBusy: Boolean,
    onSubmit: (String, String?) -> Unit,
) {
    var notes by rememberSaveable(task.id) { mutableStateOf("") }
    Text("${task.campaignName} · ${task.vehicleRegistration}", fontWeight = FontWeight.SemiBold)
    Text("Template: ${task.templateCode} · closes ${task.closesAtUtc.toFriendlyDateTime()}", style = MaterialTheme.typography.bodySmall)
    if (task.status.equals("Submitted", ignoreCase = true)) {
        Text("Submitted", color = Color(0xFF166534), style = MaterialTheme.typography.labelLarge)
    } else {
        OutlinedTextField(value = notes, onValueChange = { notes = it }, label = { Text("Inspection notes (optional)") }, modifier = Modifier.fillMaxWidth(), enabled = !isBusy && task.pendingCommandId == null)
        Button(onClick = { onSubmit(task.id, notes) }, enabled = !isBusy && task.pendingCommandId == null, modifier = Modifier.fillMaxWidth()) {
            Text(if (task.pendingCommandId == null) "Queue inspection completion" else "Queued for sync")
        }
    }
}

@Composable
private fun MissionDetailScreen(
    mission: DriverMission?,
    isBusy: Boolean,
    onBack: () -> Unit,
    onAction: (DriverMissionAction) -> Unit,
    onSubmitInspection: (Boolean, List<CapturedEvidence>) -> Unit,
    onSubmitDeliveryProof: (String, String, String, List<CapturedEvidence>) -> Unit,
    onCaptureError: (String) -> Unit,
    modifier: Modifier = Modifier,
) {
    val context = LocalContext.current
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
            Text(mission.reference, style = MaterialTheme.typography.labelLarge, color = MaterialTheme.colorScheme.onSurfaceVariant)
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
            val nextAction = mission.availableActions().firstOrNull()
            Card(colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.primaryContainer)) {
                Column(modifier = Modifier.padding(18.dp)) {
                    Text("Next action", style = MaterialTheme.typography.labelLarge, color = MaterialTheme.colorScheme.primary)
                    Text(
                        when (nextAction) {
                            DriverMissionAction.Start -> "Complete the pre-departure check, then start the mission."
                            DriverMissionAction.Arrive -> "Confirm arrival when the vehicle reaches the active stop."
                            DriverMissionAction.Complete -> "Capture the delivery proof, then complete the mission."
                            null -> "Review the mission record; there is no pending driver action."
                        },
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.SemiBold,
                        modifier = Modifier.padding(top = 4.dp),
                    )
                }
            }
        }

        item {
            Card(colors = CardDefaults.cardColors(containerColor = Color(0xFFF8FAFC), contentColor = LightCardContent)) {
                Column(modifier = Modifier.padding(18.dp)) {
                    Text("Stops", style = MaterialTheme.typography.titleMedium)
                    mission.stops.sortedBy { it.sequence }.forEachIndexed { index, stop ->
                        Text("${stop.sequence}. ${stop.name}", modifier = Modifier.padding(top = if (index == 0) 12.dp else 10.dp))
                        Text(stop.address, style = MaterialTheme.typography.bodySmall)
                        Text(stop.plannedArrivalUtc.toFriendlyDateTime(), style = MaterialTheme.typography.bodySmall, color = Color(0xFF64748B))
                        TextButton(onClick = {
                            context.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse("geo:0,0?q=${Uri.encode(stop.address)}")))
                        }) {
                            Text("Open navigation")
                        }
                        if (index < mission.stops.lastIndex) {
                            HorizontalDivider(modifier = Modifier.padding(top = 10.dp))
                        }
                    }
                }
            }
        }

        item {
            Card(colors = CardDefaults.cardColors(containerColor = Color(0xFFFFFBEB), contentColor = LightCardContent)) {
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
                onCleanInspection = { evidence -> onSubmitInspection(false, evidence) },
                onCriticalInspection = { evidence -> onSubmitInspection(true, evidence) },
                onCaptureError = onCaptureError,
            )
        }

        item {
            DeliveryProofPanel(
                mission = mission,
                isBusy = isBusy,
                onSubmitDeliveryProof = onSubmitDeliveryProof,
                onCaptureError = onCaptureError,
            )
        }
    }
}

@Composable
private fun InspectionPanel(
    isBusy: Boolean,
    onCleanInspection: (List<CapturedEvidence>) -> Unit,
    onCriticalInspection: (List<CapturedEvidence>) -> Unit,
    onCaptureError: (String) -> Unit,
) {
    var showEvidenceChooser by remember { mutableStateOf(false) }
    val evidence = remember { mutableStateListOf<CapturedEvidence>() }
    Card(colors = CardDefaults.cardColors(containerColor = Color(0xFFEFF6FF), contentColor = LightCardContent)) {
        Column(modifier = Modifier.padding(18.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Text("Pre-departure inspection", style = MaterialTheme.typography.titleMedium)
            Text(
                "Record vehicle readiness before departure. The result stays queued safely until the network is available.",
                style = MaterialTheme.typography.bodySmall,
            )
            OutlinedButton(onClick = { showEvidenceChooser = true }, enabled = !isBusy, modifier = Modifier.fillMaxWidth()) {
                Text(if (evidence.isEmpty()) "Add inspection photo (optional)" else "Inspection photo ready")
            }
            Button(onClick = { onCleanInspection(evidence.toList()) }, enabled = !isBusy, modifier = Modifier.fillMaxWidth()) {
                Text("Queue ready-to-drive inspection")
            }
            OutlinedButton(onClick = { onCriticalInspection(evidence.toList()) }, enabled = !isBusy, modifier = Modifier.fillMaxWidth()) {
                Text("Queue blocking defect")
            }
        }
    }
    EvidenceSourceChooser(
        visible = showEvidenceChooser,
        title = "Inspection photo",
        caption = "Inspection evidence",
        onDismiss = { showEvidenceChooser = false },
        onCaptured = { capture -> evidence.clear(); evidence += capture; showEvidenceChooser = false },
        onError = onCaptureError,
    )
}

@Composable
private fun DeliveryProofPanel(
    mission: DriverMission,
    isBusy: Boolean,
    onSubmitDeliveryProof: (String, String, String, List<CapturedEvidence>) -> Unit,
    onCaptureError: (String) -> Unit,
) {
    var recipientName by rememberSaveable(mission.id) {
        mutableStateOf(if (BuildConfig.DEBUG) "Taylor Receiver" else "")
    }
    var signatureName by rememberSaveable(mission.id) {
        mutableStateOf(if (BuildConfig.DEBUG) "Taylor Receiver" else "")
    }
    val targetStop = mission.stops.maxByOrNull { it.sequence }
    val evidence = remember(mission.id) { mutableStateListOf<CapturedEvidence>() }
    var showPhotoChooser by rememberSaveable(mission.id) { mutableStateOf(false) }
    var showSignatureCapture by rememberSaveable(mission.id) { mutableStateOf(false) }
    var recipientConsented by rememberSaveable(mission.id) { mutableStateOf(false) }

    Card(colors = CardDefaults.cardColors(containerColor = Color(0xFFECFDF5), contentColor = LightCardContent)) {
        Column(modifier = Modifier.padding(18.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Text("Delivery proof", style = MaterialTheme.typography.titleMedium)
            Text(
                "Capture recipient details offline. Media uploads resume from the stored offset until the server finalizes the proof.",
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
            OutlinedButton(onClick = { showPhotoChooser = true }, enabled = !isBusy && targetStop != null, modifier = Modifier.fillMaxWidth()) {
                Text(if (evidence.any { it.caption == DELIVERY_PHOTO_CAPTION }) "Delivery photo ready" else "Capture delivery photo")
            }
            OutlinedButton(onClick = { showSignatureCapture = true }, enabled = !isBusy && targetStop != null, modifier = Modifier.fillMaxWidth()) {
                Text(if (evidence.any { it.caption == SIGNATURE_CAPTION }) "Signature ready" else "Capture handwritten signature")
            }
            Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = androidx.compose.ui.Alignment.CenterVertically) {
                Checkbox(checked = recipientConsented, onCheckedChange = { recipientConsented = it }, enabled = !isBusy && targetStop != null)
                Text("Recipient consents to capture and store this delivery proof.", style = MaterialTheme.typography.bodySmall)
            }
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
                    onSubmitDeliveryProof(stopId, recipientName, signatureName, evidence.toList())
                },
                enabled = !isBusy && targetStop != null && recipientName.isNotBlank() && signatureName.isNotBlank() && recipientConsented && evidence.any { it.caption == DELIVERY_PHOTO_CAPTION } && evidence.any { it.caption == SIGNATURE_CAPTION },
                modifier = Modifier
                    .fillMaxWidth()
                    .navigationBarsPadding(),
            ) {
                Text("Queue delivery proof")
            }
        }
    }
    EvidenceSourceChooser(
        visible = showPhotoChooser,
        title = "Delivery photo",
        caption = DELIVERY_PHOTO_CAPTION,
        onDismiss = { showPhotoChooser = false },
        onCaptured = { capture -> evidence.removeAll { it.caption == DELIVERY_PHOTO_CAPTION }; evidence += capture; showPhotoChooser = false },
        onError = onCaptureError,
    )
    SignatureCaptureDialog(
        visible = showSignatureCapture,
        onDismiss = { showSignatureCapture = false },
        onCaptured = { capture -> evidence.removeAll { it.caption == SIGNATURE_CAPTION }; evidence += capture },
        onError = onCaptureError,
    )
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
