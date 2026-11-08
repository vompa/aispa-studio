import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import axe from 'axe-core'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { MyDayApp } from './MyDayApp'

type Reply = { status: number; body?: unknown; headers?: Record<string, string> }
type Handler = (request: Request) => Reply | Promise<Reply>

const me = {
  id: 'u-1',
  email: 'mia@example.at',
  displayName: 'Mia Masseurin',
  passkeyCount: 0,
  access: { tenantId: 't-1', tenantName: 'Spa Wien', role: 'Therapist', allLocations: false, locationIds: ['loc-1', 'loc-2'] },
  employeeId: 'emp-1',
  rights: ['ViewCalendar', 'ChangeStatus'],
}
const locations = [
  { id: 'loc-1', name: 'Wien Innere Stadt', timeZone: 'Europe/Vienna', currency: 'EUR', openingHours: [] },
  { id: 'loc-2', name: 'Baden', timeZone: 'Europe/Vienna', currency: 'EUR', openingHours: [] },
]
const preferences = {
  pressure: 'kräftig',
  scent: 'Lavendel',
  roomTemperature: null,
  music: null,
  prefersQuiet: true,
  focusAreas: 'Nacken',
  avoidAreas: null,
  other: null,
}
// Wiener Winterzeit (UTC+1): 10:00 und 13:00 Uhr.
const card = (id: string, start: string, end: string, treatmentName: string, customerName: string) => ({
  appointmentId: id,
  start,
  end,
  status: 'Booked',
  treatmentName,
  durationMinutes: 50,
  roomName: 'Raum Lotus',
  equipmentName: null,
  employeeName: 'Mia Masseurin',
  customerId: `c-${id}`,
  customerName,
  visitNumber: 3,
  lastVisit: '2027-02-01T09:00:00Z',
  occasion: null,
  bookingNote: null,
  anamnesisStatus: 'Fragebogen vom 01.02.2027',
  preferences,
  history: [{ start: '2027-02-01T09:00:00Z', treatmentName, employeeName: 'Mia Masseurin', note: 'Schulter links verspannt' }],
  todo: [
    { stepId: 'st-1', text: 'Liege anwärmen', dueAt: '2027-03-01T08:45:00Z', source: 'Rezept', done: false },
    { stepId: 'st-2', text: 'Öl vorbereiten', dueAt: null, source: 'Rezept', done: true },
  ],
  followUpNote: null,
})
const plan = {
  employeeId: 'emp-1',
  employeeName: 'Mia Masseurin',
  date: '2027-03-01',
  shifts: [{ start: '2027-03-01T08:00:00Z', end: '2027-03-01T16:00:00Z' }],
  // Absichtlich nicht nach Zeit sortiert.
  cards: [
    card('ap-2', '2027-03-01T12:00:00Z', '2027-03-01T12:50:00Z', 'Hot Stone', 'Karl Maria Huber'),
    card('ap-1', '2027-03-01T09:00:00Z', '2027-03-01T09:50:00Z', 'Klassische Massage', 'Lena Gast'),
  ],
  hints: [],
}

// Raumcheck (ADR 0068): nach dem Abschluss stehen die Materialien des Raumschranks unter der Mindestmenge auf der Karte.
const oilLine = { itemId: 'm-1', itemName: 'Massageöl neutral', unit: 'ml', quantity: 200, minimum: 500, suggested: 300, state: 'Open' }
const towelLine = { itemId: 'm-2', itemName: 'Handtuch', unit: 'Stk', quantity: 4, minimum: 6, suggested: 2, state: 'Open' }
const completedPlan = () => ({
  ...plan,
  cards: [{ ...plan.cards[1], status: 'Completed', restock: [oilLine, towelLine] }, plan.cards[0]],
})

let routes: Record<string, Handler>
const requests: { method: string; path: string; query: string; body: unknown }[] = []

function stubFetch() {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: Request | string, init?: RequestInit) => {
      const request = typeof input === 'string' ? new Request(new URL(input, 'http://localhost'), init) : input
      const url = new URL(request.url)
      const text = request.method === 'GET' ? '' : await request.clone().text()
      requests.push({ method: request.method, path: url.pathname, query: url.search, body: text ? JSON.parse(text) : undefined })
      const handler = routes[`${request.method} ${url.pathname}`]
      const reply = handler ? await handler(request) : { status: 404 }
      return new Response(reply.body === undefined ? null : JSON.stringify(reply.body), {
        status: reply.status,
        headers: { 'Content-Type': 'application/json', ...reply.headers },
      })
    }),
  )
}

