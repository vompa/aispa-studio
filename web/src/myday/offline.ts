// Verbindung zum Service Worker (public/mein-tag/sw.js). Der Worker hält nur den Tagesplan für den Behandlungsraum vor;
// siehe dort, was gespeichert wird und was nicht.

import { clearCountQueue } from './countQueue'

export const DATA_CACHE = 'aispa-mein-tag-data'
const OWNER_KEY = 'aispa-mein-tag-owner'
/** Kopfzeilen, die der Worker an eine Antwort aus dem Speicher hängt. */
export const OFFLINE_HEADER = 'X-Aispa-Offline'
export const CACHED_AT_HEADER = 'X-Aispa-Cached-At'

export function registerServiceWorker() {
  if (!import.meta.env.PROD || !('serviceWorker' in navigator)) return
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('/mein-tag/sw.js', { scope: '/mein-tag/' }).catch(() => {
      // Ohne Worker geht alles, nur nicht offline. Kein Grund für eine Meldung.
    })
  })
}

/** Ein geteiltes Handy: Der gespeicherte Plan gehört der Person, die ihn geladen hat, auch wenn sie sich anderswo abgemeldet hat. */
export function bindOfflineDataTo(userId: string) {
  try {
    if (localStorage.getItem(OWNER_KEY) !== userId) clearOfflineData()
    localStorage.setItem(OWNER_KEY, userId)
  } catch {
    clearOfflineData()
  }
}

/** Nach Abmelden oder abgelaufener Anmeldung: gespeicherte Tagespläne und wartende Zählmengen löschen, damit am Gerät nichts liegen bleibt. */
export function clearOfflineData() {
  // Noch nicht gesendete Mengen einer Inventur gehören der Person, die gezählt hat.
  clearCountQueue()
  try {
    navigator.serviceWorker?.controller?.postMessage({ type: 'clear' })
  } catch {
    // Kein Worker aktiv.
  }
  // Zusätzlich direkt aus der Seite, falls der Worker gerade nicht steuert (z. B. erster Aufruf).
  if (typeof caches !== 'undefined') void caches.delete(DATA_CACHE).catch(() => undefined)
}
