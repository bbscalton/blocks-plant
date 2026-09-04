package com.blocksplant.app.data.prefs

import android.content.Context
import android.content.SharedPreferences
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKey
import com.blocksplant.app.BuildConfig
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

data class Session(
    val token: String,
    val username: String,
    val fullName: String,
    val role: String,
    val userId: Int
) {
    val isOwner: Boolean get() = role.equals("Owner", ignoreCase = true)
    val isCashier: Boolean get() = role.equals("Cashier", ignoreCase = true)
    val isOperator: Boolean get() = role.equals("Operator", ignoreCase = true)
    val canPos: Boolean get() = isOwner || isCashier
    val canProduction: Boolean get() = isOwner || isOperator
}

class SessionStore(context: Context) {
    private val prefs: SharedPreferences = EncryptedSharedPreferences.create(
        context,
        "blocks_plant_secure",
        MasterKey.Builder(context).setKeyScheme(MasterKey.KeyScheme.AES256_GCM).build(),
        EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
        EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM
    )

    private val settingsPrefs = context.getSharedPreferences("blocks_plant_settings", Context.MODE_PRIVATE)

    private val _session = MutableStateFlow(readSession())
    val session: StateFlow<Session?> = _session.asStateFlow()

    private val _baseUrl = MutableStateFlow(
        settingsPrefs.getString(KEY_BASE_URL, BuildConfig.DEFAULT_API_BASE_URL)
            ?: BuildConfig.DEFAULT_API_BASE_URL
    )
    val baseUrl: StateFlow<String> = _baseUrl.asStateFlow()

    fun currentToken(): String? = _session.value?.token

    fun saveSession(loginToken: String, username: String, fullName: String, role: String, userId: Int) {
        prefs.edit()
            .putString(KEY_TOKEN, loginToken)
            .putString(KEY_USERNAME, username)
            .putString(KEY_FULL_NAME, fullName)
            .putString(KEY_ROLE, role)
            .putInt(KEY_USER_ID, userId)
            .apply()
        _session.value = Session(loginToken, username, fullName, role, userId)
    }

    fun clearSession() {
        prefs.edit().clear().apply()
        _session.value = null
    }

    fun setBaseUrl(url: String) {
        val normalized = url.trim().trimEnd('/')
        settingsPrefs.edit().putString(KEY_BASE_URL, normalized).apply()
        _baseUrl.value = normalized
    }

    private fun readSession(): Session? {
        val token = prefs.getString(KEY_TOKEN, null) ?: return null
        val username = prefs.getString(KEY_USERNAME, null) ?: return null
        val fullName = prefs.getString(KEY_FULL_NAME, "") ?: ""
        val role = prefs.getString(KEY_ROLE, null) ?: return null
        val userId = prefs.getInt(KEY_USER_ID, -1)
        if (userId < 0) return null
        return Session(token, username, fullName, role, userId)
    }

    companion object {
        private const val KEY_TOKEN = "token"
        private const val KEY_USERNAME = "username"
        private const val KEY_FULL_NAME = "fullName"
        private const val KEY_ROLE = "role"
        private const val KEY_USER_ID = "userId"
        private const val KEY_BASE_URL = "apiBaseUrl"
    }
}
