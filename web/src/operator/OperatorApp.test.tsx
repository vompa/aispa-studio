import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import axe from 'axe-core'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { OperatorApp } from './OperatorApp'

type Call = { method: string; path: string; key: string | null; body?: unknown }

const spa = {
  id: 't-1',
  name: 'Spa am See',
  country: 'AT',
  createdAt: '2026-10-01T08:00:00Z',
  locations: 2,
  mailboxAddress: 'termine@spa-am-see.at',
  mailboxEnabled: true,
  mailboxCheckedAt: '2026-10-18T09:00:00Z',
  mailboxError: null,
  routes: [{ id: 'r-1', channel: 'Sms', address: '+436601234567', senderId: null }],
  lockedAt: null,
  lockReason: null,
  plan: 'SpaPlus',
}

let calls: Call[] = []
let routes: Record<string, (body: unknown) => { status: number; body?: unknown }> = {}

beforeEach(() => {
  calls = []
  sessionStorage.clear()
  routes = {
    'GET /api/operator/spas': () => ({ status: 200, body: [spa] }),
    'POST /api/operator/routes': (body) => ({ status: 201, body: { id: 'r-2', ...(body as object) } }),
    'DELETE /api/operator/routes/r-1': () => ({ status: 204 }),
    'POST /api/operator/spas/t-1/lock': () => ({ status: 204 }),
    'DELETE /api/operator/spas/t-1/lock': () => ({ status: 204 }),
    'PUT /api/operator/spas/t-1/plan': () => ({ status: 204 }),
  }
  vi.stubGlobal(
    'fetch',
    vi.fn(async (request: Request) => {
      const url = new URL(request.url)
      const text = request.method === 'GET' || request.method === 'DELETE' ? '' : await request.clone().text()
      const body = text ? JSON.parse(text) : undefined
      calls.push({ method: request.method, path: url.pathname, key: request.headers.get('X-Aispa-Operator-Key'), body })
      const handler = routes[`${request.method} ${url.pathname}`]
      const reply = handler ? handler(body) : { status: 404 }
      return new Response(reply.body === undefined ? null : JSON.stringify(reply.body), {
        status: reply.status,
        headers: { 'Content-Type': 'application/json' },
      })
    }),
  )
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

/** Pflichtfelder tragen einen Stern (aria-hidden) im Label-Text. */
const labelled = (label: string) => (text: string) => text.replace(/ [*]$/, '') === label

async function expectAccessible() {
  const result = await axe.run(document.body, {
    runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] },
    rules: { 'color-contrast': { enabled: false } },
  })
  expect(result.violations.map((v) => `${v.id}: ${v.nodes.map((n) => n.html).join(' | ')}`)).toEqual([])
}

function signIn(key = 'betreiber-test') {
  fireEvent.change(screen.getByLabelText(labelled('Betreiber-Schlüssel')), { target: { value: key } })
  fireEvent.click(screen.getByRole('button', { name: 'Anmelden' }))
}

