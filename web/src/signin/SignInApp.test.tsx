import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import axe from 'axe-core'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { addPasskey } from '../auth/passkeys'
import { SignInApp } from './SignInApp'

vi.mock('../auth/passkeys', () => ({
  addPasskey: vi.fn(async () => true),
  signInWithPasskey: vi.fn(async () => false),
  passkeysSupported: () => true,
}))

type Reply = { status: number; body?: unknown }
type Handler = (request: Request) => Reply | Promise<Reply>

const owner = {
  id: 'u-1',
  email: 'inhaberin@spa.test',
  displayName: 'Katrin Aigner',
  passkeyCount: 1,
  access: { tenantId: 't-1', tenantName: 'Spa Wien', role: 'Owner', allLocations: true, locationIds: [] },
  employeeId: null,
  rights: ['ViewCalendar', 'Book', 'ManageStaff', 'ManageCatalog', 'ManageLocation', 'ViewFinance'],
  hasPin: false,
  mustChangePassword: false,
}
const modules = (standalone: boolean) => ({
  standalone,
  onlineBooking: !standalone,
  bookingIntake: false,
  assistant: false,
  reminders: false,
  rebooking: false,
  benchmark: false,
  dashboard: false,
})

let routes: Record<string, Handler>
const requests: { method: string; path: string; body: unknown }[] = []

function stubFetch() {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: Request | string, init?: RequestInit) => {
      const request = typeof input === 'string' ? new Request(new URL(input, 'http://localhost'), init) : input
      const path = new URL(request.url).pathname
      const text = request.method === 'GET' ? '' : await request.clone().text()
      requests.push({ method: request.method, path, body: text ? JSON.parse(text) : undefined })
      const handler = routes[`${request.method} ${path}`]
      const reply = handler ? await handler(request) : { status: 404 }
      return new Response(reply.body === undefined ? null : JSON.stringify(reply.body), {
        status: reply.status,
        headers: { 'Content-Type': 'application/json' },
      })
    }),
  )
}

/** Pflichtfelder tragen einen Stern (aria-hidden) im Label-Text. */
const byLabel = (label: string) => screen.getByLabelText((text) => text.replace(/ [*]$/, '') === label)

function at(search: string) {
  window.history.replaceState(null, '', `/anmelden/${search}`)
}

/** Einzelplatz, nicht angemeldet; nach dem Anmelden liefert /api/v1/auth/me die Inhaberin. */
function standaloneSignedOut(mustChangePassword = false) {
  let signedIn = false
  routes['GET /api/v1/system/modules'] = () => ({ status: 200, body: modules(true) })
  routes['GET /api/v1/auth/me'] = () => (signedIn ? { status: 200, body: { ...owner, userName: 'katrin', mustChangePassword } } : { status: 401 })
  routes['POST /api/v1/auth/standalone/login'] = () => {
    signedIn = true
    return { status: 200, body: {} }
  }
}

async function signInWithPassword() {
  fireEvent.change(await screen.findByLabelText((text) => text.replace(/ [*]$/, '') === 'Benutzername'), { target: { value: 'katrin' } })
  fireEvent.change(byLabel('Passwort'), { target: { value: 'geheim123' } })
  fireEvent.click(screen.getByRole('button', { name: 'Anmelden' }))
}

beforeEach(() => {
  requests.length = 0
  routes = {
    'GET /api/v1/system/modules': () => ({ status: 200, body: modules(false) }),
    'GET /api/v1/auth/me': () => ({ status: 200, body: owner }),
  }
  vi.mocked(addPasskey).mockClear()
  stubFetch()
  at('')
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
  at('')
})

