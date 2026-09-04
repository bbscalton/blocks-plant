package com.blocksplant.app.ui.customers

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
import androidx.compose.material3.OutlinedButton
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
import com.blocksplant.app.data.api.CustomerDto
import com.blocksplant.app.data.api.PaymentRequest
import com.blocksplant.app.data.repo.AppRepository
import com.blocksplant.app.ui.components.ErrorText
import com.blocksplant.app.ui.components.KeyValueRow
import com.blocksplant.app.ui.components.LabeledField
import com.blocksplant.app.ui.components.SectionCard
import com.blocksplant.app.ui.components.SuccessText
import com.blocksplant.app.util.toUserMessage
import kotlinx.coroutines.launch

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun CustomersScreen(repository: AppRepository) {
    var customers by remember { mutableStateOf<List<CustomerDto>>(emptyList()) }
    var search by remember { mutableStateOf("") }
    var name by remember { mutableStateOf("") }
    var phone by remember { mutableStateOf("") }
    var address by remember { mutableStateOf("") }
    var payCustomerId by remember { mutableStateOf<Int?>(null) }
    var payAmount by remember { mutableStateOf("") }
    var refreshing by remember { mutableStateOf(false) }
    var error by remember { mutableStateOf<String?>(null) }
    var success by remember { mutableStateOf<String?>(null) }
    val scope = rememberCoroutineScope()

    fun refresh(q: String? = search.ifBlank { null }) {
        refreshing = true
        error = null
        scope.launch {
            try {
                customers = repository.getCustomers(q)
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
                Text("Customers", style = MaterialTheme.typography.headlineSmall)
                Spacer(Modifier.height(8.dp))
                LabeledField("Search", search, onValueChange = { search = it })
                OutlinedButton(
                    onClick = { refresh(search.ifBlank { null }) },
                    modifier = Modifier.padding(top = 8.dp)
                ) { Text("Search") }

                Spacer(Modifier.height(12.dp))
                Text("New customer", style = MaterialTheme.typography.titleMedium)
                LabeledField("Name", name, onValueChange = { name = it })
                Spacer(Modifier.height(6.dp))
                LabeledField("Phone", phone, onValueChange = { phone = it })
                Spacer(Modifier.height(6.dp))
                LabeledField("Address", address, onValueChange = { address = it })
                Button(
                    onClick = {
                        if (name.isBlank()) {
                            error = "Name is required."
                            return@Button
                        }
                        scope.launch {
                            try {
                                repository.createCustomer(
                                    name.trim(),
                                    phone.ifBlank { null },
                                    address.ifBlank { null }
                                )
                                name = ""
                                phone = ""
                                address = ""
                                success = "Customer created."
                                refresh()
                            } catch (t: Throwable) {
                                error = t.toUserMessage()
                            }
                        }
                    },
                    modifier = Modifier.fillMaxWidth().padding(top = 8.dp)
                ) { Text("Create customer") }

                ErrorText(error)
                SuccessText(success)
                Spacer(Modifier.height(12.dp))
            }

            items(customers, key = { it.id }) { customer ->
                SectionCard(customer.name) {
                    if (!customer.phone.isNullOrBlank()) KeyValueRow("Phone", customer.phone)
                    if (!customer.address.isNullOrBlank()) KeyValueRow("Address", customer.address)
                    KeyValueRow("Balance", "$" + "%.2f".format(customer.balance))
                    if (customer.balance > 0) {
                        if (payCustomerId == customer.id) {
                            LabeledField(
                                "Payment amount",
                                payAmount,
                                onValueChange = { payAmount = it.filter { ch -> ch.isDigit() || ch == '.' } }
                            )
                            Button(
                                onClick = {
                                    val amount = payAmount.toDoubleOrNull() ?: 0.0
                                    if (amount <= 0) {
                                        error = "Enter a valid payment amount."
                                        return@Button
                                    }
                                    scope.launch {
                                        try {
                                            repository.createPayment(
                                                PaymentRequest(
                                                    customerId = customer.id,
                                                    saleId = null,
                                                    amount = amount,
                                                    method = "Cash"
                                                )
                                            )
                                            success = "Payment recorded."
                                            payCustomerId = null
                                            payAmount = ""
                                            refresh()
                                        } catch (t: Throwable) {
                                            error = t.toUserMessage()
                                        }
                                    }
                                },
                                modifier = Modifier.fillMaxWidth().padding(top = 6.dp)
                            ) { Text("Record payment") }
                        } else {
                            OutlinedButton(onClick = {
                                payCustomerId = customer.id
                                payAmount = ""
                            }) { Text("Record payment") }
                        }
                    }
                }
            }
        }
    }
}
