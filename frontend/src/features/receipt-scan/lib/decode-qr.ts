import { scanImageData } from '@undecaf/zbar-wasm'

/**
 * Robust QR decoding for fiscal-receipt photos.
 *
 * Real receipt captures put a small, often skewed QR inside a large, noisy frame.
 * The previous `@zxing/browser` decoder failed on such photos outright — even on
 * cleanly-shot ones — so we decode with the platform's native `BarcodeDetector`
 * where it exists (excellent on Android Chrome, the primary scanning device) and
 * fall back to the zbar WASM engine everywhere else (Firefox, desktop Safari, …).
 */

// `BarcodeDetector` ships in Chromium-based browsers but isn't in the TS DOM lib.
interface DetectedBarcode {
  rawValue: string
}
interface BarcodeDetectorLike {
  detect(source: CanvasImageSource | ImageData | Blob): Promise<DetectedBarcode[]>
}
interface BarcodeDetectorCtor {
  new (options?: { formats?: string[] }): BarcodeDetectorLike
  getSupportedFormats?: () => Promise<string[]>
}

// Resolved once: a shared detector instance, or null when unsupported.
let detectorPromise: Promise<BarcodeDetectorLike | null> | undefined

async function getDetector(): Promise<BarcodeDetectorLike | null> {
  detectorPromise ??= (async () => {
    const ctor = (globalThis as { BarcodeDetector?: BarcodeDetectorCtor }).BarcodeDetector
    if (!ctor) return null
    try {
      const formats = (await ctor.getSupportedFormats?.()) ?? ['qr_code']
      if (!formats.includes('qr_code')) return null
      return new ctor({ formats: ['qr_code'] })
    } catch {
      return null
    }
  })()
  return detectorPromise
}

async function detectNative(source: CanvasImageSource): Promise<string | null> {
  const detector = await getDetector()
  if (!detector) return null
  try {
    const codes = await detector.detect(source)
    return codes.find((c) => c.rawValue)?.rawValue ?? null
  } catch {
    return null
  }
}

async function detectZbar(imageData: ImageData): Promise<string | null> {
  try {
    const symbols = await scanImageData(imageData)
    return symbols.find((s) => s.decode())?.decode() ?? null
  } catch {
    return null
  }
}

/** Draws a source onto a 2D canvas scaled so its longest side is `maxDim`. */
function toImageData(
  source: CanvasImageSource,
  width: number,
  height: number,
  maxDim: number,
): ImageData {
  const scale = Math.min(1, maxDim / Math.max(width, height))
  const w = Math.max(1, Math.round(width * scale))
  const h = Math.max(1, Math.round(height * scale))
  const canvas = document.createElement('canvas')
  canvas.width = w
  canvas.height = h
  const ctx = canvas.getContext('2d', { willReadFrequently: true })
  if (!ctx) throw new Error('2D canvas context unavailable')
  ctx.drawImage(source, 0, 0, w, h)
  return ctx.getImageData(0, 0, w, h)
}

/**
 * Decodes a fiscal-receipt QR from an uploaded image file. Returns the raw QR
 * string, or `null` when no QR could be read.
 */
export async function decodeQrFromImageFile(file: Blob): Promise<string | null> {
  const bitmap = await createImageBitmap(file)
  try {
    // 1) Native detector handles the full-resolution image best (scales internally).
    const native = await detectNative(bitmap)
    if (native) return native

    // 2) zbar fallback over a few resolutions — large photos often decode better
    //    once downscaled, so try a descending ladder and stop at the first hit.
    const targets = [bitmap.width, 1600, 1100, 800].filter(
      (t, i, all) => t <= bitmap.width && all.indexOf(t) === i,
    )
    for (const maxDim of targets) {
      const text = await detectZbar(toImageData(bitmap, bitmap.width, bitmap.height, maxDim))
      if (text) return text
    }
    return null
  } finally {
    bitmap.close()
  }
}

/**
 * Decodes a QR from the current frame of a playing `<video>`. Returns the raw QR
 * string, or `null` when this frame held no readable code (the caller keeps
 * polling). `scratch` is a reusable canvas so the scan loop avoids per-frame
 * allocations.
 */
export async function decodeQrFromVideoFrame(
  video: HTMLVideoElement,
  scratch: HTMLCanvasElement,
): Promise<string | null> {
  const { videoWidth: w, videoHeight: h } = video
  if (!w || !h) return null

  const native = await detectNative(video)
  if (native) return native

  scratch.width = w
  scratch.height = h
  const ctx = scratch.getContext('2d', { willReadFrequently: true })
  if (!ctx) return null
  ctx.drawImage(video, 0, 0, w, h)
  return detectZbar(ctx.getImageData(0, 0, w, h))
}
