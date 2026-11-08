/* Service Worker für „Mein Tag“ (Geltungsbereich /mein-tag/), von Hand geschrieben und ohne Abhängigkeiten.

   Was gespeichert wird:
   - Die App selbst (Seite /mein-tag/ und die gebauten Dateien unter /assets/): stale-while-revalidate im Cache
     SHELL. Die Dateinamen tragen einen Hash; deshalb lädt der Worker beim Aktualisieren der Seite die darin
     genannten Dateien gleich mit und wirft nicht mehr genannte weg.
   - Nur der eigene Tagesplan (GET /api/v1/reports/locations/{id}/day-plan, ältere Seiten ohne v1): network-first, ohne Netz aus dem Cache DATA.
     So lässt sich der Tag im Behandlungsraum auch ohne WLAN lesen. Der Tagesplan enthält Namen, Zeiten, Räume,
     Vorbereitung, Vorlieben und Notizen, aber keine Gesundheitsdaten: die Behandlungskarte trägt nur den Stand der
     Anamnese (z. B. „Fragebogen vom …“), nie die Antworten (die liegen verschlüsselt im Schema health).
     Dazu kommt das Allergie-Kennzeichen: nur ja oder nein, ohne Inhalt; es fällt mit dem Plan beim Abmelden weg.
   - Nichts sonst aus /api: Anmeldung, Melden, Anträge, Abhaken und Notizen gehen immer ans Netz.

   Abmelden oder eine abgelaufene Anmeldung (401/403) löscht DATA; die Seite schickt dazu {type: 'clear'}. */

const SHELL = 'aispa-mein-tag-shell-v1'
const DATA = 'aispa-mein-tag-data'
const START = '/mein-tag/'
/** Höchstens so viele Tage bleiben gespeichert; ältere fallen heraus. */
const MAX_DAYS = 28

// Beim ersten Aufruf lädt die Seite schon, bevor der Worker läuft; deshalb hier einmal Seite und Dateien holen.
self.addEventListener('install', (event) => {
  self.skipWaiting()
  event.waitUntil(
    (async () => {
      const cache = await caches.open(SHELL)
      const start = new URL(START, self.location.origin).href
      const response = await fetch(start, { cache: 'no-cache' })
      if (!response.ok) return
      await cache.put(start, response.clone())
      await refreshAssets(cache, await response.text())
    })().catch(() => undefined),
  )
})

self.addEventListener('activate', (event) => {
  event.waitUntil(
    (async () => {
      const names = await caches.keys()
      await Promise.all(names.filter((n) => n.startsWith('aispa-mein-tag-shell-') && n !== SHELL).map((n) => caches.delete(n)))
      await self.clients.claim()
    })(),
  )
})

self.addEventListener('message', (event) => {
  if (event.data && event.data.type === 'clear') event.waitUntil(caches.delete(DATA))
})

self.addEventListener('fetch', (event) => {
  const request = event.request
  if (request.method !== 'GET') return
  const url = new URL(request.url)
  if (url.origin !== self.location.origin) return

  if (request.mode === 'navigate' && url.pathname.startsWith(START)) {
    // Jede Seite im Geltungsbereich ist dieselbe App: ein Eintrag für alle, damit auch /mein-tag/?… offline startet.
    event.respondWith(shell(event, new Request(new URL(START, self.location.origin).href), true))
  } else if (url.pathname.startsWith('/assets/')) {
    event.respondWith(shell(event, request, false))
  } else if (/^\/api\/(v1\/)?reports\/locations\/[^/]+\/day-plan$/.test(url.pathname)) {
    event.respondWith(dayPlan(request))
  }
  // Alles andere (auch jedes andere /api) geht unverändert ans Netz.
})

async function shell(event, key, isPage) {
  const cache = await caches.open(SHELL)
  const cached = await cache.match(key)
  const update = fetch(key, isPage ? { cache: 'no-cache' } : undefined)
    .then(async (response) => {
      if (response.ok) {
        await cache.put(key, response.clone())
        if (isPage) await refreshAssets(cache, await response.clone().text())
      }
      return response
    })
    .catch(() => undefined)
  if (cached) {
    event.waitUntil(update)
    return cached
  }
  return (await update) ?? new Response('Keine Verbindung.', { status: 503, headers: { 'Content-Type': 'text/plain; charset=utf-8' } })
}

/** Lädt die von der Seite genannten Dateien vor und entfernt die nicht mehr genannten. */
async function refreshAssets(cache, html) {
  const wanted = new Set([...html.matchAll(/["'](\/assets\/[^"']+)["']/g)].map((m) => new URL(m[1], self.location.origin).href))
  for (const href of wanted) {
    if (!(await cache.match(href))) await cache.add(href).catch(() => undefined)
  }
  for (const request of await cache.keys()) {
    if (new URL(request.url).pathname.startsWith('/assets/') && !wanted.has(request.url)) await cache.delete(request)
  }
}

async function dayPlan(request) {
  const cache = await caches.open(DATA)
  try {
    const response = await fetch(request)
    if (response.ok) {
      const headers = new Headers(response.headers)
      headers.set('X-Aispa-Cached-At', new Date().toISOString())
      await cache.delete(request, { ignoreVary: true })
      await cache.put(request, new Response(await response.clone().blob(), { status: 200, headers }))
      const keys = await cache.keys()
      for (const old of keys.slice(0, Math.max(0, keys.length - MAX_DAYS))) await cache.delete(old)
    } else if (response.status === 401 || response.status === 403) {
      await caches.delete(DATA)
    }
    return response
  } catch (error) {
    const cached = await cache.match(request, { ignoreVary: true })
    if (!cached) throw error
    const headers = new Headers(cached.headers)
    headers.set('X-Aispa-Offline', '1')
    return new Response(cached.body, { status: 200, headers })
  }
}
