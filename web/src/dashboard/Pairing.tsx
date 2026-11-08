import { useEffect, useState } from 'react'
import { api } from '../api/client'

const POLL_MS = 3000

type Start = { code: string; secret: string; expiresAt: string }

/**
 * Kopplungsbildschirm: zeigt den Code, den die Leitung in der Windows-App (Seite Geräte) eintippt. Die Anzeige fragt dabei alle paar Sekunden nach,
 * ob sie gekoppelt ist; ein abgelaufener Code wird durch einen neuen ersetzt. Keine Eingabe an diesem Bildschirm.
 */
export function Pairing({ onPaired, onOff }: { onPaired: () => void; onOff: () => void }) {
  const [start, setStart] = useState<Start | null>(null)
  const [offline, setOffline] = useState(false)

  useEffect(() => {
    let cancelled = false
    let timer: ReturnType<typeof setTimeout> | undefined

    async function begin() {
      try {
        const { data, response } = await api.POST('/api/v1/display/pairing')
        if (cancelled) return
        if (response.status === 404) return onOff()
        if (!data) throw new Error('keine Antwort')
        setOffline(false)
        setStart(data)
        void poll(data)
      } catch {
        if (cancelled) return
        setOffline(true)
        timer = setTimeout(() => void begin(), POLL_MS)
      }
    }

    async function poll(current: Start) {
      try {
        const { response } = await api.POST('/api/v1/display/pairing/redeem', { body: { secret: current.secret } })
        if (cancelled) return
        if (response.status === 200) return onPaired()
        // 202: noch nicht gekoppelt. 401: Code abgelaufen, also ein neuer.
        if (response.status === 401) return void begin()
        setOffline(false)
      } catch {
        if (cancelled) return
        setOffline(true)
      }
      timer = setTimeout(() => void poll(current), POLL_MS)
    }

    void begin()
    return () => {
      cancelled = true
      clearTimeout(timer)
    }
  }, [onPaired, onOff])

  return (
    <main className="screen center">
      <h1>Anzeige koppeln</h1>
      <p className="lead">In der Windows-App unter Geräte bei „Anzeigen im Pausenraum“ diesen Code eintippen:</p>
      <p className="code" aria-live="polite">
        {start ? start.code : '…'}
      </p>
      {offline && <p className="hint">Keine Verbindung zum Server. Die Anzeige versucht es weiter.</p>}
    </main>
  )
}
