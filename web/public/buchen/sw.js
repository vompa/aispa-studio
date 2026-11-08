/* Service Worker der Kunden-App (Geltungsbereich /buchen/), von Hand geschrieben und ohne Abhängigkeiten.

   Gespeichert wird nur die App selbst:
   - Die Seite: zuerst vom Netz, damit der Einbettungs-Header je Spa frisch bleibt (ADR 0080); ohne Netz aus dem Speicher. Je
     Adresse ein Eintrag, denn ?spa= gehört zur Antwort. Der Teil hinter dem # bleibt aus dem Schlüssel: Dort stehen die Codes
     aus Mail-Links, und sie bleiben nicht auf dem Gerät.
   - Die gebauten Dateien: aus dem Speicher, denn ihre Namen tragen einen Hash; die ältesten fallen über MAX_ASSETS heraus.
   Datenabrufe laufen nicht über diesen Worker: Termine, Gutscheine, Anmeldung und Gesundheitsfragebogen bleiben nicht auf dem
   Gerät. Deshalb muss beim Abmelden nichts gelöscht werden. */

const SHELL = 'aispa-buchen-shell-v1'
const MAX_ASSETS = 40

self.addEventListener('install', () => self.skipWaiting())

self.addEventListener('activate', (event) => {
  event.waitUntil(
    (async () => {
      const names = await caches.keys()
      await Promise.all(names.filter((n) => n.startsWith('aispa-buchen-shell-') && n !== SHELL).map((n) => caches.delete(n)))
      await self.clients.claim()
    })(),
  )
})

self.addEventListener('fetch', (event) => {
  const request = event.request
  if (request.method !== 'GET') return
  const url = new URL(request.url)
  if (url.origin !== self.location.origin) return

  if (request.mode === 'navigate' && url.pathname.startsWith('/buchen/')) event.respondWith(page(request))
  else if (url.pathname.startsWith('/assets/')) event.respondWith(asset(request))
})

async function page(request) {
  const cache = await caches.open(SHELL)
  const key = new URL(request.url)
  key.hash = ''
  try {
    const response = await fetch(request)
    if (response.ok) await cache.put(key.href, response.clone())
    return response
  } catch {
    return (await cache.match(key.href)) ?? new Response('Keine Verbindung.', { status: 503, headers: { 'Content-Type': 'text/plain; charset=utf-8' } })
  }
}

async function asset(request) {
  const cache = await caches.open(SHELL)
  const cached = await cache.match(request)
  if (cached) return cached
  const response = await fetch(request)
  if (response.ok) {
    await cache.put(request, response.clone())
    const assets = (await cache.keys()).filter((entry) => new URL(entry.url).pathname.startsWith('/assets/'))
    await Promise.all(assets.slice(0, -MAX_ASSETS).map((entry) => cache.delete(entry)))
  }
  return response
}
