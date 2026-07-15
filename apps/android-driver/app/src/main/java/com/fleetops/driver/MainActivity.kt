package com.fleetops.driver

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent { FleetOpsDriverApp() }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun FleetOpsDriverApp() {
    MaterialTheme {
        Scaffold(topBar = { TopAppBar(title = { Text("FleetOps Driver") }) }) { padding ->
            Column(
                modifier = Modifier.fillMaxSize().padding(padding).padding(24.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.Center,
            ) {
                Text("Aucune mission affectée", style = MaterialTheme.typography.headlineSmall)
                Text("Le mode offline et les missions seront ajoutés au sprint 05.")
                Button(onClick = {}, modifier = Modifier.padding(top = 24.dp)) {
                    Text("Actualiser")
                }
            }
        }
    }
}
