package com.fleetops.driver

import com.google.gson.Gson
import com.google.gson.reflect.TypeToken

private val gson = Gson()

object driverJson {
    fun encodeInspectionOperation(operation: PendingInspectionOperation): String =
        gson.toJson(operation)

    fun decodeInspectionOperation(json: String): PendingInspectionOperation =
        gson.fromJson(json, PendingInspectionOperation::class.java)

    fun encodeDeliveryProofOperation(operation: PendingDeliveryProofOperation): String =
        gson.toJson(operation)

    fun decodeDeliveryProofOperation(json: String): PendingDeliveryProofOperation =
        gson.fromJson(json, PendingDeliveryProofOperation::class.java)

    fun encodeToString(photos: List<PendingPhotoUpload>): String =
        gson.toJson(photos)

    fun decodePendingPhotos(json: String): List<PendingPhotoUpload> {
        val type = object : TypeToken<List<PendingPhotoUpload>>() {}.type
        return gson.fromJson<List<PendingPhotoUpload>>(json, type) ?: emptyList()
    }
}
