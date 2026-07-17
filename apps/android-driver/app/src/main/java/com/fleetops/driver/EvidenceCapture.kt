package com.fleetops.driver

import android.content.Context
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.graphics.Path
import android.net.Uri
import android.provider.Settings
import android.view.MotionEvent
import android.view.View
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.PickVisualMediaRequest
import androidx.activity.result.contract.ActivityResultContracts
import androidx.camera.core.CameraSelector
import androidx.camera.core.ImageCapture
import androidx.camera.core.ImageCaptureException
import androidx.camera.core.Preview
import androidx.camera.lifecycle.ProcessCameraProvider
import androidx.camera.view.PreviewView
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.getValue
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalLifecycleOwner
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.core.content.ContextCompat
import androidx.core.content.FileProvider
import java.io.ByteArrayOutputStream
import java.io.File
import java.util.Base64
import java.util.UUID
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

private const val MAX_EVIDENCE_BYTES = 1_500_000

@Composable
fun EvidenceSourceChooser(
    visible: Boolean,
    title: String,
    caption: String,
    onDismiss: () -> Unit,
    onCaptured: (CapturedEvidence) -> Unit,
    onError: (String) -> Unit,
) {
    if (!visible) return
    val context = LocalContext.current
    var showCamera by remember { mutableStateOf(false) }
    val picker = rememberLauncherForActivityResult(ActivityResultContracts.PickVisualMedia()) { uri ->
        if (uri != null) {
            prepareEvidence(context, uri, caption, onCaptured, onError)
        }
    }
    val permission = rememberLauncherForActivityResult(ActivityResultContracts.RequestPermission()) { granted ->
        if (granted) {
            showCamera = true
        } else {
            onError("Camera access was not granted. Choose an existing photo instead.")
        }
    }

    if (showCamera) {
        CameraCaptureDialog(
            title = title,
            onDismiss = { showCamera = false },
            onCaptured = { uri ->
                showCamera = false
                prepareEvidence(context, uri, caption, onCaptured, onError)
            },
            onError = onError,
        )
    } else {
        AlertDialog(
            onDismissRequest = onDismiss,
            title = { Text(title) },
            text = { Text("Choose Camera for a new photo. Permission is requested only when you use the camera; Photo picker works without it.") },
            confirmButton = {
                Button(onClick = { permission.launch(android.Manifest.permission.CAMERA) }) { Text("Use camera") }
            },
            dismissButton = {
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    OutlinedButton(onClick = { picker.launch(PickVisualMediaRequest(ActivityResultContracts.PickVisualMedia.ImageOnly)) }) { Text("Photo picker") }
                    OutlinedButton(onClick = onDismiss) { Text("Cancel") }
                }
            },
        )
    }
}

@Composable
private fun CameraCaptureDialog(
    title: String,
    onDismiss: () -> Unit,
    onCaptured: (Uri) -> Unit,
    onError: (String) -> Unit,
) {
    val context = LocalContext.current
    val lifecycleOwner = LocalLifecycleOwner.current
    val previewView = remember { PreviewView(context) }
    val imageCapture = remember { ImageCapture.Builder().build() }
    val scope = rememberCoroutineScope()
    LaunchedEffect(lifecycleOwner) {
        val provider = ProcessCameraProvider.getInstance(context).get()
        provider.unbindAll()
        provider.bindToLifecycle(lifecycleOwner, CameraSelector.DEFAULT_BACK_CAMERA, Preview.Builder().build().also { it.surfaceProvider = previewView.surfaceProvider }, imageCapture)
    }
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(title) },
        text = { AndroidView(factory = { previewView }, modifier = Modifier.fillMaxWidth().height(360.dp)) },
        confirmButton = {
            Button(onClick = {
                val file = createEvidenceFile(context)
                val output = ImageCapture.OutputFileOptions.Builder(file).build()
                imageCapture.takePicture(output, ContextCompat.getMainExecutor(context), object : ImageCapture.OnImageSavedCallback {
                    override fun onImageSaved(outputFileResults: ImageCapture.OutputFileResults) {
                        onCaptured(FileProvider.getUriForFile(context, "${context.packageName}.files", file))
                    }
                    override fun onError(exception: ImageCaptureException) {
                        onError("Camera capture failed. You can retry or use Photo picker.")
                    }
                })
            }) { Text("Take photo") }
        },
        dismissButton = { OutlinedButton(onClick = onDismiss) { Text("Cancel") } },
    )
}

