import { useCallback, useEffect, useState } from 'react'
import { api } from '../api/client'
import type { components } from '../api/schema'
import { BoardView } from './Board'
import { dayLabel, timeIn } from './format'
import { startDisplayLive, type StartLive } from './live'
import { Pairing } from './Pairing'

type Board = components['schemas']['DisplayBoard']

/** Die Tafel lädt auch ohne Signal nach: die Zeit läuft weiter, Dienstplan und Raumsperren lösen kein Signal aus. */
export const REFRESH_MS = 30_000
const CLOCK_MS = 10_000
const RETRY_MS = 5000

type Stage = 'checking' | 'pairing' | 'board' | 'off'

/** Dashboard im Pausenraum: koppeln, dann den Tag zeigen. Nur Anzeige, nach Wunsch im Vollbild (F11). Eigenes Bundle unter /dashboard/. */
export function DashboardApp({ live = startDisplayLive }: { live?: StartLive }) {
  const [stage, setStage] = useState<Stage>('checking')
  const [offline, setOffline] = useState(false)

  const check = useCallback(async () => {
    try {
      const { response } = await api.GET('/api/v1/display/session')
      // Nur 401 heißt „nicht gekoppelt“; ein Fehler des Servers (502 beim Hochfahren) darf die Kopplung nie verwerfen.
      const known = response.status === 200 || response.status === 404 || response.status === 401
      setOffline(!known)
      if (known) setStage(response.status === 200 ? 'board' : response.status === 404 ? 'off' : 'pairing')
    } catch {
      setOffline(true)
    }
  }, [])

  useEffect(() => {
    if (stage !== 'checking') return
    // Der erste Versuch läuft sofort, aber nicht im Rumpf des Effekts: die Antwort setzt den Zustand.
    const first = setTimeout(() => void check(), 0)
    const timer = setInterval(() => void check(), RETRY_MS)
    return () => {
      clearTimeout(first)
      clearInterval(timer)
    }
  }, [stage, check])

  const paired = useCallback(() => setStage('board'), [])
  const off = useCallback(() => setStage('off'), [])
  const unpaired = useCallback(() => setStage('pairing'), [])

  if (stage === 'pairing') return <Pairing onPaired={paired} onOff={off} />
  if (stage === 'board') return <Tafel live={live} onSignedOut={unpaired} onOff={off} />
  return (
    <main className="screen center">
      <h1>{stage === 'off' ? 'Dashboard ausgeschaltet' : 'AISPA Studio'}</h1>
      <p className="lead">
        {stage === 'off'
          ? 'Das Dashboard ist auf diesem Server nicht eingeschaltet.'
          : offline
            ? 'Keine Verbindung zum Server. Die Anzeige versucht es weiter.'
            : 'Die Anzeige startet …'}
      </p>
    </main>
  )
}

function Tafel({ live, onSignedOut, onOff }: { live: StartLive; onSignedOut: () => void; onOff: () => void }) {
  const [board, setBoard] = useState<Board | null>(null)
  const [stale, setStale] = useState(false)
  const [now, setNow] = useState(() => new Date())

  const load = useCallback(async () => {
    try {
      const { data, response } = await api.GET('/api/v1/display/board')
      if (response.status === 401 || response.status === 403) return onSignedOut()
      if (response.status === 404) return onOff()
      if (!data) throw new Error('keine Antwort')
      setBoard(data)
      setStale(false)
    } catch {
      setStale(true)
    }
  }, [onSignedOut, onOff])

  useEffect(() => {
    const first = setTimeout(() => void load(), 0)
    const refresh = setInterval(() => void load(), REFRESH_MS)
    const stop = live({ onChanged: () => void load() })
    return () => {
      clearTimeout(first)
      clearInterval(refresh)
      stop()
    }
  }, [load, live])

  useEffect(() => {
    const tick = setInterval(() => setNow(new Date()), CLOCK_MS)
    return () => clearInterval(tick)
  }, [])

  if (!board) {
    return (
      <main className="screen center">
        <h1>AISPA Studio</h1>
        <p className="lead">{stale ? 'Keine Verbindung zum Server. Die Anzeige versucht es weiter.' : 'Der Tag wird geladen …'}</p>
      </main>
    )
  }

  return (
    <main className="screen">
      <header>
        <h1>{board.locationName}</h1>
        <p className="clock">
          <span className="day">{dayLabel(now, board.timeZone)}</span> <time dateTime={now.toISOString()}>{timeIn(now, board.timeZone)}</time>
        </p>
      </header>
      {stale && <p className="hint">Keine Verbindung zum Server. Gezeigt wird der letzte Stand.</p>}
      <BoardView board={board} now={now} />
    </main>
  )
}
