import { HubConnectionState, type HubConnection } from '@microsoft/signalr'
import { createLiveConnection, watchDisplay } from '../live/connection'

/** Wartezeit zwischen zwei Verbindungsversuchen: die Tafel läuft Tage ohne Zutun, ein Neustart des Servers darf sie nicht anhalten. */
export const RECONNECT_MS = 5000

export interface DisplayLiveHandlers {
  /** Etwas hat sich geändert (oder die Verbindung stand wieder): die Tafel lädt neu. */
  onChanged: () => void
}

export type StartLive = (handlers: DisplayLiveHandlers) => () => void

type Builder = (handlers: { onDisplayChanged: () => void }, options: { reconnectEveryMs: number }) => HubConnection

/**
 * Hört auf die Änderungen des Standorts der Anzeige und verbindet sich ohne Zutun neu: beim ersten Start so lange, bis der Server antwortet,
 * danach über die automatische Neuverbindung (ohne Ende, alle <see cref="RECONNECT_MS"/>). Nach jeder (Neu-)Verbindung lädt die Tafel neu,
 * weil in der Zwischenzeit Signale verloren gegangen sein können. Liefert die Funktion zum Beenden.
 */
export function startDisplayLive(handlers: DisplayLiveHandlers, build: Builder = createLiveConnection): () => void {
  const connection = build({ onDisplayChanged: handlers.onChanged }, { reconnectEveryMs: RECONNECT_MS })
  let stopped = false
  let timer: ReturnType<typeof setTimeout> | undefined

  const subscribe = async () => {
    await watchDisplay(connection)
    handlers.onChanged()
  }
  connection.onreconnected(() => void subscribe().catch(() => undefined))

  const connect = async () => {
    try {
      // Nach einem gelungenen Start wiederholt sich nur das Abo; start() auf einer verbundenen Verbindung wirft immer.
      if (connection.state === HubConnectionState.Disconnected) await connection.start()
      await subscribe()
    } catch {
      if (!stopped) timer = setTimeout(() => void connect(), RECONNECT_MS)
    }
  }
  void connect()

  return () => {
    stopped = true
    clearTimeout(timer)
    void connection.stop()
  }
}
