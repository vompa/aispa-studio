import { readFileSync, existsSync } from 'node:fs'
import { join } from 'node:path'
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import axe from 'axe-core'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { BookingApp } from './BookingApp'

type Reply = { status: number; body?: unknown }

const appointment = {
  id: 'ap-1',
  locationId: 'loc-1',
  locationName: 'Wien Innere Stadt',
  timeZone: 'Europe/Vienna',
  treatmentName: 'Klassische Massage',
  start: '2027-03-01T09:15:00Z',
  end: '2027-03-01T10:05:00Z',
  price: 79,
  currency: 'EUR',
  state: 'Booked',
  cancellableUntil: '2027-02-28T09:15:00Z',
  canCancel: true,
  canCancelLate: false,
  lateCancelFee: null,
  anamnesisSubmitted: false,
}
// Ein Termin in 12 Stunden bei 24 Stunden Frist: was das heißt, entscheidet die API.
const late = { ...appointment, canCancel: false, canCancelLate: true, lateCancelFee: 39.5 }
const session = { email: 'gast@example.at', firstName: 'Nora', lastName: 'Handy', phone: null, hasProfile: true, passkeyCount: 1 }

let routes: Record<string, () => Reply>
const requests: { method: string; url: string }[] = []

beforeEach(() => {
  window.location.hash = '#/termine'
  localStorage.setItem('aispa.sprache', 'de')
  requests.length = 0
  routes = {
    'GET /api/online/me': () => ({ status: 200, body: session }),
    'GET /api/online/me/appointments': () => ({ status: 200, body: [appointment] }),
    'GET /api/online/me/waitlist': () => ({ status: 200, body: [] }),
    'GET /api/online/me/vouchers': () => ({ status: 200, body: [] }),
  }
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: Request | string, init?: RequestInit) => {
      const request = typeof input === 'string' ? new Request(new URL(input, 'http://localhost'), init) : input
      const url = new URL(request.url)
      requests.push({ method: request.method, url: url.pathname + url.search })
      const reply = routes[`${request.method} ${url.pathname}`]?.() ?? { status: 404 }
      return new Response(reply.body === undefined ? null : JSON.stringify(reply.body), { status: reply.status, headers: { 'Content-Type': 'application/json' } })
    }),
  )
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

async function expectAccessible() {
  const result = await axe.run(document.body, {
    runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] },
    rules: { 'color-contrast': { enabled: false } },
  })
  expect(result.violations.map((v) => v.id)).toEqual([])
}

