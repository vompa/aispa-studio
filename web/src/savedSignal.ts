/**
 * „Daten gespeichert“: jede erfolgreiche Änderung an der API meldet das hier, damit die KI-Leiste nach „Jetzt ergänzen“ anbieten kann, die Frage zu wiederholen
 * (ADR 0072, Nachtrag Runde 5b). Im Browser hören die Leiste und ihre Tests auf das Fensterereignis; eingebettet in die Windows-App geht dieselbe Meldung als
 * Nachricht an die App (`invokeCSharpAction`, die der eingebettete Browser bereitstellt), denn dort steht die KI-Leiste außerhalb der Verwaltung.
 */
const EVENT = 'aispa:saved'

/** Schreibende Aufrufe, die nichts bedeuten, was die Antwort des Assistenten ändert. */
const IGNORED = ['/api/v1/assistant', '/api/v1/auth/', '/calendar-request']

type AppBridge = { invokeCSharpAction?: (message: unknown) => void }

export function isSavingRequest(method: string, url: string): boolean {
  if (method === 'GET' || method === 'HEAD' || method === 'OPTIONS') return false
  return !IGNORED.some((part) => url.includes(part))
}

export function notifySaved() {
  window.dispatchEvent(new Event(EVENT))
  if (new URLSearchParams(window.location.search).has('eingebettet')) (window as unknown as AppBridge).invokeCSharpAction?.({ type: 'saved' })
}

/** Hört auf „gespeichert“; gibt die Funktion zum Abmelden zurück. */
export function onSaved(listener: () => void): () => void {
  window.addEventListener(EVENT, listener)
  return () => window.removeEventListener(EVENT, listener)
}
