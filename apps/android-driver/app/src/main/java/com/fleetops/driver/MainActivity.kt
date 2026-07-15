package com.fleetops.driver

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.lifecycle.viewmodel.compose.viewModel

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent { ZynroDriveApp() }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun ZynroDriveApp() {
    val viewModel: DriverDashboardViewModel = viewModel()
    val state by viewModel.state.collectAsState()

    MaterialTheme {
        Scaffold(topBar = { TopAppBar(title = { Text("Zynro Drive") }) }) { padding ->
            DriverDashboard(
                state = state,
                onRefresh = viewModel::refresh,
                modifier = Modifier.padding(padding),
            )
        }
    }
}

@Composable
private fun DriverDashboard(
    state: DriverDashboardState,
    onRefresh: () -> Unit,
    modifier: Modifier = Modifier,
) {
    if (state.isEmpty) {
        Column(
            modifier = modifier.fillMaxSize().padding(24.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center,
        ) {
            Text("Aucune mission affectee", style = MaterialTheme.typography.headlineSmall)
            Text("Les donnees locales de demonstration restent disponibles hors ligne.")
            Button(onClick = onRefresh, modifier = Modifier.padding(top = 24.dp)) {
                Text(if (state.isRefreshing) "Actualisation..." else "Actualiser")
            }
        }
        return
    }

    LazyColumn(
        modifier = modifier.fillMaxSize().padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        item {
            Text("Missions affectees", style = MaterialTheme.typography.headlineSmall)
        }
        items(state.missions, key = { it.id }) { mission ->
            Card(colors = CardDefaults.cardColors()) {
                Column(modifier = Modifier.padding(16.dp)) {
                    Text(mission.reference, style = MaterialTheme.typography.titleMedium)
                    Text(mission.destination)
                    Text(mission.status, style = MaterialTheme.typography.bodySmall)
                }
            }
        }
        item {
            Button(onClick = onRefresh) {
                Text(if (state.isRefreshing) "Actualisation..." else "Actualiser")
            }
        }
    }
}
