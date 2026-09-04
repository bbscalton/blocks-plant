package com.blocksplant.app.ui.materials

import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.blocksplant.app.data.api.RawMaterialDto
import com.blocksplant.app.data.repo.AppRepository
import com.blocksplant.app.ui.components.ErrorText
import com.blocksplant.app.ui.components.KeyValueRow
import com.blocksplant.app.ui.components.SectionCard
import com.blocksplant.app.util.toUserMessage
import kotlinx.coroutines.launch

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun MaterialsScreen(repository: AppRepository) {
    var items by remember { mutableStateOf<List<RawMaterialDto>>(emptyList()) }
    var refreshing by remember { mutableStateOf(false) }
    var error by remember { mutableStateOf<String?>(null) }
    val scope = rememberCoroutineScope()

    fun refresh() {
        refreshing = true
        error = null
        scope.launch {
            try {
                items = repository.getMaterials()
            } catch (t: Throwable) {
                error = t.toUserMessage()
            } finally {
                refreshing = false
            }
        }
    }

    LaunchedEffect(Unit) { refresh() }

    PullToRefreshBox(
        isRefreshing = refreshing,
        onRefresh = { refresh() },
        modifier = Modifier.fillMaxSize()
    ) {
        LazyColumn(modifier = Modifier.fillMaxSize().padding(16.dp)) {
            item {
                Text("Raw materials", style = MaterialTheme.typography.headlineSmall)
                Text(
                    "Stock levels used by production recipes.",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                ErrorText(error)
                Spacer(Modifier.height(8.dp))
            }
            items(items, key = { it.id }) { material ->
                SectionCard(material.name) {
                    KeyValueRow(
                        "On hand",
                        "${formatQty(material.quantityOnHand)} ${material.unit}"
                    )
                    KeyValueRow("Min stock", formatQty(material.minStock))
                    if (material.isLow) {
                        Text(
                            "Low stock",
                            color = MaterialTheme.colorScheme.error,
                            style = MaterialTheme.typography.labelLarge
                        )
                    }
                }
            }
        }
    }
}

private fun formatQty(value: Double): String {
    val s = "%.4f".format(value).trimEnd('0').trimEnd('.')
    return s.ifEmpty { "0" }
}