/** WCAG 2.1 A/AA mit axe-core (ohne Kontrast: jsdom rechnet keine Farben). */
async function expectAccessible() {
  const result = await axe.run(document.body, {
    runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] },
    rules: { 'color-contrast': { enabled: false } },
  })
  expect(result.violations.map((v) => `${v.id}: ${v.nodes.map((n) => n.html).join(' | ')}`)).toEqual([])
}

beforeEach(() => {
  // 09:30 Uhr in Wien: die Massage um 10:00 ist die nächste.
  vi.useFakeTimers({ toFake: ['Date'] })
  vi.setSystemTime(new Date('2027-03-01T08:30:00Z'))
  localStorage.clear()
  requests.length = 0
  routes = {
    'GET /api/v1/auth/me': () => ({ status: 200, body: me }),
    'GET /api/v1/locations': () => ({ status: 200, body: locations }),
    'POST /api/v1/auth/channel/staff-app': () => ({ status: 200, body: { ...me, channel: 'StaffApp' } }),
    'GET /api/v1/absence-requests/mine': () => ({ status: 200, body: [] }),
    'GET /api/v1/reports/locations/loc-1/day-plan': () => ({ status: 200, body: plan }),
    'GET /api/v1/reports/locations/loc-2/day-plan': () => ({ status: 200, body: { ...plan, shifts: [], cards: [] } }),
  }
  stubFetch()
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
  vi.useRealTimers()
})

