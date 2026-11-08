// Datum und Uhrzeit für „Mein Tag“, immer in der Zeitzone des Standorts. Eigene kleine Helfer, damit nichts aus dem
// Kalender der Team-Oberfläche ins Bundle kommt.

/** Kalenderdatum (YYYY-MM-DD) eines Zeitpunkts in der Zeitzone des Standorts. */
export function dateIn(timeZone: string, instant: Date = new Date()): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone, year: 'numeric', month: '2-digit', day: '2-digit' }).format(instant)
}

export function addDays(date: string, days: number): string {
  const [y, m, d] = date.split('-').map(Number)
  return new Date(Date.UTC(y, m - 1, d + days)).toISOString().slice(0, 10)
}

/** „Montag, 1. März“ für ein Kalenderdatum. */
export function dayLabel(date: string): string {
  const [y, m, d] = date.split('-').map(Number)
  return new Intl.DateTimeFormat('de-AT', { timeZone: 'UTC', weekday: 'long', day: 'numeric', month: 'long' }).format(new Date(Date.UTC(y, m - 1, d)))
}

export function timeIn(iso: string, timeZone: string): string {
  return new Intl.DateTimeFormat('de-AT', { timeZone, hour: '2-digit', minute: '2-digit' }).format(new Date(iso))
}

export function shortDate(iso: string, timeZone: string): string {
  return new Intl.DateTimeFormat('de-AT', { timeZone, day: 'numeric', month: 'numeric', year: 'numeric' }).format(new Date(iso))
}

/** Stand aus dem Speicher: „08:12“ am selben Tag, sonst mit Datum. */
export function standLabel(iso: string, timeZone: string, today: string | null): string {
  const clock = timeIn(iso, timeZone)
  return dateIn(timeZone, new Date(iso)) === today ? clock : `${shortDate(iso, timeZone)}, ${clock}`
}

/** Der Montag der Woche eines Kalenderdatums. */
export function mondayOf(date: string): string {
  const [y, m, d] = date.split('-').map(Number)
  const weekday = new Date(Date.UTC(y, m - 1, d)).getUTCDay()
  return addDays(date, weekday === 0 ? -6 : 1 - weekday)
}

/** Vorname und Initiale des Nachnamens: „Lena Gast“ wird „Lena G.“. Mehr braucht es im Behandlungsraum nicht. */
export function guestName(fullName: string): string {
  const parts = fullName.trim().split(/\s+/).filter(Boolean)
  if (parts.length < 2) return parts[0] ?? ''
  return `${parts[0]} ${parts[parts.length - 1][0]}.`
}

/** Menge für Anzeige und Eingabe: „300“, „1,5“. */
export const amount = (value: number) => value.toLocaleString('de-AT', { maximumFractionDigits: 3 })

/** Eingabe mit Komma oder Punkt als Zahl; leer oder keine Zahl ergibt <c>null</c>. */
export function parseAmount(text: string): number | null {
  const value = Number(text.trim().replace(',', '.'))
  return text.trim() !== '' && Number.isFinite(value) ? value : null
}

export const hours = (h: number) => `${h.toLocaleString('de-AT', { maximumFractionDigits: 1 })} h`

export function problemText(error: unknown, fallback = 'Das hat nicht geklappt. Prüfen Sie die Verbindung oder versuchen Sie es erneut.'): string {
  const e = error as { errors?: Record<string, string[]>; detail?: string; title?: string } | undefined
  if (e?.errors) return Object.values(e.errors).flat().join(' ')
  return e?.detail ?? e?.title ?? fallback
}
