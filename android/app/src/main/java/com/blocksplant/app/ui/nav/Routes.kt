package com.blocksplant.app.ui.nav

object Routes {
    const val Login = "login"
    const val Settings = "settings"
    const val Production = "production"
    const val Stock = "stock"
    const val Materials = "materials"
    const val Pos = "pos"
    const val Sales = "sales"
    const val Customers = "customers"
    const val Deliveries = "deliveries"
    const val Dashboard = "dashboard"
}

data class NavDest(
    val route: String,
    val label: String
)

fun destinationsForRole(role: String): List<NavDest> {
    val r = role.lowercase()
    return buildList {
        when (r) {
            "operator" -> {
                add(NavDest(Routes.Production, "Production"))
                add(NavDest(Routes.Stock, "Stock"))
                add(NavDest(Routes.Materials, "Materials"))
            }
            "cashier" -> {
                add(NavDest(Routes.Pos, "POS"))
                add(NavDest(Routes.Sales, "Sales"))
                add(NavDest(Routes.Customers, "Customers"))
                add(NavDest(Routes.Deliveries, "Deliveries"))
                add(NavDest(Routes.Stock, "Stock"))
                add(NavDest(Routes.Materials, "Materials"))
            }
            "owner" -> {
                add(NavDest(Routes.Dashboard, "Dashboard"))
                add(NavDest(Routes.Production, "Production"))
                add(NavDest(Routes.Pos, "POS"))
                add(NavDest(Routes.Sales, "Sales"))
                add(NavDest(Routes.Customers, "Customers"))
                add(NavDest(Routes.Deliveries, "Deliveries"))
                add(NavDest(Routes.Stock, "Stock"))
                add(NavDest(Routes.Materials, "Materials"))
            }
            else -> add(NavDest(Routes.Stock, "Stock"))
        }
        add(NavDest(Routes.Settings, "Settings"))
    }
}

fun startRouteForRole(role: String): String =
    when (role.lowercase()) {
        "operator" -> Routes.Production
        "cashier" -> Routes.Pos
        "owner" -> Routes.Dashboard
        else -> Routes.Stock
    }
