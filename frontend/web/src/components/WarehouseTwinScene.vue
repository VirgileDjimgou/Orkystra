<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import {
  AmbientLight,
  BoxGeometry,
  Color,
  CylinderGeometry,
  DirectionalLight,
  Group,
  Mesh,
  MeshStandardMaterial,
  PCFSoftShadowMap,
  PerspectiveCamera,
  PlaneGeometry,
  Scene,
  WebGLRenderer,
} from 'three'
import type { WarehouseZoneView } from '../data/controlTower'

const props = defineProps<{
  zones: WarehouseZoneView[]
  occupiedDockCount: number
  storedPalletCount: number
}>()

const canvasHost = ref<HTMLElement | null>(null)

let renderer: WebGLRenderer | null = null
let scene: Scene | null = null
let camera: PerspectiveCamera | null = null
let warehouseGroup: Group | null = null
let animationFrameId = 0
let resizeObserver: ResizeObserver | null = null
let isDragging = false
let dragOriginX = 0
let baseRotationY = 0

function buildScene(): void {
  if (!canvasHost.value) {
    return
  }

  scene = new Scene()
  scene.background = new Color('#0f172a')

  camera = new PerspectiveCamera(45, 1, 0.1, 100)
  camera.position.set(0, 8.5, 13)
  camera.lookAt(0, 0, 0)

  renderer = new WebGLRenderer({ antialias: true, alpha: false })
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2))
  renderer.shadowMap.enabled = true
  renderer.shadowMap.type = PCFSoftShadowMap

  canvasHost.value.innerHTML = ''
  canvasHost.value.appendChild(renderer.domElement)

  const ambientLight = new AmbientLight('#f8fafc', 1.8)
  scene.add(ambientLight)

  const keyLight = new DirectionalLight('#c7d2fe', 2.6)
  keyLight.position.set(6, 12, 8)
  keyLight.castShadow = true
  scene.add(keyLight)

  const backLight = new DirectionalLight('#14b8a6', 1.5)
  backLight.position.set(-8, 6, -6)
  scene.add(backLight)

  const floor = new Mesh(
    new PlaneGeometry(18, 12),
    new MeshStandardMaterial({ color: '#172554', metalness: 0.2, roughness: 0.8 }),
  )
  floor.rotation.x = -Math.PI / 2
  floor.position.y = -1.25
  floor.receiveShadow = true
  scene.add(floor)

  const dockStrip = new Mesh(
    new BoxGeometry(10, 0.35, 1.2),
    new MeshStandardMaterial({ color: '#1d4ed8', metalness: 0.15, roughness: 0.55 }),
  )
  dockStrip.position.set(0, -1.05, 4.4)
  scene.add(dockStrip)

  warehouseGroup = new Group()
  scene.add(warehouseGroup)

  populateWarehouse()
  resizeScene()
  animateScene()

  resizeObserver = new ResizeObserver(() => resizeScene())
  resizeObserver.observe(canvasHost.value)
}

function populateWarehouse(): void {
  if (!warehouseGroup) {
    return
  }

  warehouseGroup.clear()

  props.zones.forEach((zone, index) => {
    const normalizedHeight = 1.8 + zone.utilization / 28
    const rack = new Mesh(
      new BoxGeometry(1.35, normalizedHeight, 2.2),
      new MeshStandardMaterial({
        color: zone.status === 'Critical' ? '#f97316' : zone.status === 'Watch' ? '#facc15' : '#22c55e',
        metalness: 0.22,
        roughness: 0.45,
      }),
    )

    rack.castShadow = true
    rack.receiveShadow = true
    rack.position.set(-4.2 + index * 2.15, normalizedHeight / 2 - 1.2, 0)
    warehouseGroup.add(rack)

    const palletTop = new Mesh(
      new BoxGeometry(1.1, 0.22, 1.75),
      new MeshStandardMaterial({ color: '#e2e8f0', metalness: 0.08, roughness: 0.72 }),
    )
    palletTop.position.set(rack.position.x, rack.position.y + normalizedHeight / 2 + 0.28, 0)
    palletTop.castShadow = true
    warehouseGroup.add(palletTop)
  })

  const beaconCount = Math.max(props.occupiedDockCount, 1)
  for (let index = 0; index < beaconCount; index += 1) {
    const beacon = new Mesh(
      new CylinderGeometry(0.14, 0.14, 0.9, 16),
      new MeshStandardMaterial({ color: '#38bdf8', emissive: '#0ea5e9', emissiveIntensity: 0.4 }),
    )
    beacon.position.set(-3.6 + index * 2.3, -0.5, 4.3)
    warehouseGroup.add(beacon)
  }

  warehouseGroup.rotation.x = -0.28
  warehouseGroup.rotation.y = -0.4
}