describe('Betreiber-Seite', () => {
  it('meldet mit dem Schlüssel an und zeigt die Spas mit Postfach und Nummern', async () => {
    render(<OperatorApp />)
    await expectAccessible()
    signIn()

    const card = (await screen.findByRole('heading', { name: 'Spa am See' })).closest('section')!
    expect(within(card).getByText(/2 Standorte/)).toBeTruthy()
    expect(within(card).getByText(/Postfach termine@spa-am-see\.at/)).toBeTruthy()
    expect(within(card).getByText('+436601234567')).toBeTruthy()
    expect(calls[0]).toMatchObject({ path: '/api/operator/spas', key: 'betreiber-test' })
    expect(sessionStorage.getItem('aispa.betreiber.schluessel')).toBe('betreiber-test')
    await expectAccessible()
  })

  it('ordnet eine WhatsApp-Nummer mit Kennung zu und löst eine Nummer erst nach Rückfrage', async () => {
    sessionStorage.setItem('aispa.betreiber.schluessel', 'betreiber-test')
    render(<OperatorApp />)
    const card = (await screen.findByRole('heading', { name: 'Spa am See' })).closest('section')!

    fireEvent.change(within(card).getByLabelText('Kanal'), { target: { value: 'WhatsApp' } })
    fireEvent.change(within(card).getByLabelText(labelled('Nummer')), { target: { value: '+43 660 7654321' } })
    fireEvent.change(within(card).getByLabelText('Phone-Number-ID (Meta)'), { target: { value: '1098765' } })
    fireEvent.click(within(card).getByRole('button', { name: 'Zuordnen' }))

    expect((await within(card).findByRole('status')).textContent).toContain('gehört jetzt zu Spa am See')
    expect(calls.find((c) => c.method === 'POST')).toMatchObject({
      key: 'betreiber-test',
      body: { tenantId: 't-1', channel: 'WhatsApp', address: '+43 660 7654321', senderId: '1098765' },
    })

    fireEvent.click(within(card).getByRole('button', { name: 'SMS +436601234567 lösen' }))
    expect(calls.some((c) => c.method === 'DELETE')).toBe(false)
    fireEvent.click(within(card).getByRole('button', { name: 'Wirklich lösen' }))
    await vi.waitFor(() => expect(calls.some((c) => c.method === 'DELETE' && c.path === '/api/operator/routes/r-1')).toBe(true))
  })

  it('zeigt, wenn die Nummer schon einem anderen Spa gehört', async () => {
    sessionStorage.setItem('aispa.betreiber.schluessel', 'betreiber-test')
    routes['POST /api/operator/routes'] = () => ({
      status: 409,
      body: { title: 'Schon vergeben', detail: 'Diese Nummer bzw. Adresse ist schon einem Spa zugeordnet.' },
    })
    render(<OperatorApp />)
    const card = (await screen.findByRole('heading', { name: 'Spa am See' })).closest('section')!

    fireEvent.change(within(card).getByLabelText(labelled('Nummer')), { target: { value: '+436601234567' } })
    fireEvent.click(within(card).getByRole('button', { name: 'Zuordnen' }))

    const error = await within(card).findByText(/schon einem Spa zugeordnet/)
    const address = within(card).getByLabelText(labelled('Nummer'))
    expect(address.getAttribute('aria-describedby')).toBe(error.id)
    expect(address.getAttribute('aria-invalid')).toBe('true')
  })

  it('Given „Spa sperren“ When das Formular erscheint Then steht der Fokus im Feld „Grund“ und kehrt mit „Abbrechen“ zurück', async () => {
    sessionStorage.setItem('aispa.betreiber.schluessel', 'betreiber-test')
    render(<OperatorApp />)
    const card = (await screen.findByRole('heading', { name: 'Spa am See' })).closest('section')!

    const trigger = within(card).getByRole('button', { name: 'Spa sperren' })
    trigger.focus()
    fireEvent.click(trigger)
    expect(document.activeElement).toBe(within(card).getByLabelText(labelled('Grund (sieht das Team)')))

    fireEvent.click(within(card).getByRole('button', { name: 'Abbrechen' }))
    expect(document.activeElement).toBe(within(card).getByRole('button', { name: 'Spa sperren' }))
  })

  it('Given ein abgelehntes Sperren When der Fehler erscheint Then hängt er per aria-describedby am Feld „Grund“ (fluent2-web, Eingaben)', async () => {
    sessionStorage.setItem('aispa.betreiber.schluessel', 'betreiber-test')
    routes['POST /api/operator/spas/t-1/lock'] = () => ({ status: 500 })
    render(<OperatorApp />)
    const card = (await screen.findByRole('heading', { name: 'Spa am See' })).closest('section')!

    fireEvent.click(within(card).getByRole('button', { name: 'Spa sperren' }))
    const reason = within(card).getByLabelText(labelled('Grund (sieht das Team)'))
    fireEvent.change(reason, { target: { value: 'Vertrag beendet.' } })
    fireEvent.click(within(card).getByRole('button', { name: 'Wirklich sperren' }))

    const message = await within(card).findByText('Sperren hat nicht geklappt.')
    expect(reason.getAttribute('aria-invalid')).toBe('true')
    expect(reason.getAttribute('aria-describedby')).toBe(message.id)
    expect(message.id).not.toBe('')
  })

  it('Given „Lösen“ When die Rückfrage erscheint Then steht der Fokus auf „Wirklich lösen“ und kehrt mit „Abbrechen“ zu „Lösen“ zurück', async () => {
    sessionStorage.setItem('aispa.betreiber.schluessel', 'betreiber-test')
    render(<OperatorApp />)
    const card = (await screen.findByRole('heading', { name: 'Spa am See' })).closest('section')!

    fireEvent.click(within(card).getByRole('button', { name: 'SMS +436601234567 lösen' }))
    expect(document.activeElement).toBe(within(card).getByRole('button', { name: 'Wirklich lösen' }))

    fireEvent.click(within(card).getByRole('button', { name: 'Abbrechen' }))
    expect(document.activeElement).toBe(within(card).getByRole('button', { name: 'SMS +436601234567 lösen' }))
  })

  it('sperrt ein Spa erst mit Grund und Rückfrage und entsperrt es wieder', async () => {
    sessionStorage.setItem('aispa.betreiber.schluessel', 'betreiber-test')
    render(<OperatorApp />)
    const card = (await screen.findByRole('heading', { name: 'Spa am See' })).closest('section')!

    fireEvent.click(within(card).getByRole('button', { name: 'Spa sperren' }))
    expect(calls.some((c) => c.path.endsWith('/lock'))).toBe(false)
    fireEvent.change(within(card).getByLabelText(labelled('Grund (sieht das Team)')), { target: { value: 'Vertrag beendet.' } })
    routes['GET /api/operator/spas'] = () => ({ status: 200, body: [{ ...spa, lockedAt: '2026-10-18T13:00:00Z', lockReason: 'Vertrag beendet.' }] })
    fireEvent.click(within(card).getByRole('button', { name: 'Wirklich sperren' }))

    expect(await within(card).findByText(/Gesperrt seit .*Vertrag beendet\./)).toBeTruthy()
    expect(calls.find((c) => c.method === 'POST' && c.path === '/api/operator/spas/t-1/lock')).toMatchObject({
      key: 'betreiber-test',
      body: { reason: 'Vertrag beendet.' },
    })
    await expectAccessible()

    fireEvent.click(within(card).getByRole('button', { name: 'Entsperren' }))
    await vi.waitFor(() => expect(calls.some((c) => c.method === 'DELETE' && c.path === '/api/operator/spas/t-1/lock')).toBe(true))
  })

  it('Given „Nummer zuordnen“ ohne Nummer When abgeschickt wird Then steht der Fehler am Feld und der Fokus liegt im Feld', async () => {
    sessionStorage.setItem('aispa.betreiber.schluessel', 'betreiber-test')
    render(<OperatorApp />)
    const card = (await screen.findByRole('heading', { name: 'Spa am See' })).closest('section')!

    fireEvent.click(within(card).getByRole('button', { name: 'Zuordnen' }))

    const address = within(card).getByLabelText(labelled('Nummer'))
    expect(address.getAttribute('aria-describedby')).toBe(within(card).getByText('Die Nummer fehlt. Trage sie ein.').id)
    expect(address.getAttribute('aria-invalid')).toBe('true')
    expect(document.activeElement).toBe(address)
    expect(within(card).queryByRole('alert')).toBeNull()
    expect(calls.some((c) => c.method === 'POST')).toBe(false)
  })

  it('Given „Spa sperren“ ohne Grund When abgeschickt wird Then steht der Fehler am Feld und der Fokus liegt im Feld', async () => {
    sessionStorage.setItem('aispa.betreiber.schluessel', 'betreiber-test')
    render(<OperatorApp />)
    const card = (await screen.findByRole('heading', { name: 'Spa am See' })).closest('section')!

    fireEvent.click(within(card).getByRole('button', { name: 'Spa sperren' }))
    fireEvent.click(within(card).getByRole('button', { name: 'Wirklich sperren' }))

    const reason = within(card).getByLabelText(labelled('Grund (sieht das Team)'))
    expect(reason.getAttribute('aria-describedby')).toBe(within(card).getByText('Der Grund fehlt. Trage ihn ein, das Team sieht ihn.').id)
    expect(document.activeElement).toBe(reason)
    expect(calls.some((c) => c.path.endsWith('/lock'))).toBe(false)
  })

  it('Given die Anmeldung ohne Schlüssel When abgeschickt wird Then steht der Fehler am Feld und der Fokus liegt im Feld', () => {
    render(<OperatorApp />)

    fireEvent.click(screen.getByRole('button', { name: 'Anmelden' }))

    const key = screen.getByLabelText(labelled('Betreiber-Schlüssel'))
    expect(key.getAttribute('aria-describedby')).toBe(screen.getByText('Der Betreiber-Schlüssel fehlt. Gib ihn ein.').id)
    expect(document.activeElement).toBe(key)
    expect(calls).toEqual([])
  })

  it('Given die Betreiber-Seite When im geschlossenen Auswahlfeld Pfeil runter gedrückt wird Then geht kein Aufruf an die API', async () => {
    sessionStorage.setItem('aispa.betreiber.schluessel', 'betreiber-test')
    render(<OperatorApp />)
    const card = (await screen.findByRole('heading', { name: 'Spa am See' })).closest('section')!
    const select = within(card).getByLabelText<HTMLSelectElement>('Abo-Stufe')

    // Chrome und Edge unter Windows wechseln bei geschlossenem Auswahlfeld mit jeder Pfeiltaste den Wert und feuern `change`.
    fireEvent.keyDown(select, { key: 'ArrowDown' })
    fireEvent.change(select, { target: { value: 'Spa' } })
    fireEvent.keyDown(select, { key: 'ArrowDown' })
    fireEvent.change(select, { target: { value: 'Start' } })

    await new Promise((resolve) => setTimeout(resolve, 0))
    expect(calls.some((c) => c.method === 'PUT')).toBe(false)
    expect(select.value).toBe('Start')
    expect(within(card).getByRole<HTMLButtonElement>('button', { name: 'Stufe ändern' }).disabled).toBe(false)
  })

  it('Given eine neue Stufe ist gewählt When „Stufe ändern“ gedrückt und die Rückfrage bestätigt wird Then geht genau ein Aufruf an die API und „Stufe geändert“ erscheint mit role=status', async () => {
    sessionStorage.setItem('aispa.betreiber.schluessel', 'betreiber-test')
    render(<OperatorApp />)
    const card = (await screen.findByRole('heading', { name: 'Spa am See' })).closest('section')!
    fireEvent.change(within(card).getByLabelText('Abo-Stufe'), { target: { value: 'Start' } })
    fireEvent.click(within(card).getByRole('button', { name: 'Stufe ändern' }))

    const dialog = screen.getByRole('alertdialog', { name: 'Abo-Stufe ändern?' })
    expect(dialog.textContent).toContain('Spa am See wechselt von Spa Plus auf Start')
    expect(document.activeElement).toBe(within(dialog).getByRole('button', { name: 'Stufe ändern' }))
    expect(calls.some((c) => c.method === 'PUT')).toBe(false)
    await expectAccessible()

    routes['GET /api/operator/spas'] = () => ({ status: 200, body: [{ ...spa, plan: 'Start' }] })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Stufe ändern' }))

    expect((await within(card).findByRole('status')).textContent).toBe('Stufe geändert.')
    const puts = calls.filter((c) => c.method === 'PUT')
    expect(puts).toHaveLength(1)
    expect(puts[0]).toMatchObject({ path: '/api/operator/spas/t-1/plan', key: 'betreiber-test', body: { plan: 'Start' } })
    await vi.waitFor(() => expect(calls.filter((c) => c.method === 'GET')).toHaveLength(2))
    expect(within(card).getByLabelText<HTMLSelectElement>('Abo-Stufe').value).toBe('Start')
    expect(screen.queryByRole('alertdialog')).toBeNull()
    await expectAccessible()
  })

  it('ändert nach Abbrechen in der Rückfrage nichts und zeigt wieder die aktuelle Stufe', async () => {
    sessionStorage.setItem('aispa.betreiber.schluessel', 'betreiber-test')
    render(<OperatorApp />)
    const card = (await screen.findByRole('heading', { name: 'Spa am See' })).closest('section')!
    fireEvent.change(within(card).getByLabelText('Abo-Stufe'), { target: { value: 'Spa' } })
    fireEvent.click(within(card).getByRole('button', { name: 'Stufe ändern' }))
    fireEvent.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Abbrechen' }))

    expect(screen.queryByRole('alertdialog')).toBeNull()
    expect(within(card).getByLabelText<HTMLSelectElement>('Abo-Stufe').value).toBe('SpaPlus')
    expect(within(card).getByRole<HTMLButtonElement>('button', { name: 'Stufe ändern' }).disabled).toBe(true)
    // Der Auslöser ist jetzt gesperrt und kann den Fokus nicht zurücknehmen; er geht ins Auswahlfeld statt verloren.
    expect(document.activeElement).toBe(within(card).getByLabelText('Abo-Stufe'))
    expect(calls.some((c) => c.method === 'PUT')).toBe(false)
  })

  it('zeigt den Fehler, wenn die Abo-Stufe nicht geändert werden kann', async () => {
    sessionStorage.setItem('aispa.betreiber.schluessel', 'betreiber-test')
    routes['PUT /api/operator/spas/t-1/plan'] = () => ({ status: 400, body: { title: 'Unbekannte Abo-Stufe.' } })
    render(<OperatorApp />)
    const card = (await screen.findByRole('heading', { name: 'Spa am See' })).closest('section')!
    fireEvent.change(within(card).getByLabelText('Abo-Stufe'), { target: { value: 'Spa' } })
    fireEvent.click(within(card).getByRole('button', { name: 'Stufe ändern' }))
    fireEvent.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Stufe ändern' }))

    expect((await within(card).findByRole('alert')).textContent).toBe('Unbekannte Abo-Stufe.')
    expect(within(card).getByLabelText<HTMLSelectElement>('Abo-Stufe').value).toBe('Spa')
  })

  it('verwirft einen falschen Schlüssel und erklärt einen abgeschalteten Zugang', async () => {
    routes['GET /api/operator/spas'] = () => ({ status: 403 })
    render(<OperatorApp />)
    signIn('falsch')
    expect((await screen.findByRole('alert')).textContent).toBe('Der Schlüssel stimmt nicht.')
    expect(sessionStorage.getItem('aispa.betreiber.schluessel')).toBeNull()

    routes['GET /api/operator/spas'] = () => ({ status: 404 })
    signIn('egal')
    expect((await screen.findByRole('alert')).textContent).toContain('nicht eingeschaltet')
  })
})
