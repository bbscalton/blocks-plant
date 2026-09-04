package com.blocksplant.app.ui.pos

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
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
import androidx.compose.material3.FilterChip
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.MenuAnchorType
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.blocksplant.app.data.api.CreateSaleRequest
import com.blocksplant.app.data.api.CustomerDto
import com.blocksplant.app.data.api.ProductDto
import com.blocksplant.app.data.api.SaleLineRequest
import com.blocksplant.app.data.repo.AppRepository
import com.blocksplant.app.ui.components.ErrorText
import com.blocksplant.app.ui.components.KeyValueRow
import com.blocksplant.app.ui.components.LabeledField
import com.blocksplant.app.ui.components.LoadingBox
import com.blocksplant.app.ui.components.SuccessText
import com.blocksplant.app.util.toUserMessage
import kotlinx.coroutines.launch
import java.util.UUID
import kotlin.math.max

data class CartLine(val product: ProductDto, val quantity: Int)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun PosScreen(repository: AppRepository) {
    var products by remember { mutableStateOf<List<ProductDto>>(emptyList()) }
    var customers by remember { mutableStateOf<List<CustomerDto>>(emptyList()) }
    var cart by remember { mutableStateOf<List<CartLine>>(emptyList()) }
    var fulfillment by remember { mutableStateOf("Collect") }
    var customerId by remember { mutableStateOf<Int?>(null) }
    var deliveryAddress by remember { mutableStateOf("") }
    var deliveryNotes by remember { mutableStateOf("") }
    var amountPaidText by remember { mutableStateOf("") }
    var loading by remember { mutableStateOf(true) }
    var submitting by remember { mutableStateOf(false) }
    var error by remember { mutableStateOf<String?>(null) }
    var success by remember { mutableStateOf<String?>(null) }
    var customerMenuExpanded by remember { mutableStateOf(false) }
    val scope = rememberCoroutineScope()

    LaunchedEffect(Unit) {
        loading = true
        try {
            products = repository.getProducts()
            customers = repository.getCustomers()
        } catch (t: Throwable) {
            error = t.toUserMessage()
        } finally {
            loading = false
        }
    }