describe('Mein Tag', () => {
  it('zeigt den eigenen Tagesplan in zeitlicher Reihenfolge und hebt den nächsten Termin hervor', async () => {
    render(<MyDayApp />)

    const list = await screen.findByRole('list', { name: 'Termine' })
    const cards = within(list).getAllByRole('article')
    expect(cards.map((c) => within(c).getByRole('heading', { level: 3 }).textContent)).toEqual(['10:00 Klassische Massage', '13:00 Hot Stone'])
    expect(screen.getByRole('heading', { level: 2, name: /^Heute, Montag, 1\. März/ })).toBeTruthy()
    expect(screen.getByText('Dienst 09:00–17:00')).toBeTruthy()

    const first = cards[0]
    expect(within(first).getByText('Als Nächstes')).toBeTruthy()
    expect(first.getAttribute('aria-current')).toBe('time')
    expect(cards[1].getAttribute('aria-current')).toBeNull()
    // Nur Vorname und Initiale des Nachnamens.
    expect(within(first).getByText('Lena G.')).toBeTruthy()
    expect(within(cards[1]).getByText('Karl H.')).toBeTruthy()
    expect(within(first).getByText('kräftig')).toBeTruthy()
    expect(within(first).getByText('Lieber wenig')).toBeTruthy()
    expect(within(first).getByText('Schulter links verspannt')).toBeTruthy()

    // Eigener Plan: ohne employeeId, mit dem Tag in der Zeitzone des Standorts.
    const load = requests.find((r) => r.path === '/api/v1/reports/locations/loc-1/day-plan')
    expect(load?.query).toBe('?date=2027-03-01')
    await expectAccessible()
  })

  it('hakt einen Vorbereitungsschritt ab und nimmt ihn bei einem Fehler zurück', async () => {
    routes['PUT /api/v1/reports/appointments/ap-1/preparation/st-1'] = () => ({ status: 204 })
    routes['PUT /api/v1/reports/appointments/ap-1/preparation/st-2'] = () => ({ status: 500 })
    render(<MyDayApp />)

    const first = (await screen.findAllByRole('article'))[0]
    const warm = within(first).getByRole('checkbox', { name: /Liege anwärmen/ }) as HTMLInputElement
    fireEvent.click(warm)
    expect(warm.checked).toBe(true)
    await waitFor(() =>
      expect(requests.find((r) => r.method === 'PUT' && r.path === '/api/v1/reports/appointments/ap-1/preparation/st-1')?.body).toEqual({ done: true }),
    )
    expect(within(first).getByText('(2 von 2 erledigt)')).toBeTruthy()

    const oil = within(first).getByRole('checkbox', { name: /Öl vorbereiten/ }) as HTMLInputElement
    fireEvent.click(oil)
    expect(oil.checked).toBe(false)
    await waitFor(() => expect(oil.checked).toBe(true))
    expect(within(first).getByRole('status').textContent).toContain('„Öl vorbereiten“ konnte nicht gespeichert werden')
  })

  it('zeigt nach dem Abschluss den Raumcheck und beantwortet die Zeilen mit Aufgefüllt und Reicht noch', async () => {
    routes['GET /api/v1/reports/locations/loc-1/day-plan'] = () => ({ status: 200, body: completedPlan() })
    routes['PUT /api/v1/appointments/ap-1/room-care/restock/m-1'] = () => ({
      status: 200,
      body: { appointmentId: 'ap-1', restock: [{ ...oilLine, quantity: 450, suggested: 50, state: 'Refilled' }, towelLine] },
    })
    routes['PUT /api/v1/appointments/ap-1/room-care/restock/m-2'] = () => ({
      status: 200,
      body: { appointmentId: 'ap-1', restock: [{ ...oilLine, quantity: 450, suggested: 50, state: 'Refilled' }, { ...towelLine, state: 'Enough' }] },
    })
    render(<MyDayApp />)

    const [first, second] = await screen.findAllByRole('article')
    expect(within(first).getByRole('heading', { level: 4, name: /Raum prüfen/ }).textContent).toContain('2 offen')
    expect(within(first).getByText('Massageöl neutral: 200 von 500 ml, 300 ml nachfüllen')).toBeTruthy()
    expect(within(first).getByText('Handtuch: 4 von 6 Stk, 2 Stk nachfüllen')).toBeTruthy()
    // Erst nach dem Abschluss.
    expect(within(second).queryByText(/Raum prüfen/)).toBeNull()
    expect((within(first).getByLabelText('Menge (ml)') as HTMLInputElement).value).toBe('300')
    await expectAccessible()

    fireEvent.change(within(first).getByLabelText('Menge (ml)'), { target: { value: '250' } })
    fireEvent.click(within(first).getByRole('button', { name: 'Aufgefüllt: Massageöl neutral' }))
    await waitFor(() => expect(within(first).getByText('aufgefüllt')).toBeTruthy())
    expect(requests.find((r) => r.method === 'PUT' && r.path.endsWith('/restock/m-1'))?.body).toEqual({ state: 'Refilled', quantity: 250 })
    expect(within(first).getByText('Massageöl neutral: 450 von 500 ml')).toBeTruthy()
    expect(within(first).getByRole('heading', { level: 4, name: /Raum prüfen/ }).textContent).toContain('1 offen')

    fireEvent.click(within(first).getByRole('button', { name: 'Reicht noch: Handtuch' }))
    await waitFor(() => expect(within(first).getByText('reicht')).toBeTruthy())
    expect(requests.find((r) => r.method === 'PUT' && r.path.endsWith('/restock/m-2'))?.body).toEqual({ state: 'Enough', quantity: null })
    expect(within(first).queryByRole('button', { name: /Aufgefüllt/ })).toBeNull()
    await expectAccessible()
  })

  it('verlangt beim Raumcheck eine Menge und zeigt die Absage des Servers', async () => {
    routes['GET /api/v1/reports/locations/loc-1/day-plan'] = () => ({ status: 200, body: completedPlan() })
    routes['PUT /api/v1/appointments/ap-1/room-care/restock/m-2'] = () => ({ status: 400, body: { errors: { itemId: ['Diese Zeile ist schon als aufgefüllt gebucht.'] } } })
    render(<MyDayApp />)

    const first = (await screen.findAllByRole('article'))[0]
    fireEvent.change(within(first).getByLabelText('Menge (ml)'), { target: { value: '' } })
    fireEvent.click(within(first).getByRole('button', { name: 'Aufgefüllt: Massageöl neutral' }))
    expect(within(first).getByRole('status').textContent).toBe('Menge für Massageöl neutral angeben.')
    expect(requests.some((r) => r.method === 'PUT')).toBe(false)

    fireEvent.click(within(first).getByRole('button', { name: 'Reicht noch: Handtuch' }))
    expect(await within(first).findByText('Diese Zeile ist schon als aufgefüllt gebucht.')).toBeTruthy()
  })

  it('zeigt den Raumcheck ohne Netz nur an', async () => {
    routes['GET /api/v1/reports/locations/loc-1/day-plan'] = () => ({
      status: 200,
      body: completedPlan(),
      headers: { 'X-Aispa-Offline': '1', 'X-Aispa-Cached-At': '2027-03-01T07:12:00Z' },
    })
    render(<MyDayApp />)

    const first = (await screen.findAllByRole('article'))[0]
    const button = within(first).getByRole('button', { name: 'Aufgefüllt: Massageöl neutral' })
    expect(button.getAttribute('aria-disabled')).toBe('true')
    fireEvent.click(button)
    expect(within(first).getByRole('status').textContent).toBe('Nur mit Verbindung.')
    await new Promise((r) => setTimeout(r, 0))
    expect(requests.some((r) => r.method === 'PUT')).toBe(false)
  })

  it('speichert die Notiz für die nächste Behandlung', async () => {
    routes['PUT /api/v1/reports/appointments/ap-1/follow-up'] = () => ({ status: 204 })
    render(<MyDayApp />)

    const first = (await screen.findAllByRole('article'))[0]
    fireEvent.change(within(first).getByLabelText('Notiz für die nächste Behandlung'), { target: { value: 'Nächstes Mal mehr Nacken.' } })
    fireEvent.click(within(first).getByRole('button', { name: 'Notiz speichern' }))
    expect(await within(first).findByText('Notiz gespeichert.')).toBeTruthy()
    expect(requests.find((r) => r.method === 'PUT' && r.path.endsWith('/follow-up'))?.body).toEqual({ note: 'Nächstes Mal mehr Nacken.' })
  })

  // T-015 (Auftraggeber, 05.11.2026): keine Zeiterfassung per Handy; Arbeitszeiten trägt die Leitung in der Windows-App ein.
  it('zeigt keine Stempeluhr und fragt die Zeiterfassung nicht ab', async () => {
    render(<MyDayApp />)

    expect(await screen.findByText('Klassische Massage')).toBeTruthy()
    expect(screen.queryByRole('region', { name: 'Zeiterfassung' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Kommen' })).toBeNull()
    expect(requests.some((r) => r.path.startsWith('/api/v1/time/'))).toBe(false)
  })

  it('merkt sich den gewählten Standort', async () => {
    render(<MyDayApp />)
    fireEvent.change(await screen.findByLabelText('Standort'), { target: { value: 'loc-2' } })
    expect(await screen.findByText('Heute keine Termine')).toBeTruthy()
    expect(JSON.parse(localStorage.getItem('aispa.meinTag.standort')!)).toMatchObject({ id: 'loc-2' })

    cleanup()
    render(<MyDayApp />)
    expect(await screen.findByText('Heute keine Termine')).toBeTruthy()
    expect((screen.getByLabelText('Standort') as HTMLSelectElement).value).toBe('loc-2')
  })

  it('blättert zum Folgetag und wieder zurück zu heute', async () => {
    render(<MyDayApp />)
    await screen.findAllByRole('article')
    fireEvent.click(screen.getByRole('button', { name: 'Folgetag ›' }))
    await waitFor(() => expect(requests.some((r) => r.query === '?date=2027-03-02')).toBe(true))
    expect(await screen.findByRole('heading', { level: 2, name: /^Dienstag, 2\. März/ })).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Heute' }))
    expect(await screen.findByRole('heading', { level: 2, name: /^Heute, Montag/ })).toBeTruthy()
  })

  it('zeigt den Stand aus dem Speicher und sperrt das Abhaken', async () => {
    routes['GET /api/v1/reports/locations/loc-1/day-plan'] = () => ({
      status: 200,
      body: plan,
      headers: { 'X-Aispa-Offline': '1', 'X-Aispa-Cached-At': '2027-03-01T07:12:00Z' },
    })
    render(<MyDayApp />)

    expect(await screen.findByText('Offline, Stand 08:12')).toBeTruthy()
    const first = screen.getAllByRole('article')[0]
    const warm = within(first).getByRole('checkbox', { name: /Liege anwärmen/ }) as HTMLInputElement
    expect(warm.getAttribute('aria-disabled')).toBe('true')
    fireEvent.click(warm)
    expect(warm.checked).toBe(false)
    expect(within(first).getByRole('button', { name: 'Notiz speichern' }).getAttribute('aria-disabled')).toBe('true')
    await new Promise((r) => setTimeout(r, 0))
    expect(requests.some((r) => r.method === 'PUT')).toBe(false)
    await expectAccessible()
  })

  it('Gegeben Lena meldet begonnen, dann beendet: die API bekommt beides, die Karte zeigt den Stand vom Server', async () => {
    let current = plan
    const withStatus = (status: string, startedAt: string | null) => ({ ...plan, cards: [plan.cards[0], { ...plan.cards[1], status, startedAt }] })
    routes['GET /api/v1/reports/locations/loc-1/day-plan'] = () => ({ status: 200, body: current })
    routes['POST /api/v1/appointments/ap-1/begin'] = () => {
      current = withStatus('CheckedIn', '2027-03-01T08:31:00Z')
      return { status: 200, body: { id: 'ap-1' } }
    }
    routes['POST /api/v1/appointments/ap-1/finish'] = () => {
      current = withStatus('Completed', '2027-03-01T08:31:00Z')
      return { status: 200, body: { id: 'ap-1' } }
    }
    render(<MyDayApp />)

    const massage = await screen.findByRole('article', { name: /Klassische Massage/ })
    fireEvent.click(within(massage).getByRole('button', { name: 'Begonnen melden' }))
    expect(await within(massage).findByText('Läuft seit 09:31')).toBeTruthy()

    fireEvent.click(within(massage).getByRole('button', { name: 'Beendet melden' }))
    expect(await within(massage).findByText('Abgeschlossen')).toBeTruthy()
    expect(within(massage).queryByRole('button', { name: /melden/ })).toBeNull()
    expect(requests.filter((r) => r.method === 'POST' && r.path.startsWith('/api/v1/appointments')).map((r) => r.path)).toEqual([
      '/api/v1/appointments/ap-1/begin',
      '/api/v1/appointments/ap-1/finish',
    ])
    await expectAccessible()
  })

  it('zeigt die Absage der API beim Melden an der Karte', async () => {
    routes['POST /api/v1/appointments/ap-1/begin'] = () => ({ status: 400, body: { errors: { status: ['Begonnen lässt sich nur am Tag des Termins melden.'] } } })
    render(<MyDayApp />)

    const massage = await screen.findByRole('article', { name: /Klassische Massage/ })
    fireEvent.click(within(massage).getByRole('button', { name: 'Begonnen melden' }))
    expect(await within(massage).findByText('Begonnen lässt sich nur am Tag des Termins melden.')).toBeTruthy()
  })

  it('bietet Melden nur für den heutigen Tag und nicht ohne Netz an', async () => {
    routes['GET /api/v1/reports/locations/loc-1/day-plan'] = () => ({
      status: 200,
      body: plan,
      headers: { 'X-Aispa-Offline': '1', 'X-Aispa-Cached-At': '2027-03-01T07:12:00Z' },
    })
    render(<MyDayApp />)

    const massage = await screen.findByRole('article', { name: /Klassische Massage/ })
    const button = within(massage).getByRole('button', { name: 'Begonnen melden' })
    expect(button.getAttribute('aria-disabled')).toBe('true')
    fireEvent.click(button)
    expect(await within(massage).findByText('Nur mit Verbindung.')).toBeTruthy()
    expect(requests.some((r) => r.method === 'POST' && r.path.startsWith('/api/v1/appointments'))).toBe(false)

    fireEvent.click(screen.getByRole('button', { name: 'Folgetag ›' }))
    await screen.findByRole('heading', { name: /Dienstag/ })
    expect(screen.queryByRole('button', { name: 'Begonnen melden' })).toBeNull()
  })

  it('zeigt das Allergie-Kennzeichen nur als Hinweis ohne Inhalt', async () => {
    routes['GET /api/v1/reports/locations/loc-1/day-plan'] = () => ({ status: 200, body: { ...plan, cards: [plan.cards[0], { ...plan.cards[1], allergyFlag: true }] } })
    render(<MyDayApp />)

    const massage = await screen.findByRole('article', { name: /Klassische Massage/ })
    expect(within(massage).getByText('Allergie angegeben: bitte den Fragebogen beachten.')).toBeTruthy()
    expect(screen.getAllByText(/Allergie angegeben/)).toHaveLength(1)
  })

  it('wechselt nach der Anmeldung in den Kanal der Mitarbeiter-App', async () => {
    render(<MyDayApp />)
    await screen.findByRole('heading', { name: /Heute/ })
    expect(requests.some((r) => r.method === 'POST' && r.path === '/api/v1/auth/channel/staff-app')).toBe(true)
  })

  it('zeigt die Woche mit Diensten und Terminen und öffnet einen Tag', async () => {
    render(<MyDayApp />)
    fireEvent.click(await screen.findByRole('button', { name: 'Woche' }))

    const monday = await screen.findByRole('button', { name: /Montag, 1\. März/ })
    expect(monday.textContent).toContain('2 Termine')
    expect(monday.getAttribute('aria-current')).toBe('date')
    await expectAccessible()

    fireEvent.click(screen.getByRole('button', { name: /Mittwoch, 3\. März/ }))
    expect(await screen.findByRole('heading', { name: /Mittwoch, 3\. März/ })).toBeTruthy()
  })

  it('räumt den gespeicherten Plan, wenn eine andere Person sich am selben Handy anmeldet', async () => {
    const deleted = vi.fn(async () => true)
    vi.stubGlobal('caches', { delete: deleted })
    localStorage.setItem('aispa-mein-tag-owner', 'eine-andere-person')
    render(<MyDayApp />)

    await screen.findByRole('heading', { name: /Heute/ })
    expect(deleted).toHaveBeenCalledWith('aispa-mein-tag-data')
    expect(localStorage.getItem('aispa-mein-tag-owner')).toBe(me.id)
    deleted.mockClear()
    cleanup()
    render(<MyDayApp />)
    await screen.findByRole('heading', { name: /Heute/ })
    expect(deleted).not.toHaveBeenCalled()
    vi.unstubAllGlobals()
  })

  it('zeigt einen Fehler beim Laden der Anträge und lässt das Absenden nach Netzrückkehr zu', async () => {
    let online = false
    routes['GET /api/v1/absence-requests/mine'] = () => {
      if (!online) throw new TypeError('Failed to fetch')
      return { status: 200, body: [] }
    }
    routes['POST /api/v1/absence-requests'] = () => ({ status: 200, body: { id: 'r-1' } })
    render(<MyDayApp />)
    fireEvent.click(await screen.findByRole('button', { name: 'Abwesenheit' }))
    expect(await screen.findByText('Anträge gehen nur mit Verbindung.')).toBeTruthy()

    online = true
    fireEvent.click(screen.getByRole('button', { name: 'Antrag stellen' }))
    expect(await screen.findByText(/Antrag gestellt/)).toBeTruthy()
    await waitFor(() => expect(screen.queryByText('Anträge gehen nur mit Verbindung.')).toBeNull())
  })

  it('nennt in der Woche ohne Netz den Stand', async () => {
    routes['GET /api/v1/reports/locations/loc-1/day-plan'] = () => ({ status: 200, body: plan, headers: { 'X-Aispa-Offline': '1', 'X-Aispa-Cached-At': '2027-03-01T07:12:00Z' } })
    render(<MyDayApp />)
    fireEvent.click(await screen.findByRole('button', { name: 'Woche' }))

    await screen.findByRole('heading', { name: /Woche ab/ })
    expect(await screen.findByText('Offline, Stand 08:12')).toBeTruthy()
  })

  it('Gegeben Lena beantragt Freitag frei: der Antrag geht an die API und steht danach mit Stand in der Liste', async () => {
    const mine: unknown[] = []
    routes['GET /api/v1/absence-requests/mine'] = () => ({ status: 200, body: mine })
    routes['POST /api/v1/absence-requests'] = () => {
      mine.push({ id: 'r-1', kind: 'DayOff', from: '2027-03-05', until: '2027-03-05', note: null, requestedAt: '2027-03-01T08:30:00Z', state: 'Open', decidedAt: null })
      return { status: 200, body: mine[0] }
    }
    render(<MyDayApp />)
    fireEvent.click(await screen.findByRole('button', { name: 'Abwesenheit' }))
    expect(await screen.findByText('Noch keine Anträge')).toBeTruthy()

    fireEvent.change(screen.getByLabelText('Art'), { target: { value: 'DayOff' } })
    fireEvent.change(screen.getByLabelText('Von'), { target: { value: '2027-03-05' } })
    expect((screen.getByLabelText('Bis') as HTMLInputElement).value).toBe('2027-03-05')
    await expectAccessible()
    fireEvent.click(screen.getByRole('button', { name: 'Antrag stellen' }))

    expect(await screen.findByText('Offen')).toBeTruthy()
    expect(screen.getByText(/5\. März/)).toBeTruthy()
  })

  it('Gegeben die Leitung hat entschieden: Lena sieht Genehmigt und Abgelehnt', async () => {
    routes['GET /api/v1/absence-requests/mine'] = () => ({
      status: 200,
      body: [
        { id: 'r-1', kind: 'Vacation', from: '2027-03-08', until: '2027-03-12', note: null, requestedAt: '2027-03-01T08:00:00Z', state: 'Approved', decidedAt: '2027-03-01T08:10:00Z' },
        { id: 'r-2', kind: 'DayOff', from: '2027-03-05', until: '2027-03-05', note: null, requestedAt: '2027-02-28T08:00:00Z', state: 'Rejected', decidedAt: '2027-02-28T09:00:00Z' },
      ],
    })
    render(<MyDayApp />)
    fireEvent.click(await screen.findByRole('button', { name: 'Abwesenheit' }))

    expect(await screen.findByText('Genehmigt')).toBeTruthy()
    expect(screen.getByText('Abgelehnt')).toBeTruthy()
  })

  it('zeigt die Absage der API beim Antrag', async () => {
    routes['POST /api/v1/absence-requests'] = () => ({ status: 400, body: { errors: { from: ['Für diesen Zeitraum gibt es schon einen offenen Antrag.'] } } })
    render(<MyDayApp />)
    fireEvent.click(await screen.findByRole('button', { name: 'Abwesenheit' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Antrag stellen' }))
    expect(await screen.findByText('Für diesen Zeitraum gibt es schon einen offenen Antrag.')).toBeTruthy()
  })

  it('zeigt einen abgebrochenen Passkey als Meldung mit dem Weg über das Passwort', async () => {
    routes['GET /api/v1/auth/me'] = () => ({ status: 401 })
    routes['POST /api/v1/auth/passkey/request-options'] = () => ({ status: 200, body: {} })
    vi.stubGlobal('PublicKeyCredential', { parseRequestOptionsFromJSON: () => ({}) })
    render(<MyDayApp />)

    fireEvent.click(await screen.findByRole('button', { name: 'Mit Passkey anmelden' }))
    expect((await screen.findByRole('alert')).textContent).toContain('mit Passwort anmelden')
    vi.unstubAllGlobals()
  })

  it('zeigt ohne Anmeldung den Weg zur Anmeldung', async () => {
    routes['GET /api/v1/auth/me'] = () => ({ status: 401 })
    render(<MyDayApp />)

    expect(await screen.findByRole('heading', { name: 'Bitte anmelden' })).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Mit Passwort anmelden' })).toBeTruthy()
    expect(requests.some((r) => r.path.includes('day-plan'))).toBe(false)
    await expectAccessible()
  })

  it('Mit_Passwort_anmelden_fuehrt_auf_anmelden_mit_Rueckkehr', async () => {
    routes['GET /api/v1/auth/me'] = () => ({ status: 401 })
    render(<MyDayApp />)

    const link = await screen.findByRole('link', { name: 'Mit Passwort anmelden' })
    expect(link.getAttribute('href')).toBe('/anmelden/?zurueck=/mein-tag/')
  })

  it('meldet sich bei abgelaufener Anmeldung ab', async () => {
    routes['GET /api/v1/reports/locations/loc-1/day-plan'] = () => ({ status: 401 })
    render(<MyDayApp />)
    expect(await screen.findByRole('heading', { name: 'Bitte anmelden' })).toBeTruthy()
  })

  it('zeigt ohne Netz den zuletzt gespeicherten Standort', async () => {
    localStorage.setItem('aispa.meinTag.standort', JSON.stringify({ id: 'loc-1', name: 'Wien Innere Stadt', timeZone: 'Europe/Vienna' }))
    routes['GET /api/v1/auth/me'] = () => {
      throw new TypeError('Failed to fetch')
    }
    routes['GET /api/v1/reports/locations/loc-1/day-plan'] = () => ({
      status: 200,
      body: plan,
      headers: { 'X-Aispa-Offline': '1', 'X-Aispa-Cached-At': '2027-02-28T17:05:00Z' },
    })
    render(<MyDayApp />)

    expect(await screen.findByText('Offline, Stand 28.2.2027, 18:05')).toBeTruthy()
    expect(screen.queryByText('Zeiterfassung braucht eine Verbindung.')).toBeNull()
    expect(screen.queryByRole('button', { name: 'Abmelden' })).toBeNull()
  })

  // Slice 117 (Ä274): eigene Zertifikats-Erinnerungen auf „Heute“; dass andere nicht aufs Handy kommen, entscheidet die API (StaffDutyTests).
  it('zeigt eigene Zertifikats-Erinnerungen unter „Erinnerungen“ auf Heute', async () => {
    routes['GET /api/v1/staff-duties/reminders'] = () => ({
      status: 200,
      body: [
        { kind: 'CertificateExpiring', title: 'Zertifikat läuft ab', text: 'Erste-Hilfe-Kurs läuft am 31.12. ab', employeeId: 'emp-1', locationId: null, date: '2027-12-31' },
      ],
    })
    render(<MyDayApp />)

    const list = await screen.findByRole('list', { name: 'Erinnerungen' })
    expect(within(list).getAllByRole('listitem').map((i) => i.textContent)).toEqual(['Erste-Hilfe-Kurs läuft am 31.12. ab'])
    expect(screen.getByRole('heading', { level: 3, name: 'Erinnerungen' })).toBeTruthy()
    await expectAccessible()
  })

  it('zeigt ohne Erinnerungen oder bei einem Fehler keine Liste und bleibt nutzbar', async () => {
    routes['GET /api/v1/staff-duties/reminders'] = () => ({ status: 500 })
    render(<MyDayApp />)

    await screen.findByRole('list', { name: 'Termine' })
    await waitFor(() => expect(requests.some((r) => r.path === '/api/v1/staff-duties/reminders')).toBe(true))
    expect(screen.queryByRole('list', { name: 'Erinnerungen' })).toBeNull()
    expect(screen.queryByRole('alert')).toBeNull()
  })
})

// Kennzahlen des Tages (Slice 109): die Leitung sieht den Bericht des Tages, wer das Recht nicht hat, gar nichts davon.
const owner = { ...me, displayName: 'Ida Inhaberin', access: { ...me.access, role: 'Owner' }, rights: ['ViewCalendar', 'ViewFinance', 'ViewAmountReports'] }
const kpi = (key: string, label: string, value: number, unit: string) => ({ key, label, value, previous: null, unit })
const todayReport = (over: Record<string, unknown> = {}) => ({
  locationId: 'loc-1',
  from: '2027-03-01',
  until: '2027-03-01',
  kpis: [
    kpi('revenue', 'Umsatz brutto', 3200, '€'),
    kpi('staffUtilization', 'Auslastung Personal', 72, '%'),
    kpi('roomUtilization', 'Auslastung Räume', 64, '%'),
    kpi('openRequests', 'Offene Anfragen (heute)', 4, ''),
  ],
  revenueByAccount: [],
  revenueByPayment: [],
  employees: [],
  treatments: [],
  alerts: [],
  amountsHidden: false,
  noShows: 1,
  cancellations: 2,
  ...over,
})
const managementPath = 'GET /api/v1/reports/locations/loc-1/management'

describe('Heute im Spa', () => {
  it('gibt der Rezeption keine Seite „Heute im Spa“ und fragt den Bericht nicht ab', async () => {
    routes['GET /api/v1/auth/me'] = () => ({ status: 200, body: { ...me, rights: ['ViewCalendar', 'Book', 'ChangeStatus'] } })
    render(<MyDayApp />)

    await screen.findByRole('list', { name: 'Termine' })
    expect(screen.queryByRole('button', { name: 'Heute im Spa' })).toBeNull()
    expect(requests.some((r) => r.path.endsWith('/management'))).toBe(false)
  })

  it('zeigt der Inhaberin den Umsatz laut Kassa von heute', async () => {
    routes['GET /api/v1/auth/me'] = () => ({ status: 200, body: owner })
    routes[managementPath] = () => ({ status: 200, body: todayReport() })
    render(<MyDayApp />)

    fireEvent.click(await screen.findByRole('button', { name: 'Heute im Spa' }))

    const revenue = await screen.findByText(/3\.200,00/)
    expect(revenue.textContent).toContain('€')
    const load = requests.find((r) => r.path.endsWith('/management'))
    expect(load?.query).toBe('?from=2027-03-01&until=2027-03-01')
    await expectAccessible()
  })

  it('zeigt Auslastung, Absagen und offene Anfragen aus dem Bericht', async () => {
    routes['GET /api/v1/auth/me'] = () => ({ status: 200, body: owner })
    routes[managementPath] = () => ({ status: 200, body: todayReport() })
    render(<MyDayApp />)

    fireEvent.click(await screen.findByRole('button', { name: 'Heute im Spa' }))

    const figures = await screen.findByRole('list', { name: 'Kennzahlen des Tages' })
    const texts = within(figures).getAllByRole('listitem').map((li) => li.textContent)
    expect(texts).toContainEqual(expect.stringMatching(/Auslastung Personal.*72\s?%/))
    expect(texts).toContainEqual(expect.stringMatching(/Auslastung Räume.*64\s?%/))
    expect(texts).toContainEqual(expect.stringMatching(/Absagen.*2/))
    expect(texts).toContainEqual(expect.stringMatching(/Nicht erschienen.*1/))
    expect(texts).toContainEqual(expect.stringMatching(/Offene Anfragen.*4/))
  })

  it('zeigt ohne das Recht auf Beträge keinen Umsatz, sondern den Hinweis', async () => {
    routes['GET /api/v1/auth/me'] = () => ({ status: 200, body: { ...owner, rights: ['ViewCalendar', 'ViewFinance'] } })
    routes[managementPath] = () => ({
      status: 200,
      // Der Umsatz steht absichtlich noch im Stub: die Seite darf ihn bei amountsHidden nicht zeigen, auch wenn die API ihn schickte.
      body: todayReport({ amountsHidden: true }),
    })
    render(<MyDayApp />)

    fireEvent.click(await screen.findByRole('button', { name: 'Heute im Spa' }))

    expect(await screen.findByText(/Beträge sehen nur Personen mit dem Recht/)).toBeTruthy()
    expect(screen.queryByText(/Umsatz/)).toBeNull()
  })

  it('sagt ohne Netz, dass Kennzahlen eine Verbindung brauchen, und zeigt keine Zahl', async () => {
    routes['GET /api/v1/auth/me'] = () => ({ status: 200, body: owner })
    routes[managementPath] = () => {
      throw new TypeError('Failed to fetch')
    }
    render(<MyDayApp />)

    fireEvent.click(await screen.findByRole('button', { name: 'Heute im Spa' }))

    expect(await screen.findByText('Kennzahlen brauchen eine Verbindung.')).toBeTruthy()
    expect(screen.queryByRole('list', { name: 'Kennzahlen des Tages' })).toBeNull()
  })
})
