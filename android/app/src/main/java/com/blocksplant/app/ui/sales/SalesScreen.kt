package com.blocksplant.app.ui.sales

import androidx.compose.foundation.layout.Column
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
import com.blocksplant.app.data.api.SaleDto
import com.blocksplant.app.data.repo.AppRepository
import com.blocksplant.app.ui.components.ErrorText
import com.blocksplant.app.ui.components.KeyValueRow
import com.blocksplant.app.ui.components.SectionCard
import com.blocksplant.app.util.toUserMessage
import kotlinx.coroutines.launch

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SalesScreen(repository: AppRepository) {
    var sales by remember { mutableStateOf<List<SaleDto>>(emptyList()) }
    var refreshing by remember { mutableStateOf(false) }
    var error by remember { mutableStateOf<String?>(null) }
    val scope = rememberCoroutineScope()

    fun refresh() {
        refreshing = true
        error = null
        scope.launch {
            try {
                sales = repository.getSales()
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
                Text("Sales", style = MaterialTheme.typography.headlineSmall)
                ErrorText(error)
                Spacer(Modifier.height(8.dp))
            }
            items(sales, key = { it.id }) { sale ->
                SectionCard("#${sale.id} · ${sale.customerName ?: "Walk-in"}") {
                    KeyValueRow("Total", "$" + "%.2f".format(sale.subtotal))
                    KeyValueRow("Paid", "$" + "%.2f".format(sale.amountPaid))
                    KeyValueRow("Balance", "$" + "%.2f".format(sale.balanceDue))
                    KeyValueRow("Fulfillment", "${sale.fulfillmentType} / ${sale.deliveryStatus}")
                    KeyValueRow("Cashier", sale.cashierName)
                    Text(sale.createdAt, style = MaterialTheme.typography.bodySmall)
                    sale.lines.forEach { line ->
                        Text(
                            "· ${line.productName} × ${line.quantity}",
                            style = MaterialTheme.typography.bodySmall
                        )
                    }
                }
            }
        }
    }
}
