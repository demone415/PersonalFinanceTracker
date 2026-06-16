import { useCallback, useEffect, useRef, useState } from 'react'
import { BrowserQRCodeReader, type IScannerControls } from '@zxing/browser'
import { Camera, CameraOff, ImageUp, Loader2 } from 'lucide-react'
import { Button } from '@/shared/ui/button'

/**
 * QR scanner (T4.3.1): reads a fiscal-receipt QR either from the live camera
 * (WebRTC, rear-facing where available) or from an uploaded image. Emits the raw
 * decoded string — the parent decides what to do with it (POST /scan-qr).
 *
 * The `@zxing/browser` reader requests the camera itself; we surface permission
 * and "no QR found" failures as inline messages rather than throwing.
 */
export function QrScanner({
  onDecode,
  disabled = false,
}: {
  onDecode: (qrRaw: string) => void
  disabled?: boolean
}) {
  const videoRef = useRef<HTMLVideoElement>(null)
  const controlsRef = useRef<IScannerControls | null>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)

  const [cameraOn, setCameraOn] = useState(false)
  const [starting, setStarting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const stopCamera = useCallback(() => {
    controlsRef.current?.stop()
    controlsRef.current = null
    setCameraOn(false)
  }, [])

  // Always release the camera when the component unmounts.
  useEffect(() => stopCamera, [stopCamera])

  async function startCamera() {
    setError(null)
    setStarting(true)
    try {
      const reader = new BrowserQRCodeReader()
      setCameraOn(true)
      controlsRef.current = await reader.decodeFromConstraints(
        { video: { facingMode: 'environment' } },
        videoRef.current!,
        (result) => {
          if (!result) return // no code in this frame — keep scanning
          stopCamera()
          onDecode(result.getText())
        },
      )
    } catch (err) {
      setCameraOn(false)
      setError(
        err instanceof DOMException && err.name === 'NotAllowedError'
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
    const url = URL.createObjectURL(file)
    try {
      const reader = new BrowserQRCodeReader()
      const result = await reader.decodeFromImageUrl(url)
      onDecode(result.getText())
    } catch {
      setError('QR-код не распознан на изображении. Попробуйте другое фото.')
    } finally {
      URL.revokeObjectURL(url)
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
            <Camera className="size-10" />
            <p className="px-6 text-center text-sm">
              Наведите камеру на QR-код чека или загрузите фото
            </p>
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
            disabled={disabled || starting}
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
          disabled={disabled}
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
