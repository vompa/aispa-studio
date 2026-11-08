import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr'
import type { AppointmentDto } from '../api/client'

/** Wie PongEvent in AispaStudio.Contracts (SignalR-Nachrichten stehen nicht im OpenAPI-Dokument). */
export interface PongEvent {
  pingId: string
  message: string
  processedAt: string
}

/** Wie InboxChangedEvent in AispaStudio.Contracts. */
export interface InboxChangedEvent {
  mailId: string
}

/** Was sich im Betrieb eines Standorts geändert hat, wie OperationsChangeKind in AispaStudio.Contracts (die Windows-App lädt danach ihre Meldungen nach). */
export type OperationsChangeKind = 'Stock' | 'RoomCare' | 'RoomStart' | 'Reminders'

export interface LiveHandlers {
  onPong?: (pong: PongEvent) => void
  /** Termin gebucht, verschoben, storniert oder Status geändert (nur für beobachtete Standorte). */
  onAppointmentChanged?: (appointment: AppointmentDto) => void
  /** Nachricht im Posteingang neu oder geändert (nach watchInbox). */
  onInboxChanged?: (change: InboxChangedEvent) => void
  /** Betrieb eines beobachteten Standorts geändert (Lager, Raumpflege, Tagesstart, Handbuch); die Team-Oberfläche braucht es nicht. */
  onOperationsChanged?: (locationId: string, kind: OperationsChangeKind) => void
  /** Die Tafel im Pausenraum soll neu laden; das Signal trägt keine Daten (nur die gekoppelte Anzeige bekommt es, nach watchDisplay). */
  onDisplayChanged?: () => void
}

export interface LiveOptions {
  /** Verbindet sich nach einem Abbruch ohne Ende alle so viele Millisekunden neu (Standard von SignalR: nach vier Versuchen aufgeben). */
  reconnectEveryMs?: number
}

/** Echtzeit-Verbindung zur API (SignalR); Pfad und Methodennamen wie in LiveHubContract.cs. */
export function createLiveConnection(handlers: LiveHandlers, options: LiveOptions = {}): HubConnection {
  const { reconnectEveryMs } = options
  const builder = new HubConnectionBuilder().withUrl('/hubs/live')
  const connection = (
    reconnectEveryMs === undefined ? builder.withAutomaticReconnect() : builder.withAutomaticReconnect({ nextRetryDelayInMilliseconds: () => reconnectEveryMs })
  )
    .configureLogging(LogLevel.Warning)
    .build()
  if (handlers.onPong) connection.on('Pong', handlers.onPong)
  if (handlers.onAppointmentChanged) connection.on('AppointmentChanged', handlers.onAppointmentChanged)
  if (handlers.onInboxChanged) connection.on('InboxChanged', handlers.onInboxChanged)
  if (handlers.onDisplayChanged) connection.on('DisplayChanged', handlers.onDisplayChanged)
  // Der Server ruft die Methode für jeden Beobachter eines Standorts auf; ohne Empfänger käme je Aufruf eine Warnung im Protokoll.
  connection.on('OperationsChanged', handlers.onOperationsChanged ?? (() => undefined))
  return connection
}

export const watchLocation = (connection: HubConnection, locationId: string) =>
  connection.invoke('WatchLocation', locationId)

export const unwatchLocation = (connection: HubConnection, locationId: string) =>
  connection.invoke('UnwatchLocation', locationId)

export const watchInbox = (connection: HubConnection) => connection.invoke('WatchInbox')

/** Nur für die gekoppelte Anzeige: hört auf die Änderungen ihres Standorts (der Standort kommt vom Server aus ihrer Anmeldung). */
export const watchDisplay = (connection: HubConnection) => connection.invoke('WatchDisplay')
