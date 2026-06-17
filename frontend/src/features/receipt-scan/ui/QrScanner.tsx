import { useCallback, useEffect, useRef, useState } from 'react'
import { Camera, CameraOff, ImageUp, Loader2 } from 'lucide-react'
import { Button } from '@/shared/ui/button'
import { decodeQrFromImageFile, decodeQrFromVideoFrame } from '../lib/decode-qr'

/** How often the live-camera loop samples a frame for a QR (ms). */
const SCAN_INTERVAL_MS = 250

/**
 * QR scanner (T4.3.1): reads a fiscal-receipt QR either from the live camera
 * (WebRTC, rear-facing where available) or from an uploaded image. Emits the raw
 * decoded string — the parent decides what to do with it (POST /scan-qr).
 *
 * Decoding goes through the platform's native `BarcodeDetector` when available and
 * falls back to the zbar WASM engine (see `lib/decode-qr`), which reads real
 * receipt photos that the older `@zxing/browser` reader could not.
 */
export function QrScanner({
  onDecode,
  disabled = false,
}: {
  onDecode: (qrRaw: string) => void
  disabled?: boolean
}) {
  const videoRef = useRef<HTMLVideoElement>(null)
  const streamRef = useRef<MediaStream | null>(null)
  const scratchRef = useRef<HTMLCanvasElement | null>(null)
  const scanIntervalRef = useRef<number | null>(null)
  const scanningRef = useRef(false)
  const decodingFrameRef = useRef(false)
  const fileInputRef = useRef<HTMLInputElement>(null)
  const mountedRef = useRef(true)

  const [cameraOn, setCameraOn] = useState(false)
  const [starting, setStarting] = useState(false)
  const [decoding, setDecoding] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const stopCamera = useCallback(() => {
    scanningRef.current = false
    if (scanIntervalRef.current !== null) {
      window.clearInterval(scanIntervalRef.current)
      scanIntervalRef.current = null
    }
    streamRef.current?.getTracks().forEach((track) => track.stop())
    streamRef.current = null
    const video = videoRef.current
    if (video) video.srcObject = null
    setCameraOn(false)
  }, [])

  // Always release the camera when the component unmounts.
  useEffect(() => {
    mountedRef.current = true
    return () => {
      mountedRef.current = false
      stopCamera()
    }
  }, [stopCamera])

  // One sampling tick of the live-camera loop. Driven by an interval (rather than
  // a self-rescheduling timeout) and guarded by `decodingFrameRef` so a slow
  // decode never overlaps the next tick.
  const scanFrame = useCallback(() => {
    if (!scanningRef.current || decodingFrameRef.current) return
    const video = videoRef.current
    if (!video) return
    const scratch = (scratchRef.current ??= document.createElement('canvas'))

    decodingFrameRef.current = true
    void decodeQrFromVideoFrame(video, scratch)
      .then((qrRaw) => {
        if (scanningRef.current && qrRaw) {
          stopCamera()
          onDecode(qrRaw)
        }
      })
      .catch(() => {})
      .finally(() => {
        decodingFrameRef.current = false
      })
  }, [onDecode, stopCamera])

  async function startCamera() {
    setError(null)
    setStarting(true)
    try {
      const stream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: 'environment' },
      })
      // An unmount during the await would otherwise leave the camera live.
      if (!mountedRef.current) {
        stream.getTracks().forEach((track) => track.stop())
        return
      }
      streamRef.current = stream
      const video = videoRef.current!
      video.srcObject = stream
      await video.play()
      setCameraOn(true)
      scanningRef.current = true
      decodingFrameRef.current = false
      scanIntervalRef.current = window.setInterval(scanFrame, SCAN_INTERVAL_MS)
    } catch (err) {
      stopCamera()
      // The browser rejects getUserMedia synchronously (no permission prompt) when
      // no camera device exists — that's `NotFoundError`, distinct from the user
      // denying access (`NotAllowedError`).
      const name = err instanceof DOMException ? err.name : ''
      setError(
        name === 'NotFoundError' || name === 'DevicesNotFoundError'
          ? 'Камера не найдена на устройстве. Загрузите фото чека.'
          : name === 'NotAllowedError'
            ? 'Нет доступа к камере. Разрешите доступ или загрузите изображение.'
            : 'Не удалось запустить камеру. Попробуйте загрузить изображение.',
      )
    } finally {
      setStarting(false)
    }
  }

  async function handleFile(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    event.target.value = '' // allow re-selecting the same file
    if (!file) return

    setError(null)
    stopCamera()
    setDecoding(true)
    try {
      const qrRaw = await decodeQrFromImageFile(file)
      if (qrRaw) {
        onDecode(qrRaw)
      } else {
        setError(
          'QR-код не распознан. Сфотографируйте чек ровно (без изгибов), ближе к QR-коду и при хорошем освещении.',
        )
      }
    } catch {
      setError('Не удалось обработать изображение. Попробуйте другое фото.')
    } finally {
      setDecoding(false)
    }
  }

  return (
    <div className="space-y-3">
      <div className="relative aspect-square w-full overflow-hidden rounded-xl border bg-muted">
        <video
          ref={videoRef}
          className="size-full object-cover"
          muted
          playsInline
          hidden={!cameraOn}
        />
        {!cameraOn && (
          <div className="absolute inset-0 flex flex-col items-center justify-center gap-2 text-muted-foreground">
            {decoding ? (
              <>
                <Loader2 className="size-10 animate-spin" />
                <p className="px-6 text-center text-sm">Распознаём QR-код…</p>
              </>
            ) : (
              <>
                <Camera className="size-10" />
                <p className="px-6 text-center text-sm">
                  Наведите камеру на QR-код чека или загрузите фото
                </p>
              </>
            )}
          </div>
        )}
        {cameraOn && (
          // Framing guide overlay.
          <div className="pointer-events-none absolute inset-0 flex items-center justify-center">
            <div className="size-2/3 rounded-lg border-2 border-primary/80 shadow-[0_0_0_9999px_rgba(0,0,0,0.35)]" />
          </div>
        )}
      </div>

      {error && <p className="text-sm text-destructive">{error}</p>}

      <div className="flex gap-2">
        {cameraOn ? (
          <Button type="button" variant="outline" className="flex-1" onClick={stopCamera}>
            <CameraOff className="size-4" /> Остановить
          </Button>
        ) : (
          <Button
            type="button"
            className="flex-1"
            disabled={disabled || starting || decoding}
            onClick={startCamera}
          >
            {starting ? <Loader2 className="size-4 animate-spin" /> : <Camera className="size-4" />}
            Камера
          </Button>
        )}

        <Button
          type="button"
          variant="outline"
          className="flex-1"
          disabled={disabled || decoding}
          onClick={() => fileInputRef.current?.click()}
        >
          <ImageUp className="size-4" /> Загрузить фото
        </Button>
        <input
          ref={fileInputRef}
          type="file"
          accept="image/*"
          hidden
          onChange={handleFile}
        />
      </div>
    </div>
  )
}
