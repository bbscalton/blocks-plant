package com.blocksplant.app

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import com.blocksplant.app.ui.BlocksPlantRoot
import com.blocksplant.app.ui.theme.BlocksPlantTheme

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        val container = application.appContainer()
        setContent {
            BlocksPlantTheme {
                BlocksPlantRoot(container)
            }
        }
    }
}
