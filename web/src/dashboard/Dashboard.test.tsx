import { act, cleanup, render, screen, waitFor, within } from '@testing-library/react'
import axe from 'axe-core'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { DashboardApp, REFRESH_MS } from './DashboardApp'
import { RECONNECT_MS, startDisplayLive, type DisplayLiveHandlers, type StartLive } from './live'

type Reply = { status: number; body?: unknown }

// Montag, 1. März 2027, 10:30 in Wien (UTC+1).
const NOW = new Date('2027-03-01T09:30:00Z')

const board = (overrides: Record<string, unknown> = {}) => ({
  locationName: 'Wien',
  timeZone: 'Europe/Vienna',
  now: NOW.toISOString(),
  dayStart: '2027-02-28T23:00:00Z',
  dayEnd: '2027-03-01T23:00:00Z',
  people: [
    {
      name: 'Anna',
      color: '#2f6f5e',
      state: 'InTreatment',
      shifts: [{ start: '2027-03-01T08:00:00Z', end: '2027-03-01T16:00:00Z' }],
      appointments: [{ start: '2027-03-01T09:00:00Z', end: '2027-03-01T09:50:00Z' }],
    },
    {
      name: 'Ben',
      color: '#8a4b2a',
      state: 'Free',
      shifts: [{ start: '2027-03-01T08:00:00Z', end: '2027-03-01T16:00:00Z' }],
      appointments: [],
    },
  ],
  rooms: [
    { name: 'Raum A', free: false, until: '2027-03-01T10:05:00Z' },
    { name: 'Raum B', free: true, until: '2027-03-01T12:55:00Z' },
    { name: 'Raum C', free: true, until: null },
  ],
  next: [{ start: '2027-03-01T13:00:00Z', initials: 'A. H.', person: 'Ben', room: 'Raum B' }],
  ...overrides,
})

const session = { deviceId: 'd-1', name: 'Pausenraum', locationId: 'loc-1', locationName: 'Wien' }

let routes: Record<string, () => Reply>
let requests: string[]

function stubFetch() {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: Request | string, init?: RequestInit) => {
      const request = typeof input === 'string' ? new Request(new URL(input, 'http://localhost'), init) : input
      const key = `${request.method} ${new URL(request.url).pathname}`
      requests.push(key)
      const reply = routes[key]?.() ?? { status: 404 }
      return new Response(reply.body === undefined ? null : JSON.stringify(reply.body), {
        status: reply.status,
        headers: { 'Content-Type': 'application/json' },
      })
    }),
  )
}

/** Ersatz für die SignalR-Verbindung: hält die Rückrufe fest, damit der Test ein Signal auslösen kann. */
function fakeLive() {
  const started: DisplayLiveHandlers[] = []
  const live: StartLive = (handlers) => {
    started.push(handlers)
    return () => undefined
  }
  return { live, started }
}

async function expectAccessible() {
  const result = await axe.run(document.body, {
    runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] },
    rules: { 'color-contrast': { enabled: false } },
  })
  expect(result.violations.map((v) => `${v.id}: ${v.nodes.map((n) => n.html).join(' | ')}`)).toEqual([])
}

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date', 'setInterval', 'clearInterval', 'setTimeout', 'clearTimeout'], shouldAdvanceTime: true })
  vi.setSystemTime(NOW)
  requests = []
  routes = {
    'GET /api/v1/display/session': () => ({ status: 200, body: session }),
    'GET /api/v1/display/board': () => ({ status: 200, body: board() }),
  }
  stubFetch()
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
  vi.useRealTimers()
})

