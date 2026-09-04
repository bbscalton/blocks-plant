package com.blocksplant.app.ui.deliveries

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.Button
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
import com.blocksplant.app.data.api.DeliveryDto
import com.blocksplant.app.data.repo.AppRepository
import com.blocksplant.app.ui.components.ErrorText
import com.blocksplant.app.ui.components.KeyValueRow
import com.blocksplant.app.ui.components.SectionCard
import com.blocksplant.app.ui.components.SuccessText
import com.blocksplant.app.util.toUserMessage
import kotlinx.coroutines.launch

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DeliveriesScreen(repository: AppRepository) {
    var items by remember { mutableStateOf<List<DeliveryDto>>(emptyList()) }
    var refreshing by remember { mutableStateOf(false) }
    var error by remember { mutableStateOf<String?>(null) }
    var success by remember { mutableStateOf<String?>(null) }
    val scope = rememberCoroutineScope()

    fun refresh() {
        refreshing = true
        error = null
        scope.launch {
            try {
                items = repository.getDeliveries(includeDelivered = false)
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
                Text("Deliveries", style = MaterialTheme.typography.headlineSmall)
                ErrorText(error)
                SuccessText(success)
                Spacer(Modifier.height(8.dp))
                if (items.isEmpty() && error == null) {
                    Text("No pending deliveries.", style = MaterialTheme.typography.bodyMedium)
                }
            }
            items(items, key = { it.saleId }) { d ->
                SectionCard("Sale #${d.saleId} · ${d.customerName ?: "Customer"}") {
                    KeyValueRow("Address", d.deliveryAddress ?: "—")
                    if (!d.deliveryNotes.isNullOrBlank()) {
                        KeyValueRow("Notes", d.deliveryNotes)
                    }
                    KeyValueRow("Total", "$" + "%.2f".format(d.subtotal))
                    KeyValueRow("Status", d.deliveryStatus)
                    Button(
                        onClick = {
                            scope.launch {
                                try {
                                    repository.markDelivered(d.saleId)
                                    success = "Marked sale #${d.saleId} delivered."
                                    refresh()
                                } catch (t: Throwable) {
                                    error = t.toUserMessage()
                                }
                            }
                        },
                        modifier = Modifier.fillMaxWidth().padding(top = 8.dp)
                    ) { Text("Mark delivered") }
                }
            }
        }
    }
}
