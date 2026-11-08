// Zeiten der Tafel, immer in der Zeitzone des Standorts. Eigene kleine Helfer: nichts aus der Team-Oberfläche im Bundle.

export function timeIn(instant: Date | string, timeZone: string): string {
  return new Intl.DateTimeFormat('de-AT', { timeZone, hour: '2-digit', minute: '2-digit' }).format(new Date(instant))
}

/** „Montag, 1. März“. */
export function dayLabel(instant: Date, timeZone: string): string {
  return new Intl.DateTimeFormat('de-AT', { timeZone, weekday: 'long', day: 'numeric', month: 'long' }).format(instant)
}

/** Volle Stunde (0–24) einer Uhrzeit am Standort, für die Skala der Tagesleiste. */
export function hourIn(instant: Date | string, timeZone: string): number {
  const parts = new Intl.DateTimeFormat('en-GB', { timeZone, hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).formatToParts(new Date(instant))
  const hour = Number(parts.find((p) => p.type === 'hour')?.value ?? 0)
  const minute = Number(parts.find((p) => p.type === 'minute')?.value ?? 0)
  return hour + minute / 60
}