function resizeScene(): void {
  if (!canvasHost.value || !renderer || !camera) {
    return
  }

  const { clientWidth, clientHeight } = canvasHost.value
  if (clientWidth === 0 || clientHeight === 0) {
    return
  }

  renderer.setSize(clientWidth, clientHeight)
  camera.aspect = clientWidth / clientHeight
  camera.updateProjectionMatrix()
}

function animateScene(): void {
  if (!renderer || !scene || !camera || !warehouseGroup) {
    return
  }

  animationFrameId = window.requestAnimationFrame(animateScene)
  if (!isDragging) {
    warehouseGroup.rotation.y += 0.003
  }
  renderer.render(scene, camera)
}

function handlePointerDown(event: PointerEvent): void {
  isDragging = true
  dragOriginX = event.clientX
  baseRotationY = warehouseGroup?.rotation.y ?? 0
}

function handlePointerMove(event: PointerEvent): void {
  if (!isDragging || !warehouseGroup) {
    return
  }

  const delta = (event.clientX - dragOriginX) * 0.01
  warehouseGroup.rotation.y = baseRotationY + delta
}

function handlePointerUp(): void {
  isDragging = false
}

watch(
  () => [props.zones, props.occupiedDockCount, props.storedPalletCount],
  () => populateWarehouse(),
  { deep: true },
)

onMounted(() => {
  buildScene()
  canvasHost.value?.addEventListener('pointerdown', handlePointerDown)
  window.addEventListener('pointermove', handlePointerMove)
  window.addEventListener('pointerup', handlePointerUp)
})

onBeforeUnmount(() => {
  window.cancelAnimationFrame(animationFrameId)
  window.removeEventListener('pointermove', handlePointerMove)
  window.removeEventListener('pointerup', handlePointerUp)
  canvasHost.value?.removeEventListener('pointerdown', handlePointerDown)
  resizeObserver?.disconnect()
  renderer?.dispose()
})
</script>

<template>
  <div class="twin-stage">
    <div ref="canvasHost" class="canvas-host" aria-label="Warehouse digital twin"></div>
    <div class="scene-caption">
      <span>Drag to rotate</span>
      <strong>{{ storedPalletCount }} pallets simulated</strong>
    </div>
  </div>
</template>

<style scoped>
.twin-stage {
  position: relative;
  min-height: 320px;
  overflow: hidden;
  border: 1px solid rgba(148, 163, 184, 0.22);
  border-radius: 8px;
  background: linear-gradient(180deg, #0f172a 0%, #111827 100%);
}

.canvas-host {
  width: 100%;
  min-height: 320px;
  cursor: grab;
}

.canvas-host:active {
  cursor: grabbing;
}

.scene-caption {
  position: absolute;
  right: 16px;
  bottom: 16px;
  display: grid;
  gap: 4px;
  padding: 12px 14px;
  color: #e2e8f0;
  background: rgba(15, 23, 42, 0.74);
  border: 1px solid rgba(148, 163, 184, 0.24);
  border-radius: 8px;
  backdrop-filter: blur(10px);
}

.scene-caption span {
  font-size: 12px;
  color: #94a3b8;
}

.scene-caption strong {
  font-size: 14px;
}
</style>