describe('Kunden-App', () => {
  it('Given ein Termin in 12 Stunden und Absagefrist 24 Stunden When der Gast absagen will Then zeigt die App die Gebühr aus der API vor dem Bestätigen', async () => {
    routes['GET /api/online/me/appointments'] = () => ({ status: 200, body: [late] })
    routes['POST /api/online/me/appointments/ap-1/cancel'] = () => ({ status: 200, body: { ...late, state: 'Cancelled', canCancelLate: false } })
    render(<BookingApp />)

    expect(await screen.findByText(/Eine Absage kostet jetzt € 39,50/)).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Termin stornieren' }))

    expect(screen.getByRole('group', { name: /Die Absage kostet €\s39,50.*Wirklich stornieren\?/ })).toBeTruthy()
    expect(requests.some((r) => r.method === 'POST')).toBe(false)
    await expectAccessible()

    fireEvent.click(screen.getByRole('button', { name: 'Ja, stornieren' }))
    await screen.findByText(/Der Termin ist storniert/)
    expect(requests.find((r) => r.method === 'POST')?.url).toBe('/api/online/me/appointments/ap-1/cancel?acceptFee=true')
  })

  it('Given die Frist läuft noch When der Gast absagt Then stimmt er keiner Gebühr zu', async () => {
    routes['POST /api/online/me/appointments/ap-1/cancel'] = () => ({ status: 200, body: { ...appointment, state: 'Cancelled', canCancel: false } })
    render(<BookingApp />)

    fireEvent.click(await screen.findByRole('button', { name: 'Termin stornieren' }))
    expect(screen.getByRole('group', { name: 'Wirklich stornieren?' })).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Ja, stornieren' }))

    await screen.findByText(/Der Termin ist storniert/)
    expect(requests.find((r) => r.method === 'POST')?.url).toBe('/api/online/me/appointments/ap-1/cancel?acceptFee=false')
  })

  it('Given die API erlaubt keine späte Absage When die Karte erscheint Then bleibt nur der Anruf', async () => {
    routes['GET /api/online/me/appointments'] = () => ({ status: 200, body: [{ ...appointment, canCancel: false }] })
    render(<BookingApp />)

    expect(await screen.findByText(/Bitte rufen Sie uns an/)).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Termin stornieren' })).toBeNull()
  })

  it('Given mehrere Termine When die App startet Then steht der nächste vor allen anderen', async () => {
    const later = { ...appointment, id: 'ap-2', treatmentName: 'Hot Stone', start: '2027-04-01T09:15:00Z', end: '2027-04-01T10:05:00Z' }
    // Die API liefert die neuesten zuerst.
    routes['GET /api/online/me/appointments'] = () => ({ status: 200, body: [later, appointment] })
    render(<BookingApp />)

    const next = (await screen.findByRole('heading', { name: 'Ihr nächster Termin' })).closest('section')!
    expect(within(next).getByText('Klassische Massage')).toBeTruthy()
    expect(within(next).queryByText('Hot Stone')).toBeNull()
    const rest = screen.getByRole('heading', { name: 'Kommende Termine' }).closest('section')!
    expect(within(rest).getByText('Hot Stone')).toBeTruthy()
    await expectAccessible()
  })

  it('Given ein Termin When der Gast „Zum Kalender hinzufügen“ wählt Then zeigt der Link auf die .ics der API', async () => {
    render(<BookingApp />)

    const link = await screen.findByRole('link', { name: 'Zum Kalender hinzufügen' })

    expect(link.getAttribute('href')).toBe('/api/online/me/appointments/ap-1/calendar.ics')
    expect(link.hasAttribute('download')).toBe(true)
  })

  it('Given ein Termin per Mail-Link When die Seite lädt Then zeigt der Kalender-Link auf den Code', async () => {
    routes['GET /api/online/me'] = () => ({ status: 401 })
    routes['GET /api/online/bookings/tok'] = () => ({ status: 200, body: appointment })
    window.location.hash = '#/termin/tok'
    render(<BookingApp />)

    const link = await screen.findByRole('link', { name: 'Zum Kalender hinzufügen' })

    expect(link.getAttribute('href')).toBe('/api/online/bookings/tok/calendar.ics')
  })

  it('Given eigene Gutscheine When die App startet Then stehen Restwert und Paketstand dort', async () => {
    routes['GET /api/online/me/vouchers'] = () => ({
      status: 200,
      body: [
        { code: 'K7QM-2XPA', kind: 'MultiPurpose', initialValue: 50, balance: 20, currency: 'EUR', status: 'Active', validUntil: null, treatmentName: null, units: null, unitsLeft: null },
        { code: 'PK10-AAAA', kind: 'SinglePurpose', initialValue: 500, balance: 300, currency: 'EUR', status: 'Active', validUntil: null, treatmentName: 'Hot Stone', units: 10, unitsLeft: 6 },
      ],
    })
    render(<BookingApp />)

    const section = (await screen.findByRole('heading', { name: 'Meine Gutscheine' })).closest('section')!

    expect(within(section).getByText(/Restwert € 20,00 von € 50,00/)).toBeTruthy()
    expect(within(section).getByText('Noch 6 von 10 Behandlungen')).toBeTruthy()
    expect(within(section).getByText(/K7QM-2XPA/)).toBeTruthy()
    await expectAccessible()
  })

  it('Given der Fragebogen fehlt When der Gast die Karte sieht Then führt ein Link zum Fragebogen', async () => {
    render(<BookingApp />)

    const link = await screen.findByRole('link', { name: 'Gesundheitsfragebogen ausfüllen' })

    expect(link.getAttribute('href')).toBe('#/anamnese')
  })

  it('Given ein Termin per Mail-Link When der Fragebogen fehlt Then gibt es keinen Link, der ein Konto verlangt', async () => {
    routes['GET /api/online/me'] = () => ({ status: 401 })
    routes['GET /api/online/bookings/tok'] = () => ({ status: 200, body: appointment })
    window.location.hash = '#/termin/tok'
    render(<BookingApp />)

    await screen.findByRole('link', { name: 'Zum Kalender hinzufügen' })

    expect(screen.queryByRole('link', { name: 'Gesundheitsfragebogen ausfüllen' })).toBeNull()
  })

  it('Given zwei Termine When der nächste abgesagt ist Then ersetzt eine neue Karte die alte', async () => {
    const later = { ...appointment, id: 'ap-2', treatmentName: 'Hot Stone', start: '2027-04-01T09:15:00Z', end: '2027-04-01T10:05:00Z' }
    // Die API liefert die neuesten zuerst.
    let list = [later, appointment]
    routes['GET /api/online/me/appointments'] = () => ({ status: 200, body: list })
    routes['POST /api/online/me/appointments/ap-1/cancel'] = () => {
      list = [later]
      return { status: 200, body: { ...appointment, state: 'Cancelled' } }
    }
    render(<BookingApp />)

    const heading = await screen.findByRole('heading', { name: 'Ihr nächster Termin' })
    const before = heading.closest('section')!.querySelector('.next-appointment')!
    fireEvent.click(within(before as HTMLElement).getByRole('button', { name: 'Termin stornieren' }))
    fireEvent.click(screen.getByRole('button', { name: 'Ja, stornieren' }))
    await screen.findByText(/Der Termin ist storniert/)
    await waitFor(() => expect(before.isConnected).toBe(false))

    const after = screen.getByRole('heading', { name: 'Ihr nächster Termin' }).closest('section')!
    expect(within(after).getByText('Hot Stone')).toBeTruthy()
  })

  it('Given nicht angemeldet When die App startet Then geht Anmelden und Buchen ohne Konto', async () => {
    routes['GET /api/online/me'] = () => ({ status: 401 })
    render(<BookingApp />)

    const main = await screen.findByRole('main')

    expect(await within(main).findByRole('link', { name: 'Anmelden' })).toBeTruthy()
    expect(within(main).getByRole('link', { name: 'Termin buchen' }).getAttribute('href')).toBe('#/')
  })
})

