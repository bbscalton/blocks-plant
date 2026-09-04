package com.blocksplant.app.ui.settings

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.blocksplant.app.BuildConfig
import com.blocksplant.app.data.repo.AppRepository
import com.blocksplant.app.ui.components.LabeledField
import com.blocksplant.app.ui.components.SuccessText

@Composable
fun SettingsScreen(
    repository: AppRepository,
    currentBaseUrl: String,
    onSaved: () -> Unit
) {
    var url by remember(currentBaseUrl) { mutableStateOf(currentBaseUrl) }
    var message by remember { mutableStateOf<String?>(null) }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(16.dp)
    ) {
        Text("Settings", style = MaterialTheme.typography.headlineSmall)
        Spacer(Modifier.height(12.dp))
        Text(
            "Emulator default: ${BuildConfig.DEFAULT_API_BASE_URL}\n" +
                "Physical device: use your PC LAN IP, e.g. http://192.168.1.10:5118",
            style = MaterialTheme.typography.bodySmall
        )
        Spacer(Modifier.height(16.dp))
        LabeledField("API base URL", url, onValueChange = { url = it })
        SuccessText(message)
        Spacer(Modifier.height(16.dp))
        Button(
            onClick = {
                if (url.isBlank()) {
                    message = null
                    return@Button
                }
                repository.setBaseUrl(url)
                message = "Saved."
                onSaved()
            },
            modifier = Modifier.fillMaxWidth()
        ) {
            Text("Save")
        }
        Spacer(Modifier.height(8.dp))
        Button(
            onClick = {
                url = BuildConfig.DEFAULT_API_BASE_URL
                repository.setBaseUrl(url)
                message = "Reset to emulator default."
                onSaved()
            },
            modifier = Modifier.fillMaxWidth()
        ) {
            Text("Use emulator default (10.0.2.2)")
        }
    }
}
