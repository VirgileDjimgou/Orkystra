package com.fleetops.driver

import android.content.Context
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import java.security.KeyStore
import java.util.Base64
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

interface DriverCredentialStore {
    fun readAccessToken(): String?
    fun writeAccessToken(accessToken: String)
    fun clear()
}

class AndroidKeystoreDriverCredentialStore(context: Context) : DriverCredentialStore {
    private val preferences = context.getSharedPreferences(PREFERENCES_NAME, Context.MODE_PRIVATE)

    override fun readAccessToken(): String? {
        val encrypted = preferences.getString(CIPHERTEXT_KEY, null) ?: return null
        val iv = preferences.getString(IV_KEY, null) ?: return null
        return runCatching {
            val cipher = Cipher.getInstance(TRANSFORMATION)
            cipher.init(
                Cipher.DECRYPT_MODE,
                getOrCreateKey(),
                GCMParameterSpec(128, Base64.getDecoder().decode(iv)),
            )
            String(cipher.doFinal(Base64.getDecoder().decode(encrypted)), Charsets.UTF_8)
        }.getOrElse {
            clear()
            null
        }
    }

    override fun writeAccessToken(accessToken: String) {
        require(accessToken.isNotBlank()) { "Access token cannot be blank." }
        val cipher = Cipher.getInstance(TRANSFORMATION)
        cipher.init(Cipher.ENCRYPT_MODE, getOrCreateKey())
        val encrypted = cipher.doFinal(accessToken.toByteArray(Charsets.UTF_8))
        preferences.edit()
            .putString(CIPHERTEXT_KEY, Base64.getEncoder().encodeToString(encrypted))
            .putString(IV_KEY, Base64.getEncoder().encodeToString(cipher.iv))
            .commit()
    }

    override fun clear() {
        preferences.edit().clear().commit()
    }

    private fun getOrCreateKey(): SecretKey {
        val keyStore = KeyStore.getInstance(ANDROID_KEYSTORE).apply { load(null) }
        (keyStore.getKey(KEY_ALIAS, null) as? SecretKey)?.let { return it }

        val generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, ANDROID_KEYSTORE)
        generator.init(
            KeyGenParameterSpec.Builder(
                KEY_ALIAS,
                KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT,
            )
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setRandomizedEncryptionRequired(true)
                .build(),
        )
        return generator.generateKey()
    }

    private companion object {
        const val ANDROID_KEYSTORE = "AndroidKeyStore"
        const val KEY_ALIAS = "fleetops.driver.session.v1"
        const val PREFERENCES_NAME = "fleetops-secure-session"
        const val CIPHERTEXT_KEY = "access-token-ciphertext"
        const val IV_KEY = "access-token-iv"
        const val TRANSFORMATION = "AES/GCM/NoPadding"
    }
}

class InMemoryDriverCredentialStore : DriverCredentialStore {
    private var accessToken: String? = null

    override fun readAccessToken(): String? = accessToken

    override fun writeAccessToken(accessToken: String) {
        this.accessToken = accessToken
    }

    override fun clear() {
        accessToken = null
    }
}