describe('Seite „Anmelden“', () => {
  it('Einladungslink_Klick_auf_Annehmen_ruft_accept_und_bietet_den_Passkey_an', async () => {
    let accepted = false
    at('?einladung=tok-1')
    routes['GET /api/v1/auth/me'] = () => (accepted ? { status: 200, body: owner } : { status: 401 })
    routes['GET /api/v1/auth/invitations/tok-1'] = () => ({
      status: 200,
      body: { displayName: 'Lena Huber', email: 'lena@spa.test', role: 'Reception', locationNames: ['Wien'] },
    })
    routes['POST /api/v1/auth/invitations/tok-1/accept'] = () => {
      accepted = true
      return { status: 200, body: {} }
    }
    render(<SignInApp />)

    fireEvent.click(await screen.findByRole('button', { name: 'Einladung annehmen und Passkey anlegen' }))

    await waitFor(() => expect(requests.some((r) => r.method === 'POST' && r.path === '/api/v1/auth/invitations/tok-1/accept')).toBe(true))
    await waitFor(() => expect(addPasskey).toHaveBeenCalledTimes(1))
    expect(await screen.findByRole('button', { name: 'Code für die Windows-App' })).toBeTruthy()
    expect(window.location.search).toBe('')
  })

  it('Angemeldet_Klick_auf_Code_fuer_die_Windows_App_zeigt_Code_und_Ablaufzeit', async () => {
    // 14:30 Uhr Ortszeit, damit die Erwartung nicht von der Zeitzone des Testrechners abhängt.
    const expiresAt = new Date(2026, 10, 1, 14, 30).toISOString()
    routes['POST /api/v1/auth/device-code'] = () => ({ status: 200, body: { code: 'K7Q4ZP', expiresAt } })
    render(<SignInApp />)

    fireEvent.click(await screen.findByRole('button', { name: 'Code für die Windows-App' }))

    const code = await screen.findByText('K7Q4ZP')
    expect(code.closest('[role="status"]')?.textContent).toContain('gültig bis 14:30')
    expect(screen.getByRole('button', { name: 'Passkey hinzufügen' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Abmelden' })).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Mein Tag' }).getAttribute('href')).toBe('/mein-tag/')
    expect(screen.getByRole('link', { name: 'Hilfe' }).getAttribute('href')).toBe('/help/')
    const result = await axe.run(document.body, { rules: { 'color-contrast': { enabled: false } } })
    expect(result.violations.map((v) => `${v.id}: ${v.nodes.map((n) => n.html).join(' | ')}`)).toEqual([])
  }, 30000)

  it('Einzelplatz_zeigt_keine_Registrierung_und_meldet_mit_Benutzername_an', async () => {
    standaloneSignedOut()
    render(<SignInApp />)

    await signInWithPassword()

    expect(await screen.findByRole('button', { name: 'Code für die Windows-App' })).toBeTruthy()
    expect(requests.find((r) => r.path === '/api/v1/auth/standalone/login')?.body).toEqual({ userName: 'katrin', password: 'geheim123', persistent: true })
    expect(screen.queryByRole('button', { name: 'Neues Spa registrieren' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Mit Passkey anmelden' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Passkey hinzufügen' })).toBeNull()
  })

  it('Rueckkehr_nur_auf_eigene_Pfade', async () => {
    const own = vi.fn()
    standaloneSignedOut()
    at('?zurueck=/mein-tag/')
    render(<SignInApp navigate={own} />)
    await signInWithPassword()
    await waitFor(() => expect(own).toHaveBeenCalledWith('/mein-tag/'))
    cleanup()

    // Tab, Zeilenumbruch und Wagenrücklauf entfernt der URL-Parser: aus „/\t/host“ wird „//host“.
    for (const foreign of ['//boese.example/', '/\\boese.example/', 'https://boese.example/', 'mein-tag/', '/\t/boese.example/', '/\n/boese.example/', '/\r/boese.example/']) {
      const navigate = vi.fn()
      standaloneSignedOut()
      at(`?zurueck=${encodeURIComponent(foreign)}`)
      render(<SignInApp navigate={navigate} />)
      await signInWithPassword()
      expect(await screen.findByRole('button', { name: 'Code für die Windows-App' })).toBeTruthy()
      expect(navigate).not.toHaveBeenCalled()
      cleanup()
    }
  })

  it('Startpasswort_noch_nicht_geaendert_bleibt_auf_der_Seite_und_verweist_auf_die_Windows_App', async () => {
    const navigate = vi.fn()
    standaloneSignedOut(true)
    at('?zurueck=/mein-tag/')
    render(<SignInApp navigate={navigate} />)

    await signInWithPassword()

    expect((await screen.findByRole('alert')).textContent).toContain('Startpasswort gilt noch')
    expect(screen.getByRole('alert').textContent).toContain('in der Windows-App')
    expect(navigate).not.toHaveBeenCalled()
  })

  it('Einladung_angenommen_aber_Passkey_abgebrochen_zeigt_die_Warnung_auf_der_Seite', async () => {
    let accepted = false
    vi.mocked(addPasskey).mockResolvedValueOnce(false)
    at('?einladung=tok-2')
    routes['GET /api/v1/auth/me'] = () => (accepted ? { status: 200, body: owner } : { status: 401 })
    routes['GET /api/v1/auth/invitations/tok-2'] = () => ({
      status: 200,
      body: { displayName: 'Lena Huber', email: 'lena@spa.test', role: 'Reception', locationNames: ['Wien'] },
    })
    routes['POST /api/v1/auth/invitations/tok-2/accept'] = () => {
      accepted = true
      return { status: 200, body: {} }
    }
    render(<SignInApp />)

    fireEvent.click(await screen.findByRole('button', { name: 'Einladung annehmen und Passkey anlegen' }))

    expect(await screen.findByRole('button', { name: 'Code für die Windows-App' })).toBeTruthy()
    expect(screen.getByRole('alert').textContent).toBe('Konto angelegt. Der Passkey ist noch nicht gespeichert. Vor dem Abmelden „Passkey hinzufügen“ wählen.')
  })

  it('Ungueltige_Einladung_entfernt_den_Link_aus_der_Adresse', async () => {
    at('?einladung=alt')
    routes['GET /api/v1/auth/me'] = () => ({ status: 401 })
    render(<SignInApp />)

    expect(await screen.findByText(/Diese Einladung ist abgelaufen/)).toBeTruthy()
    expect(window.location.search).toBe('')
  })

  it('Zahlungen_1_zeigt_den_Hinweis_fuer_die_Windows_App', async () => {
    at('?zahlungen=1')
    render(<SignInApp />)

    expect(await screen.findByText('Zahlungskonto verbunden. Zurück in der Windows-App: Verwaltung → Zahlungen.')).toBeTruthy()
  })
})
