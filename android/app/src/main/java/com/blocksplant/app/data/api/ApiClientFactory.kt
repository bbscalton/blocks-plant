package com.blocksplant.app.data.api

import com.blocksplant.app.data.prefs.SessionStore
import okhttp3.Interceptor
import okhttp3.OkHttpClient
import okhttp3.logging.HttpLoggingInterceptor
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory
import java.util.concurrent.TimeUnit

class ApiClientFactory(private val sessionStore: SessionStore) {
    @Volatile
    private var cachedBaseUrl: String? = null

    @Volatile
    private var cachedApi: BlocksPlantApi? = null

    fun api(): BlocksPlantApi {
        val base = sessionStore.baseUrl.value.trimEnd('/') + "/"
        val existing = cachedApi
        if (existing != null && cachedBaseUrl == base) return existing
        synchronized(this) {
            if (cachedApi != null && cachedBaseUrl == base) return cachedApi!!
            val client = OkHttpClient.Builder()
                .connectTimeout(15, TimeUnit.SECONDS)
                .readTimeout(30, TimeUnit.SECONDS)
                .addInterceptor(AuthInterceptor(sessionStore))
                .addInterceptor(
                    HttpLoggingInterceptor().apply {
                        level = HttpLoggingInterceptor.Level.BASIC
                    }
                )
                .build()
            val retrofit = Retrofit.Builder()
                .baseUrl(base)
                .client(client)
                .addConverterFactory(GsonConverterFactory.create())
                .build()
            cachedBaseUrl = base
            cachedApi = retrofit.create(BlocksPlantApi::class.java)
            return cachedApi!!
        }
    }

    fun invalidate() {
        synchronized(this) {
            cachedApi = null
            cachedBaseUrl = null
        }
    }
}

private class AuthInterceptor(private val sessionStore: SessionStore) : Interceptor {
    override fun intercept(chain: Interceptor.Chain): okhttp3.Response {
        val token = sessionStore.currentToken()
        val request = if (token.isNullOrBlank()) {
            chain.request()
        } else {
            chain.request().newBuilder()
                .header("Authorization", "Bearer $token")
                .build()
        }
        return chain.proceed(request)
    }
}
