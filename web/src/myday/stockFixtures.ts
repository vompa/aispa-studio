// Gemeinsamer Testaufbau für Inventur, Wareneingang und Scan in „Mein Tag“: Stub-Routen statt Netz, ein Standort, zwei Materialien.
import { vi } from 'vitest'

export type Reply = { status: number; body?: unknown }
export type Handler = (request: Request) => Reply | Promise<Reply>
export type Recorded = { method: string; path: string; query: string; body: unknown }

export const therapist = {
  id: 'u-1',
  email: 'lena@example.at',
  displayName: 'Lena Therapeutin',
  passkeyCount: 1,
  access: { tenantId: 't-1', tenantName: 'Spa Wien', role: 'Therapist', allLocations: false, locationIds: ['loc-1'] },
  employeeId: 'emp-1',
  rights: ['ViewCalendar', 'ChangeStatus'],
}
export const reception = { ...therapist, id: 'u-2', displayName: 'Rita Rezeption', access: { ...therapist.access, role: 'Reception' }, rights: ['ViewCalendar', 'ChangeStatus', 'Book'] }

export const oil = { id: 'm-1', name: 'Massageöl neutral', unit: 'ml', unitCost: 0.02, minimumQuantity: 500, isActive: true, barcode: '9001234567890' }
export const towel = { id: 'm-2', name: 'Handtuch', unit: 'Stk', unitCost: 3, minimumQuantity: 6, isActive: true, barcode: '4006381333931' }

const place = (id: string, name: string, kind: string, position: number, minimums: { itemId: string; minimumQuantity: number }[]) => ({
  id,
  locationId: 'loc-1',
  name,
  description: null,
  kind,
  roomId: null,
  isActive: true,
  position,
  minimums,
})
export const places = [
  place('pl-1', 'Hauptlager', 'Main', 0, []),
  place('pl-2', 'Raumschrank 2', 'Room', 1, [
    { itemId: oil.id, minimumQuantity: 500 },
    { itemId: towel.id, minimumQuantity: 6 },
  ]),
]

export const countLine = (item: { id: string; name: string; unit: string }, counted: number) => ({
  itemId: item.id,
  name: item.name,
  unit: item.unit,
  counted,
  expected: null,
})
export const countSession = (status: 'Open' | 'Submitted', lines: ReturnType<typeof countLine>[] = []) => ({
  id: 'cs-1',
  locationId: 'loc-1',
  storagePlaceId: 'pl-2',
  storagePlaceName: 'Raumschrank 2',
  status,
  startedBy: 'Lena Therapeutin',
  startedAt: '2027-03-01T08:00:00Z',
  submittedAt: status === 'Submitted' ? '2027-03-01T08:20:00Z' : null,
  closedAt: null,
  lines,
})

export const requests: Recorded[] = []
let routes: Record<string, Handler> = {}

/** Setzt die Routen zurück (angemeldet als `user`) und ersetzt `fetch` durch den Stub. */
export function stubApi(user: typeof therapist, extra: Record<string, Handler> = {}) {
  requests.length = 0
  routes = {
    'GET /api/v1/auth/me': () => ({ status: 200, body: user }),
    'GET /api/v1/locations': () => ({ status: 200, body: [{ id: 'loc-1', name: 'Wien Innere Stadt', timeZone: 'Europe/Vienna', currency: 'EUR', openingHours: [] }] }),
    'POST /api/v1/auth/channel/staff-app': () => ({ status: 200, body: { ...user, channel: 'StaffApp' } }),
    'GET /api/v1/reports/locations/loc-1/day-plan': () => ({
      status: 200,
      body: { employeeId: 'emp-1', employeeName: user.displayName, date: '2027-03-01', shifts: [], cards: [], hints: [] },
    }),
    'GET /api/v1/stock/items': () => ({ status: 200, body: [oil, towel] }),
    'GET /api/v1/stock/locations/loc-1/storage-places': () => ({ status: 200, body: places }),
    ...extra,
  }
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
        headers: { 'Content-Type': 'application/json' },
      })
    }),
  )
}

export const calls = (method: string, path: string) => requests.filter((r) => r.method === method && r.path === path)

export const START = 'POST /api/v1/stock/locations/loc-1/storage-places/pl-2/count-sessions'
export const LINE = (itemId: string) => `PUT /api/v1/stock/locations/loc-1/count-sessions/cs-1/lines/${itemId}`
export const SUBMIT = 'POST /api/v1/stock/locations/loc-1/count-sessions/cs-1/submit'
