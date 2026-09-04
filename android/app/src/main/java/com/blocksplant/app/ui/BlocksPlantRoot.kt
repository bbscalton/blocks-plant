package com.blocksplant.app.ui

import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.Logout
import androidx.compose.material.icons.filled.Menu
import androidx.compose.material3.DrawerValue
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalDrawerSheet
import androidx.compose.material3.ModalNavigationDrawer
import androidx.compose.material3.NavigationDrawerItem
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.rememberDrawerState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.rememberNavController
import com.blocksplant.app.AppContainer
import com.blocksplant.app.ui.customers.CustomersScreen
import com.blocksplant.app.ui.dashboard.DashboardScreen
import com.blocksplant.app.ui.deliveries.DeliveriesScreen
import com.blocksplant.app.ui.login.LoginScreen
import com.blocksplant.app.ui.materials.MaterialsScreen
import com.blocksplant.app.ui.nav.Routes
import com.blocksplant.app.ui.nav.destinationsForRole
import com.blocksplant.app.ui.nav.startRouteForRole
import com.blocksplant.app.ui.pos.PosScreen
import com.blocksplant.app.ui.production.ProductionScreen
import com.blocksplant.app.ui.sales.SalesScreen
import com.blocksplant.app.ui.settings.SettingsScreen
import com.blocksplant.app.ui.stock.StockScreen
import kotlinx.coroutines.launch

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun BlocksPlantRoot(container: AppContainer) {
    val session by container.sessionStore.session.collectAsState()
    val baseUrl by container.sessionStore.baseUrl.collectAsState()
    val navController = rememberNavController()
    val drawerState = rememberDrawerState(DrawerValue.Closed)
    val scope = rememberCoroutineScope()
    val pendingCount by container.repository.pendingCount.collectAsState(initial = 0)

    if (session == null) {
        NavHost(navController = navController, startDestination = Routes.Login) {
            composable(Routes.Login) {
                LoginScreen(
                    repository = container.repository,
                    initialBaseUrl = baseUrl,
                    onLoggedIn = { },
                    onOpenSettings = { navController.navigate(Routes.Settings) }
                )
            }
            composable(Routes.Settings) {
                SettingsScreen(
                    repository = container.repository,
                    currentBaseUrl = baseUrl,
                    onSaved = { }
                )
            }
        }
        return
    }

    val currentSession = session!!
    val destinations = destinationsForRole(currentSession.role)
    val start = startRouteForRole(currentSession.role)
    val backStack by navController.currentBackStackEntryAsState()
    val currentRoute = backStack?.destination?.route ?: start
    val title = destinations.find { it.route == currentRoute }?.label ?: "Blocks Plant"

    ModalNavigationDrawer(
        drawerState = drawerState,
        drawerContent = {
            ModalDrawerSheet {
                Text(
                    "${currentSession.fullName} (${currentSession.role})",
                    modifier = Modifier.padding(PaddingValues(16.dp))
                )
                if (pendingCount > 0) {
                    Text(
                        "Offline queue: $pendingCount",
                        modifier = Modifier.padding(PaddingValues(horizontal = 16.dp, vertical = 4.dp)),
                        color = MaterialTheme.colorScheme.error
                    )
                }
                destinations.forEach { dest ->
                    NavigationDrawerItem(
                        label = { Text(dest.label) },
                        selected = currentRoute == dest.route,
                        onClick = {
                            navController.navigate(dest.route) {
                                popUpTo(start) { saveState = true }
                                launchSingleTop = true
                                restoreState = true
                            }
                            scope.launch { drawerState.close() }
                        }
                    )
                }
                NavigationDrawerItem(
                    label = { Text("Log out") },
                    selected = false,
                    icon = { Icon(Icons.AutoMirrored.Filled.Logout, contentDescription = null) },
                    onClick = {
                        container.repository.logout()
                        scope.launch { drawerState.close() }
                    }
                )
            }
        }
    ) {
        Scaffold(
            topBar = {
                TopAppBar(
                    title = {
                        Text(
                            if (pendingCount > 0 && currentRoute == Routes.Production) {
                                "$title · $pendingCount pending"
                            } else {
                                title
                            }
                        )
                    },
                    navigationIcon = {
                        IconButton(onClick = { scope.launch { drawerState.open() } }) {
                            Icon(Icons.Default.Menu, contentDescription = "Menu")
                        }
                    }
                )
            }
        ) { padding ->
            NavHost(
                navController = navController,
                startDestination = start,
                modifier = Modifier.padding(padding)
            ) {
                composable(Routes.Production) {
                    ProductionScreen(container.repository)
                }
                composable(Routes.Stock) {
                    StockScreen(container.repository, currentSession)
                }
                composable(Routes.Materials) {
                    MaterialsScreen(container.repository)
                }
                composable(Routes.Pos) {
                    PosScreen(container.repository)
                }
                composable(Routes.Sales) {
                    SalesScreen(container.repository)
                }
                composable(Routes.Customers) {
                    CustomersScreen(container.repository)
                }
                composable(Routes.Deliveries) {
                    DeliveriesScreen(container.repository)
                }
                composable(Routes.Dashboard) {
                    DashboardScreen(container.repository)
                }
                composable(Routes.Settings) {
                    SettingsScreen(
                        repository = container.repository,
                        currentBaseUrl = baseUrl,
                        onSaved = { }
                    )
                }
            }
        }
    }
}