@Composable
fun SignatureCaptureDialog(
    visible: Boolean,
    onDismiss: () -> Unit,
    onCaptured: (CapturedEvidence) -> Unit,
    onError: (String) -> Unit,
) {
    if (!visible) return
    val context = LocalContext.current
    var signatureView: SignaturePadView? by remember { mutableStateOf(null) }
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Recipient signature") },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Text("Ask the recipient to sign inside the box. This signature is stored locally until it is securely uploaded.")
                AndroidView(
                    factory = { SignaturePadView(it).also { view -> signatureView = view } },
                    modifier = Modifier.fillMaxWidth().height(220.dp).background(androidx.compose.ui.graphics.Color.White),
                )
                OutlinedButton(onClick = { signatureView?.clear() }) { Text("Clear signature") }
            }
        },
        confirmButton = {
            Button(onClick = {
                val bytes = signatureView?.toPng()
                if (bytes == null) {
                    onError("A handwritten signature is required before continuing.")
                } else {
                    onCaptured(CapturedEvidence("signature-${UUID.randomUUID()}.png", "image/png", Base64.getEncoder().encodeToString(bytes), SIGNATURE_CAPTION))
                    onDismiss()
                }
            }) { Text("Use signature") }
        },
        dismissButton = { OutlinedButton(onClick = onDismiss) { Text("Cancel") } },
    )
}

private fun prepareEvidence(context: Context, uri: Uri, caption: String, onCaptured: (CapturedEvidence) -> Unit, onError: (String) -> Unit) {
    val scope = kotlinx.coroutines.CoroutineScope(Dispatchers.Main)
    scope.launch {
        runCatching { withContext(Dispatchers.IO) { uri.toCompressedEvidence(context, caption) } }
            .onSuccess(onCaptured)
            .onFailure { onError("Photo could not be prepared. Free device storage or choose a smaller image, then retry.") }
    }
}

internal fun Uri.toCompressedEvidence(context: Context, caption: String): CapturedEvidence {
    val bitmap = context.contentResolver.openInputStream(this)?.use(BitmapFactory::decodeStream)
        ?: throw IllegalArgumentException("Selected photo is unavailable.")
    var quality = 82
    var encoded: ByteArray
    do {
        val output = ByteArrayOutputStream()
        bitmap.compress(Bitmap.CompressFormat.JPEG, quality, output)
        encoded = output.toByteArray()
        quality -= 10
    } while (encoded.size > MAX_EVIDENCE_BYTES && quality >= 42)
    if (encoded.size > MAX_EVIDENCE_BYTES) throw IllegalStateException("Photo is too large after compression.")
    return CapturedEvidence("evidence-${UUID.randomUUID()}.jpg", "image/jpeg", Base64.getEncoder().encodeToString(encoded), caption)
}

private fun createEvidenceFile(context: Context): File =
    File(context.cacheDir, "evidence").apply { mkdirs() }.let { directory -> File(directory, "camera-${UUID.randomUUID()}.jpg") }

private class SignaturePadView(context: Context) : View(context) {
    private val path = Path()
    private val paint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.BLACK; style = Paint.Style.STROKE; strokeWidth = 7f; strokeCap = Paint.Cap.ROUND }
    private var hasStroke = false
    override fun onDraw(canvas: Canvas) { super.onDraw(canvas); canvas.drawColor(Color.WHITE); canvas.drawPath(path, paint) }
    override fun onTouchEvent(event: MotionEvent): Boolean {
        when (event.actionMasked) {
            MotionEvent.ACTION_DOWN -> { path.moveTo(event.x, event.y); hasStroke = true }
            MotionEvent.ACTION_MOVE -> path.lineTo(event.x, event.y)
            MotionEvent.ACTION_UP -> path.lineTo(event.x, event.y)
        }
        invalidate(); return true
    }
    fun clear() { path.reset(); hasStroke = false; invalidate() }
    fun toPng(): ByteArray? {
        if (!hasStroke || width == 0 || height == 0) return null
        return ByteArrayOutputStream().use { output ->
            Bitmap.createBitmap(width, height, Bitmap.Config.ARGB_8888).also { bitmap -> Canvas(bitmap).drawColor(Color.WHITE); Canvas(bitmap).drawPath(path, paint); bitmap.compress(Bitmap.CompressFormat.PNG, 100, output); bitmap.recycle() }
            output.toByteArray()
        }
    }
}
