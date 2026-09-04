package com.blocksplant.app.data.local

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import kotlinx.coroutines.flow.Flow

@Dao
interface PendingProductionDao {
    @Query("SELECT * FROM pending_production ORDER BY createdAtEpochMs ASC")
    fun observeAll(): Flow<List<PendingProductionEntity>>

    @Query("SELECT * FROM pending_production ORDER BY createdAtEpochMs ASC")
    suspend fun getAll(): List<PendingProductionEntity>

    @Query("SELECT COUNT(*) FROM pending_production")
    fun observeCount(): Flow<Int>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(entity: PendingProductionEntity)

    @Query("DELETE FROM pending_production WHERE clientId = :clientId")
    suspend fun delete(clientId: String)

    @Query("UPDATE pending_production SET lastError = :error WHERE clientId = :clientId")
    suspend fun setError(clientId: String, error: String?)
}
