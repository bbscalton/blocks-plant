package com.blocksplant.app.ui.dashboard

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
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
import com.blocksplant.app.data.api.DashboardDto
import com.blocksplant.app.data.repo.AppRepository
import com.blocksplant.app.ui.components.ErrorText
import com.blocksplant.app.ui.components.KeyValueRow
import com.blocksplant.app.ui.components.SectionCard
import com.blocksplant.app.util.toUserMessage
import kotlinx.coroutines.launch

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DashboardScreen(repository: AppRepository) {
    var dashboard by remember { mutableStateOf<DashboardDto?>(null) }
    var refreshing by remember { mutableStateOf(false) }
    var error by remember { mutableStateOf<String?>(null) }
    val scope = rememberCoroutineScope()

    fun refresh() {
        refreshing = true
        error = null
        scope.launch {
            try {
                dashboard = repository.getDashboard()
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
        Column(
            modifier = Modifier
                .fillMaxSize()
                .verticalScroll(rememberScrollState())
                .padding(16.dp)
        ) {
            Text("Dashboard", style = MaterialTheme.typography.headlineSmall)
            ErrorText(error)
            Spacer(Modifier.height(8.dp))
            dashboard?.let { d ->
                SectionCard("Today") {
                    KeyValueRow("Production qty", d.todayProductionQty.toString())
                    KeyValueRow("Sales count", d.todaySalesCount.toString())
                    KeyValueRow("Sales total", "$" + "%.2f".format(d.todaySalesTotal))
                }
                SectionCard("Receivables") {
                    KeyValueRow("Total unpaid", "$" + "%.2f".format(d.totalUnpaid))
                    KeyValueRow("Debtors", d.debtorCount.toString())
                }
                SectionCard("Stock") {
                    d.stock.forEach { s ->
                        Text(
                            "${s.productName}: ${s.quantity}" + if (s.isLow) " (low)" else "",
                            style = MaterialTheme.typography.bodyMedium,
                            color = if (s.isLow) MaterialTheme.colorScheme.error
                            else MaterialTheme.colorScheme.onSurface
                        )
                    }
                }
                if (d.materials.isNotEmpty()) {
                    SectionCard("Raw materials") {
                        d.materials.forEach { m ->
                            Text(
                                "${m.name}: ${"%.4f".format(m.quantityOnHand).trimEnd('0').trimEnd('.')} ${m.unit}" +
                                    if (m.isLow) " (low)" else "",
                                style = MaterialTheme.typography.bodyMedium,
                                color = if (m.isLow) MaterialTheme.colorScheme.error
                                else MaterialTheme.colorScheme.onSurface
                            )
                        }
                    }
                }
            }
        }
    }
}
