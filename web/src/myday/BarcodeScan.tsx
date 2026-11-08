import { type KeyboardEvent, useEffect, useId, useRef, useState } from 'react'

const FORMATS = ['code_128', 'ean_13', 'ean_8', 'upc_a', 'qr_code']
const POLL_MS = 250
/** Derselbe Code, den die Kamera dauernd sieht, zählt innerhalb dieser Zeit einmal. */
const SAME_CODE_MS = 1000

type Detector = { detect: (source: HTMLVideoElement) => Promise<{ rawValue: string }[]> }
type DetectorConstructor = new (options: { formats: string[] }) => Detector

function detectorConstructor(): DetectorConstructor | null {
  const ctor = (window as unknown as { BarcodeDetector?: DetectorConstructor }).BarcodeDetector
  return ctor && typeof navigator.mediaDevices?.getUserMedia === 'function' ? ctor : null
}

/**
 * Code erfassen: Kamera-Scan mit `BarcodeDetector`, wo der Browser ihn kann, sonst (oder bei Fehler) das Feld „Code eingeben“.
 * Das Feld ist immer da. `onCode` meldet einen Satz zurück, wenn der Code nicht zu einem Material führt.
 */
export function BarcodeScan({ onCode }: { onCode: (code: string) => Promise<string | null> }) {
  const supported = detectorConstructor() !== null
  const [typed, setTyped] = useState('')
  const [running, setRunning] = useState(false)
  const [message, setMessage] = useState<string | null>(supported ? null : 'Kamera-Scan ist auf diesem Gerät nicht verfügbar. Bitte den Code eingeben.')
  const videoRef = useRef<HTMLVideoElement>(null)
  const onCodeRef = useRef(onCode)
  const inputId = useId()

  useEffect(() => {
    onCodeRef.current = onCode
  })

  async function lookUp(code: string) {
    setMessage(await onCodeRef.current(code))
  }

  useEffect(() => {
    const Detector = detectorConstructor()
    if (!running || !Detector) return
    let cancelled = false
    let timer: number | undefined
    let stream: MediaStream | undefined
    let last = { code: '', at: 0 }

    const stopStream = () => stream?.getTracks().forEach((track) => track.stop())

    async function scanLoop() {
      if (cancelled || !videoRef.current) return
      try {
        const [found] = await new Detector!({ formats: FORMATS }).detect(videoRef.current)
        if (found && !cancelled) {
          const repeated = found.rawValue === last.code && Date.now() - last.at < SAME_CODE_MS
          last = { code: found.rawValue, at: Date.now() }
          if (!repeated) await lookUp(found.rawValue)
        }
      } catch {
        // Ein Bild ohne lesbaren Code ist kein Fehler; die nächste Runde versucht es wieder.
      }
      if (!cancelled) timer = window.setTimeout(() => void scanLoop(), POLL_MS)
    }

    async function start() {
      try {
        stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' }, audio: false })
      } catch {
        if (cancelled) return
        setMessage('Die Kamera ist nicht freigegeben. Bitte den Code eingeben.')
        setRunning(false)
        return
      }
      if (cancelled) return stopStream()
      if (videoRef.current) {
        videoRef.current.srcObject = stream
        await videoRef.current.play().catch(() => undefined)
      }
      void scanLoop()
    }

    setMessage(null)
    void start()
    return () => {
      cancelled = true
      window.clearTimeout(timer)
      stopStream()
    }
    // lookUp liest nur Refs und setMessage.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [running])

  function submitTyped() {
    const code = typed.trim()
    if (!code) return
    setTyped('')
    void lookUp(code)
  }

  function onKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key !== 'Enter') return
    event.preventDefault()
    submitTyped()
  }

  return (
    <div className="scan">
      {supported && (
        <div className="actions">
          <button type="button" className="secondary" onClick={() => setRunning((r) => !r)}>
            {running ? 'Kamera beenden' : 'Kamera starten'}
          </button>
        </div>
      )}
      {running && <video ref={videoRef} className="scan-video" muted playsInline aria-label="Kamerabild" />}
      <div className="scan-code">
        <div className="field">
          <label htmlFor={inputId}>Code eingeben</label>
          <input id={inputId} type="text" inputMode="text" autoComplete="off" value={typed} onChange={(e) => setTyped(e.target.value)} onKeyDown={onKeyDown} />
        </div>
        <button type="button" className="secondary" onClick={submitTyped}>
          Suchen
        </button>
      </div>
      <p role="status" className="message scan-message">
        {message}
      </p>
    </div>
  )
}
