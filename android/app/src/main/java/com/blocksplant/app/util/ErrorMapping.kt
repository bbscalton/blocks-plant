package com.blocksplant.app.util

import com.blocksplant.app.data.api.ApiErrorBody
import com.google.gson.Gson
import retrofit2.HttpException
import java.io.IOException

fun Throwable.toUserMessage(): String = when (this) {
    is HttpException -> {
        val body = response()?.errorBody()?.string()
        val parsed = runCatching {
            body?.let { Gson().fromJson(it, ApiErrorBody::class.java)?.message }
        }.getOrNull()
        parsed?.takeIf { it.isNotBlank() }
            ?: when (code()) {
                401 -> "Session expired or invalid credentials."
                403 -> "You do not have permission for this action."
                404 -> "Not found."
                else -> "Server error (${code()})."
            }
    }
    is IOException -> "Network unavailable. Check connection and API URL."
    else -> message?.takeIf { it.isNotBlank() } ?: "Unexpected error."
}

fun isNetworkFailure(t: Throwable): Boolean = t is IOException