describe('Dashboard im Pausenraum', () => {
  it('Given eine gekoppelte Anzeige When die Tafel lädt Then zeigt sie Tagesleiste, freie Räume und nächste Termine mit Initialen', async () => {
    render(<DashboardApp live={fakeLive().live} />)

    expect(await screen.findByRole('heading', { level: 1, name: 'Wien' })).toBeTruthy()
    const people = screen.getByRole('region', { name: 'Heute im Dienst' })
    expect(within(people).getByRole('img', { name: 'Anna: Dienst 09:00 bis 17:00, Termine 10:00 bis 10:50' })).toBeTruthy()
    expect(within(people).getByText('in Behandlung')).toBeTruthy()
    expect(within(people).getByRole('img', { name: 'Ben: Dienst 09:00 bis 17:00, keine Termine' })).toBeTruthy()
    const rooms = screen.getByRole('region', { name: 'Räume' })
    expect(within(rooms).getByText('belegt bis 11:05')).toBeTruthy()
    expect(within(rooms).getByText('frei bis 13:55')).toBeTruthy()
    expect(within(rooms).getByText('frei')).toBeTruthy()
    const next = screen.getByRole('region', { name: 'Nächste Termine' })
    expect(within(next).getByText('A. H.')).toBeTruthy()
    expect(within(next).getByText('14:00')).toBeTruthy()
    expect(within(next).getByText('Ben, Raum B')).toBeTruthy()
    expect(screen.getByText('10:30')).toBeTruthy()
    await expectAccessible()
  })

  it('Given die Tafel When sie gezeigt wird Then gibt es nichts zum Bedienen', async () => {
    render(<DashboardApp live={fakeLive().live} />)
    await screen.findByRole('heading', { level: 1, name: 'Wien' })

    expect(document.querySelectorAll('button, input, select, textarea, a, [tabindex], [contenteditable]').length).toBe(0)
  })

  it('Given die Tafel läuft When ein Termin verschoben wird und das Signal kommt Then lädt sie sofort neu und zeigt die neue Zeit', async () => {
    const { live, started } = fakeLive()
    render(<DashboardApp live={live} />)
    await screen.findByText('14:00')
    routes['GET /api/v1/display/board'] = () => ({
      status: 200,
      body: board({ next: [{ start: '2027-03-01T15:00:00Z', initials: 'A. H.', person: 'Ben', room: 'Raum B' }] }),
    })

    await act(async () => started[0].onChanged())

    expect(await screen.findByText('16:00')).toBeTruthy()
    expect(screen.queryByText('14:00')).toBeNull()
  })

  it('Given kein Signal When 30 Sekunden vergehen Then lädt die Tafel von selbst nach', async () => {
    render(<DashboardApp live={fakeLive().live} />)
    await screen.findByText('14:00')
    const before = requests.filter((r) => r === 'GET /api/v1/display/board').length

    await act(async () => void vi.advanceTimersByTime(REFRESH_MS + 100))

    await waitFor(() => expect(requests.filter((r) => r === 'GET /api/v1/display/board').length).toBeGreaterThan(before))
  })

  it('Given die Anzeige ist nicht gekoppelt When sie startet Then zeigt sie den Code und wechselt nach dem Koppeln auf die Tafel', async () => {
    let paired = false
    routes['GET /api/v1/display/session'] = () => (paired ? { status: 200, body: session } : { status: 401 })
    routes['POST /api/v1/display/pairing'] = () => ({ status: 200, body: { code: 'K7QM-2XRT', secret: 'geheim', expiresAt: '2027-03-01T09:40:00Z' } })
    routes['POST /api/v1/display/pairing/redeem'] = () => (paired ? { status: 200, body: session } : { status: 202 })
    render(<DashboardApp live={fakeLive().live} />)

    expect(await screen.findByText('K7QM-2XRT')).toBeTruthy()
    expect(screen.getByRole('heading', { level: 1, name: 'Anzeige koppeln' })).toBeTruthy()
    paired = true
    await act(async () => void vi.advanceTimersByTime(3100))

    expect(await screen.findByRole('heading', { level: 1, name: 'Wien' })).toBeTruthy()
  })

  it('Given die Leitung widerruft die Anzeige When die Tafel 401 bekommt Then zeigt sie wieder den Kopplungscode', async () => {
    routes['POST /api/v1/display/pairing'] = () => ({ status: 200, body: { code: 'ABCD-EFGH', secret: 's', expiresAt: '2027-03-01T09:40:00Z' } })
    routes['POST /api/v1/display/pairing/redeem'] = () => ({ status: 202 })
    const { live, started } = fakeLive()
    render(<DashboardApp live={live} />)
    await screen.findByText('14:00')
    routes['GET /api/v1/display/board'] = () => ({ status: 401 })

    await act(async () => started[0].onChanged())

    expect(await screen.findByText('ABCD-EFGH')).toBeTruthy()
  })

  it('Given der Server ist nicht erreichbar When die Tafel nachlädt Then bleibt der letzte Stand mit Hinweis stehen', async () => {
    const { live, started } = fakeLive()
    render(<DashboardApp live={live} />)
    await screen.findByText('14:00')
    routes['GET /api/v1/display/board'] = () => {
      throw new TypeError('Netzwerk weg')
    }

    await act(async () => started[0].onChanged())

    expect(await screen.findByText('Keine Verbindung zum Server. Gezeigt wird der letzte Stand.')).toBeTruthy()
    expect(screen.getByText('14:00')).toBeTruthy()
  })

  it('Given der Server antwortet beim Hochfahren mit 502 When er später läuft Then bleibt die Kopplung erhalten und die Tafel erscheint', async () => {
    let up = false
    routes['GET /api/v1/display/session'] = () => (up ? { status: 200, body: session } : { status: 502 })
    render(<DashboardApp live={fakeLive().live} />)

    expect(await screen.findByText('Keine Verbindung zum Server. Die Anzeige versucht es weiter.')).toBeTruthy()
    expect(screen.queryByText('Anzeige koppeln')).toBeNull()
    up = true
    await act(async () => void vi.advanceTimersByTime(5100))

    expect(await screen.findByRole('heading', { level: 1, name: 'Wien' })).toBeTruthy()
  })

  it('Given das Modul wird bei laufender Tafel ausgeschaltet When die Tafel 404 bekommt Then erklärt sie das', async () => {
    const { live, started } = fakeLive()
    render(<DashboardApp live={live} />)
    await screen.findByText('14:00')
    routes['GET /api/v1/display/board'] = () => ({ status: 404 })

    await act(async () => started[0].onChanged())

    expect(await screen.findByRole('heading', { level: 1, name: 'Dashboard ausgeschaltet' })).toBeTruthy()
  })

  it('Given das Modul Dashboard ist aus When die Anzeige startet Then erklärt sie das statt zu koppeln', async () => {
    routes['GET /api/v1/display/session'] = () => ({ status: 404 })
    render(<DashboardApp live={fakeLive().live} />)

    expect(await screen.findByRole('heading', { level: 1, name: 'Dashboard ausgeschaltet' })).toBeTruthy()
  })
})