    val subtotal = cart.sumOf { (it.product.pricePerBlock ?: 0.0) * it.quantity }
    val amountPaid = amountPaidText.toDoubleOrNull() ?: 0.0
    val balanceDue = max(0.0, subtotal - amountPaid)

    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(16.dp)
    ) {
        Text("POS (backup)", style = MaterialTheme.typography.headlineSmall)
        Text(
            "Primary cashier POS is Windows. Network required for sales.",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
        Spacer(Modifier.height(12.dp))

        if (loading) {
            LoadingBox()
            return@Column
        }

        Text("Add products", style = MaterialTheme.typography.titleMedium)
        products.forEach { product ->
            val price = product.pricePerBlock
            Row(
                modifier = Modifier.fillMaxWidth().padding(vertical = 4.dp),
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                Column(modifier = Modifier.weight(1f)) {
                    Text(product.name)
                    Text(
                        "Stock ${product.quantity}" +
                            (price?.let { " · $${"%.2f".format(it)}" } ?: ""),
                        style = MaterialTheme.typography.bodySmall
                    )
                }
                OutlinedButton(onClick = {
                    val existing = cart.find { it.product.id == product.id }
                    cart = if (existing == null) {
                        cart + CartLine(product, 1)
                    } else {
                        cart.map {
                            if (it.product.id == product.id) it.copy(quantity = it.quantity + 1) else it
                        }
                    }
                }) { Text("+1") }
            }
        }

        Spacer(Modifier.height(12.dp))
        Text("Cart", style = MaterialTheme.typography.titleMedium)
        if (cart.isEmpty()) {
            Text("No lines yet.", style = MaterialTheme.typography.bodyMedium)
        } else {
            cart.forEach { line ->
                Row(
                    modifier = Modifier.fillMaxWidth().padding(vertical = 2.dp),
                    horizontalArrangement = Arrangement.SpaceBetween
                ) {
                    Text("${line.product.name} × ${line.quantity}")
                    Text("$" + "%.2f".format((line.product.pricePerBlock ?: 0.0) * line.quantity))
                }
            }
            OutlinedButton(onClick = { cart = emptyList() }) { Text("Clear cart") }
        }

        Spacer(Modifier.height(12.dp))
        Text("Fulfillment", style = MaterialTheme.typography.titleMedium)
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            FilterChip(
                selected = fulfillment == "Collect",
                onClick = { fulfillment = "Collect" },
                label = { Text("Collect") }
            )
            FilterChip(
                selected = fulfillment == "Deliver",
                onClick = { fulfillment = "Deliver" },
                label = { Text("Deliver") }
            )
        }
        if (fulfillment == "Deliver") {
            Spacer(Modifier.height(8.dp))
            LabeledField("Delivery address", deliveryAddress, onValueChange = { deliveryAddress = it })
            Spacer(Modifier.height(8.dp))
            LabeledField("Delivery notes", deliveryNotes, onValueChange = { deliveryNotes = it })
        }

        Spacer(Modifier.height(12.dp))
        Text("Customer (required if balance due)", style = MaterialTheme.typography.titleMedium)
        ExposedDropdownMenuBox(
            expanded = customerMenuExpanded,
            onExpandedChange = { customerMenuExpanded = it }
        ) {
            val selectedName = customers.find { it.id == customerId }?.name ?: "None / walk-in"
            OutlinedTextField(
                value = selectedName,
                onValueChange = {},
                readOnly = true,
                label = { Text("Customer") },
                trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded = customerMenuExpanded) },
                modifier = Modifier
                    .menuAnchor(MenuAnchorType.PrimaryNotEditable)
                    .fillMaxWidth()
            )
            ExposedDropdownMenu(
                expanded = customerMenuExpanded,
                onDismissRequest = { customerMenuExpanded = false }
            ) {
                DropdownMenuItem(
                    text = { Text("None / walk-in") },
                    onClick = {
                        customerId = null
                        customerMenuExpanded = false
                    }
                )
                customers.forEach { c ->
                    DropdownMenuItem(
                        text = { Text("${c.name} (bal $${"%.2f".format(c.balance)})") },
                        onClick = {
                            customerId = c.id
                            customerMenuExpanded = false
                        }
                    )
                }
            }
        }

        Spacer(Modifier.height(12.dp))
        KeyValueRow("Subtotal", "$" + "%.2f".format(subtotal))
        LabeledField(
            "Amount paid (cash/partial)",
            amountPaidText,
            onValueChange = { amountPaidText = it.filter { ch -> ch.isDigit() || ch == '.' } }
        )
        KeyValueRow("Balance due", "$" + "%.2f".format(balanceDue))
        Text(
            when {
                amountPaid <= 0.0 && subtotal > 0 -> "Credit sale"
                balanceDue > 0 -> "Partial payment"
                else -> "Paid in full"
            },
            style = MaterialTheme.typography.bodySmall
        )

        ErrorText(error)
        SuccessText(success)
        Spacer(Modifier.height(12.dp))
        Button(
            onClick = {
                if (cart.isEmpty()) {
                    error = "Add at least one product line."
                    return@Button
                }
                if (fulfillment == "Deliver" && deliveryAddress.isBlank()) {
                    error = "Delivery address is required."
                    return@Button
                }
                if (balanceDue > 0 && customerId == null) {
                    error = "Customer is required for credit or partial payment."
                    return@Button
                }
                if (amountPaid > subtotal) {
                    error = "Amount paid cannot exceed subtotal."
                    return@Button
                }
                submitting = true
                error = null
                success = null
                scope.launch {
                    try {
                        val sale = repository.createSale(
                            CreateSaleRequest(
                                clientId = UUID.randomUUID().toString(),
                                customerId = customerId,
                                fulfillmentType = fulfillment,
                                deliveryAddress = deliveryAddress.ifBlank { null },
                                deliveryNotes = deliveryNotes.ifBlank { null },
                                amountPaid = amountPaid,
                                lines = cart.map { SaleLineRequest(it.product.id, it.quantity) }
                            )
                        )
                        success = "Sale #${sale.id} saved. Total $${"%.2f".format(sale.subtotal)}."
                        cart = emptyList()
                        amountPaidText = ""
                        deliveryAddress = ""
                        deliveryNotes = ""
                        products = repository.getProducts()
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
            Text(if (submitting) "Saving…" else "Complete sale")
        }
    }
}
