package com.blocksplant.app.ui.production

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.FilterChip
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.blocksplant.app.data.api.ProductDto
import com.blocksplant.app.data.repo.AppRepository
import com.blocksplant.app.ui.components.ErrorText
import com.blocksplant.app.ui.components.LabeledField
import com.blocksplant.app.ui.components.LoadingBox
import com.blocksplant.app.ui.components.SectionCard
import com.blocksplant.app.ui.components.SuccessText
import com.blocksplant.app.util.toUserMessage
import kotlinx.coroutines.launch

@Composable
fun ProductionScreen(repository: AppRepository) {
    var products by remember { mutableStateOf<List<ProductDto>>(emptyList()) }
    var selected by remember { mutableStateOf<ProductDto?>(null) }
    var qtyText by remember { mutableStateOf("") }
    var rejectsText by remember { mutableStateOf("0") }
    var loading by remember { mutableStateOf(true) }
    var submitting by remember { mutableStateOf(false) }
    var syncing by remember { mutableStateOf(false) }
    var error by remember { mutableStateOf<String?>(null) }
    var success by remember { mutableStateOf<String?>(null) }
    val pendingCount by repository.pendingCount.collectAsState(initial = 0)
    val pendingItems by repository.pendingItems.collectAsState(initial = emptyList())
    val scope = rememberCoroutineScope()

    fun refresh() {
        loading = true
        error = null
        scope.launch {
            try {
                products = repository.getProducts()
                if (selected == null) selected = products.firstOrNull()
            } catch (t: Throwable) {
                error = t.toUserMessage()
            } finally {
                loading = false
            }
        }
    }

    LaunchedEffect(Unit) {
        refresh()
        repository.syncPending()
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(16.dp)
    ) {
        Text("Production entry", style = MaterialTheme.typography.headlineSmall)
        if (pendingCount > 0) {
            Text(
                "Pending sync: $pendingCount",
                color = MaterialTheme.colorScheme.error,
                style = MaterialTheme.typography.titleMedium,
                modifier = Modifier.padding(top = 8.dp)
            )
            OutlinedButton(
                onClick = {
                    syncing = true
                    success = null
                    error = null
                    scope.launch {
                        try {
                            val result = repository.syncPending()
                            success = "Synced ${result.synced}. Remaining ${result.remaining}."
                            if (result.errors.isNotEmpty()) {
                                error = result.errors.joinToString("\n")
                            }
                        } catch (t: Throwable) {
                            error = t.toUserMessage()
                        } finally {
                            syncing = false
                        }
                    }
                },
                enabled = !syncing,
                modifier = Modifier.padding(top = 4.dp)
            ) {
                Text(if (syncing) "Syncing…" else "Retry sync now")
            }
        }
        Spacer(Modifier.height(12.dp))

        if (loading) {
            LoadingBox()
        } else {
            Text("Product", style = MaterialTheme.typography.titleMedium)
            Row(
                horizontalArrangement = Arrangement.spacedBy(8.dp),
                modifier = Modifier.padding(vertical = 8.dp)
            ) {
                products.forEach { product ->
                    FilterChip(
                        selected = selected?.id == product.id,
                        onClick = { selected = product },
                        label = { Text("${product.sizeInches}\"") }
                    )
                }
            }
            selected?.let {
                Text("${it.name} · stock ${it.quantity}", style = MaterialTheme.typography.bodyMedium)
            }
            Spacer(Modifier.height(8.dp))
            LabeledField("Quantity produced", qtyText, onValueChange = { qtyText = it.filter(Char::isDigit) })
            Spacer(Modifier.height(8.dp))
            LabeledField("Rejects (optional)", rejectsText, onValueChange = { rejectsText = it.filter(Char::isDigit) })
            ErrorText(error)
            SuccessText(success)
            Spacer(Modifier.height(12.dp))
            Button(
                onClick = {
                    val product = selected
                    val qty = qtyText.toIntOrNull() ?: 0
                    val rejects = rejectsText.toIntOrNull() ?: 0
                    if (product == null) {
                        error = "Select a product."
                        return@Button
                    }
                    if (qty <= 0) {
                        error = "Quantity must be greater than zero."
                        return@Button
                    }
                    if (rejects < 0) {
                        error = "Rejects cannot be negative."
                        return@Button
                    }
                    submitting = true
                    error = null
                    success = null
                    scope.launch {
                        try {
                            success = repository.submitProduction(
                                product.id,
                                product.name,
                                qty,
                                rejects
                            )
                            qtyText = ""
                            rejectsText = "0"
                            runCatching { products = repository.getProducts() }
                        } catch (t: Throwable) {
                            error = t.toUserMessage()
                        } finally {
                            submitting = false
                        }
                    }
                },
                enabled = !submitting,
                modifier = Modifier.fillMaxWidth()
            ) {
                Text(if (submitting) "Submitting…" else "Submit production")
            }
        }

        if (pendingItems.isNotEmpty()) {
            Spacer(Modifier.height(16.dp))
            SectionCard("Offline queue") {
                pendingItems.forEach { item ->
                    Text(
                        "${item.productName}: +${item.quantity}" +
                            (if (item.rejects > 0) " (rejects ${item.rejects})" else "") +
                            (item.lastError?.let { " — $it" } ?: ""),
                        style = MaterialTheme.typography.bodyMedium,
                        modifier = Modifier.padding(vertical = 4.dp)
                    )
                }
            }
        }
    }
}