describe('Installierbar', () => {
  const web = join(import.meta.dirname, '..', '..')
  const publicDir = join(web, 'public', 'buchen')
  const manifest = JSON.parse(readFileSync(join(publicDir, 'manifest.webmanifest'), 'utf8')) as {
    start_url: string
    scope: string
    display: string
    icons: { src: string; sizes: string; purpose: string }[]
  }

  it('Given das Manifest When der Browser es liest Then startet die App bei Meine Termine im eigenen Bereich', () => {
    expect(manifest.start_url).toBe('/buchen/#/termine')
    expect(manifest.scope).toBe('/buchen/')
    expect(manifest.display).toBe('standalone')
  })

  it('Given das Manifest When die Symbole gelesen werden Then gibt es jede Datei und die verlangten Größen', () => {
    for (const icon of manifest.icons) expect(existsSync(join(web, 'public', icon.src))).toBe(true)
    expect(manifest.icons.some((i) => i.sizes === '192x192')).toBe(true)
    expect(manifest.icons.some((i) => i.sizes === '512x512' && i.purpose === 'maskable')).toBe(true)
  })

  it('Given die Seite When sie gebaut wird Then nennt sie das Manifest', () => {
    expect(readFileSync(join(web, 'buchen', 'index.html'), 'utf8')).toContain('href="/buchen/manifest.webmanifest"')
  })

  it('Given der Service Worker When er Abrufe sieht Then fasst er keine Daten aus der API an', () => {
    const code = readFileSync(join(publicDir, 'sw.js'), 'utf8').replace(/\/\*[\s\S]*?\*\//g, '')
    expect(code).not.toMatch(/\/api/)
    expect(code).toMatch(/startsWith\('\/assets\/'\)/)
  })

  /** Führt sw.js mit nachgestelltem Speicher aus; `online` entscheidet, ob das Netz antwortet. */
  function runWorker(online: { value: boolean }) {
    const listeners: Record<string, (event: unknown) => void> = {}
    const entries = new Map<string, Response>()
    const keyOf = (k: string | { url: string }) => (typeof k === 'string' ? k : k.url)
    const cache = {
      put: async (k: string | { url: string }, r: Response) => void entries.set(keyOf(k), r),
      match: async (k: string | { url: string }) => entries.get(keyOf(k))?.clone(),
      keys: async () => [...entries.keys()].map((url) => ({ url })),
      delete: async (k: string | { url: string }) => entries.delete(keyOf(k)),
    }
    const self = { location: { origin: 'https://spa.example' }, addEventListener: (n: string, f: (e: unknown) => void) => (listeners[n] = f), clients: {} }
    const network = async () => {
      if (!online.value) throw new TypeError('offline')
      return new Response('<html>')
    }
    new Function('self', 'caches', 'fetch', readFileSync(join(publicDir, 'sw.js'), 'utf8'))(self, { open: async () => cache, keys: async () => [] }, network)
    const request = (url: string, mode = 'no-cors') =>
      new Promise<Response>((resolve) => listeners.fetch({ request: { method: 'GET', mode, url }, respondWith: (p: Promise<Response>) => void p.then(resolve) }))
    return { entries, request }
  }

  it('Given ein Mail-Link When die Seite gespeichert wird Then bleibt der Code nicht auf dem Gerät und die Seite öffnet ohne Netz', async () => {
    const online = { value: true }
    const { entries, request } = runWorker(online)

    await request('https://spa.example/buchen/?spa=a#/termin/geheimer-code', 'navigate')

    expect([...entries.keys()]).toEqual(['https://spa.example/buchen/?spa=a'])
    online.value = false
    expect((await request('https://spa.example/buchen/?spa=a#/termine', 'navigate')).status).toBe(200)
  })

  it('Given viele gebaute Dateien When eine neue dazukommt Then bleiben nur die neuesten', async () => {
    const { entries, request } = runWorker({ value: true })

    for (let i = 0; i < 45; i++) await request(`https://spa.example/assets/f${i}.js`)

    expect(entries.size).toBe(40)
    expect(entries.has('https://spa.example/assets/f0.js')).toBe(false)
    expect(entries.has('https://spa.example/assets/f44.js')).toBe(true)
  })
})
