package com.blocksplant.app

import android.app.Application
import com.blocksplant.app.data.api.ApiClientFactory
import com.blocksplant.app.data.local.AppDatabase
import com.blocksplant.app.data.prefs.SessionStore
import com.blocksplant.app.data.repo.AppRepository

class BlocksPlantApp : Application() {
    lateinit var container: AppContainer
        private set

    override fun onCreate() {
        super.onCreate()
        container = AppContainer(this)
    }
}

class AppContainer(app: Application) {
    val sessionStore = SessionStore(app)
    private val db = AppDatabase.create(app)
    private val apiFactory = ApiClientFactory(sessionStore)
    val repository = AppRepository(apiFactory, sessionStore, db.pendingProductionDao())
}

fun Application.appContainer(): AppContainer = (this as BlocksPlantApp).container