describe('Live-Verbindung der Anzeige', () => {
  function fakeConnection(failStarts: number, failInvokes = 0) {
    const calls: string[] = []
    let starts = 0
    let invokes = 0
    let reconnected: () => void = () => undefined
    const connection = {
      state: 'Disconnected',
      start: vi.fn(async () => {
        starts += 1
        calls.push('start')
        if (starts <= failStarts) throw new Error('Server nicht erreichbar')
        connection.state = 'Connected'
      }),
      stop: vi.fn(async () => undefined),
      invoke: vi.fn(async (method: string) => {
        invokes += 1
        calls.push(method)
        if (invokes <= failInvokes) throw new Error('Abo abgelehnt')
      }),
      onreconnected: (callback: () => void) => void (reconnected = callback),
    }
    return { connection, calls, reconnect: () => reconnected() }
  }

  it('Given der Server ist beim Start nicht da When er später antwortet Then verbindet sich die Anzeige ohne Zutun und abonniert ihren Standort', async () => {
    const { connection, calls } = fakeConnection(2)
    const onChanged = vi.fn()

    const stop = startDisplayLive({ onChanged }, (() => connection) as never)
    await vi.advanceTimersByTimeAsync(RECONNECT_MS * 2 + 100)

    expect(calls).toEqual(['start', 'start', 'start', 'WatchDisplay'])
    expect(onChanged).toHaveBeenCalledTimes(1)
    stop()
    expect(connection.stop).toHaveBeenCalled()
  })

  it('Given der Start gelingt aber das Abo scheitert When es später klappt Then wird nur das Abo wiederholt und nicht erneut gestartet', async () => {
    const { connection, calls } = fakeConnection(0, 1)
    const onChanged = vi.fn()

    startDisplayLive({ onChanged }, (() => connection) as never)
    await vi.advanceTimersByTimeAsync(RECONNECT_MS + 100)

    expect(calls).toEqual(['start', 'WatchDisplay', 'WatchDisplay'])
    expect(onChanged).toHaveBeenCalledTimes(1)
  })

  it('Given die Verbindung bricht ab When SignalR sie wiederherstellt Then abonniert die Anzeige neu und lädt nach', async () => {
    const { connection, calls, reconnect } = fakeConnection(0)
    const onChanged = vi.fn()
    startDisplayLive({ onChanged }, (() => connection) as never)
    await vi.advanceTimersByTimeAsync(10)
    onChanged.mockClear()
    calls.length = 0

    reconnect()
    await vi.advanceTimersByTimeAsync(10)

    expect(calls).toEqual(['WatchDisplay'])
    expect(onChanged).toHaveBeenCalledTimes(1)
  })
})
