import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import axe from 'axe-core'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { BookingApp } from './BookingApp'

type Reply = { status: number; body?: unknown }
type Handler = (request: Request) => Reply | Promise<Reply>

const location = {
  id: 'loc-1',
  name: 'Wien Innere Stadt',
  timeZone: 'Europe/Vienna',
  currency: 'EUR',
  openingHours: [],
  bookingEnabled: true,
  guestNote: 'Bitte kommen Sie 10 Minuten früher.',
  cancellationDeadlineHours: 24,
}
const massage = { id: 'tr-1', name: 'Klassische Massage', durationMinutes: 50, price: 79, currency: 'EUR' }
// 10:00 und 10:15 Uhr Wiener Zeit (Winterzeit, UTC+1).
const availability = {
  locationId: 'loc-1',
  treatmentId: 'tr-1',
  timeZone: 'Europe/Vienna',
  firstBookableDate: '2027-03-01',
  lastBookableDate: '2027-05-30',
  days: [
    { date: '2027-03-01', times: ['2027-03-01T09:00:00Z', '2027-03-01T09:15:00Z'] },
    { date: '2027-03-02', times: [] },
  ],
}
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
  anamnesisSubmitted: false,
}

const withContact = <T extends object>(l: T) => ({ ...l, contact: { street: null, postalCode: null, city: null, phone: null, email: null, directions: null } })

let routes: Record<string, Handler>
const requests: { method: string; path: string; body: unknown }[] = []

function stubFetch() {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: Request | string, init?: RequestInit) => {
      const request = typeof input === 'string' ? new Request(new URL(input, 'http://localhost'), init) : input
      const url = new URL(request.url)
      const text = request.method === 'GET' ? '' : await request.clone().text()
      requests.push({ method: request.method, path: url.pathname, body: text ? JSON.parse(text) : undefined })
      const handler = routes[`${request.method} ${url.pathname}`]
      const reply = handler ? await handler(request) : { status: 404 }
      return new Response(reply.body === undefined ? null : JSON.stringify(reply.body), {
        status: reply.status,
        headers: { 'Content-Type': 'application/json' },
      })
    }),
  )
}

/** Prüft die gerade sichtbare Seite mit axe-core auf Verstöße gegen WCAG 2.1 A/AA (ohne Kontrast: jsdom rechnet keine Farben). */
async function expectAccessible() {
  const result = await axe.run(document.body, {
    runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] },
    rules: { 'color-contrast': { enabled: false } },
  })
  expect(result.violations.map((v) => `${v.id}: ${v.nodes.map((n) => n.html).join(' | ')}`)).toEqual([])
}

beforeEach(() => {
  window.location.hash = ''
  localStorage.setItem('aispa.sprache', 'de')
  requests.length = 0
  routes = {
    'GET /api/online/me': () => ({ status: 401 }),
    'GET /api/online/locations': () => ({ status: 200, body: [location] }),
    'GET /api/online/locations/loc-1/treatments': () => ({ status: 200, body: [massage] }),
    'GET /api/online/locations/loc-1/availability': () => ({ status: 200, body: availability }),
    'GET /api/online/auth/providers': () => ({ status: 200, body: { external: ['Google'] } }),
  }
  stubFetch()
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

async function chooseTreatmentAndTime() {
  fireEvent.click(await screen.findByRole('radio', { name: /Klassische Massage/ }))
  fireEvent.click(screen.getByRole('button', { name: 'Weiter' }))
  const times = await screen.findByRole('group', { name: /Uhrzeit/ })
  fireEvent.click(within(times).getByRole('radio', { name: '10:15' }))
  fireEvent.click(screen.getByRole('button', { name: 'Weiter' }))
  await screen.findByRole('heading', { name: 'Ihre Angaben' })
}

describe('Online-Buchung', () => {
  // Befund B60: Fehlerzustände haben einen Weg heraus.
  it('Given die Standorte lassen sich nicht laden When „Erneut versuchen“ gedrückt wird Then lädt die Seite neu', async () => {
    let fail = true
    routes['GET /api/online/locations'] = () => (fail ? { status: 500 } : { status: 200, body: [location] })
    render(<BookingApp />)

    expect(await screen.findByText(/Keine Verbindung zum Server/)).toBeTruthy()
    fail = false
    fireEvent.click(screen.getByRole('button', { name: 'Erneut versuchen' }))

    expect(await screen.findByText('Schritt 1 von 3')).toBeTruthy()
    expect(screen.queryByText(/Keine Verbindung zum Server/)).toBeNull()
  })

  it('Given ein Standort ohne Behandlungen When die Seite das meldet Then führt „Anderen Standort wählen“ zurück zur Auswahl', async () => {
    routes['GET /api/online/locations'] = () => ({ status: 200, body: [withContact(location), withContact({ ...location, id: 'loc-2', name: 'Graz' })] })
    routes['GET /api/online/locations/loc-2/treatments'] = () => ({ status: 200, body: [] })
    render(<BookingApp />)
    fireEvent.click(await screen.findByRole('radio', { name: /Graz/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Weiter' }))

    expect(await screen.findByText(/keine online buchbaren Behandlungen/)).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Anderen Standort wählen' }))

    expect(await screen.findByRole('radio', { name: /Graz/ })).toBeTruthy()
  })

  it('Given die freien Zeiten lassen sich nicht laden When „Erneut versuchen“ gedrückt wird Then erscheinen die Zeiten', async () => {
    let fail = true
    routes['GET /api/online/locations/loc-1/availability'] = () => (fail ? { status: 500 } : { status: 200, body: availability })
    render(<BookingApp />)
    fireEvent.click(await screen.findByRole('radio', { name: /Klassische Massage/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Weiter' }))

    expect(await screen.findByText(/Das hat leider nicht geklappt/)).toBeTruthy()
    fail = false
    fireEvent.click(screen.getByRole('button', { name: 'Erneut versuchen' }))

    expect(await screen.findByRole('group', { name: /Uhrzeit/ })).toBeTruthy()
    expect(screen.queryByText(/Das hat leider nicht geklappt/)).toBeNull()
  })

  it('nimmt einen Gutscheincode mit und meldet einen ungültigen am Feld, eine Ablehnung oben', async () => {
    let reply: Reply = { status: 400, body: { errors: { voucherCode: ['ungültig'] } } }
    routes['POST /api/online/bookings'] = () => reply
    render(<BookingApp />)
    await chooseTreatmentAndTime()
    fireEvent.change(screen.getByLabelText(/^Vorname/), { target: { value: 'Lena' } })
    fireEvent.change(screen.getByLabelText(/^Nachname/), { target: { value: 'Gast' } })
    fireEvent.change(screen.getByLabelText(/^E-Mail-Adresse/), { target: { value: 'lena@example.at' } })
    fireEvent.change(screen.getByLabelText(/^Gutscheincode/), { target: { value: ' k7qm-2xpa ' } })
    fireEvent.click(screen.getByRole('checkbox', { name: /Datenschutzhinweis/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Verbindlich buchen' }))

    expect((await screen.findAllByText(/Diesen Gutschein können wir/)).length).toBe(2)
    expect(requests.find((r) => r.path === '/api/online/bookings')?.body).toMatchObject({ voucherCode: 'k7qm-2xpa' })
    await expectAccessible()

    reply = { status: 403, body: { title: 'Online nicht möglich' } }
    fireEvent.click(screen.getByRole('button', { name: 'Verbindlich buchen' }))
    expect(await screen.findByText(/Bitte rufen Sie uns an/)).toBeTruthy()
    expect(screen.getByRole('heading', { name: 'Ihre Angaben' })).toBeTruthy()
  })

  it('führt als Gast durch Behandlung, Zeit und Angaben bis zur Bestätigung', async () => {
    routes['POST /api/online/bookings'] = () => ({
      status: 201,
      body: { appointment, manageToken: 'verwalten-123', anamnesisToken: 'anamnese-456' },
    })
    render(<BookingApp />)

    // Nur ein Standort: es geht direkt mit der Behandlung los.
    expect(await screen.findByText('Schritt 1 von 3')).toBeTruthy()
    await expectAccessible()
    await chooseTreatmentAndTime()
    await expectAccessible()
    expect(screen.getByText('Online stornierbar bis 24 Stunden vor Beginn.')).toBeTruthy()
    expect(screen.getByText('Bitte kommen Sie 10 Minuten früher.')).toBeTruthy()

    fireEvent.change(screen.getByLabelText(/^Vorname/), { target: { value: 'Lena' } })
    fireEvent.change(screen.getByLabelText(/^Nachname/), { target: { value: 'Gast' } })
    fireEvent.change(screen.getByLabelText(/^E-Mail-Adresse/), { target: { value: 'lena@example.at' } })
    fireEvent.click(screen.getByRole('checkbox', { name: /Datenschutzhinweis/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Verbindlich buchen' }))

    expect(await screen.findByRole('heading', { name: 'Ihr Termin ist gebucht' })).toBeTruthy()
    expect(screen.getAllByRole('status').map((s) => s.textContent).join(' ')).toContain('lena@example.at')
    expect(screen.getByRole('link', { name: 'Gesundheitsfragebogen ausfüllen' }).getAttribute('href')).toBe('#/anamnese/anamnese-456')
    expect(screen.getByRole('link', { name: 'Termin ansehen oder stornieren' }).getAttribute('href')).toBe('#/termin/verwalten-123')
    await expectAccessible()

    const booking = requests.find((r) => r.method === 'POST' && r.path === '/api/online/bookings')
    expect(booking?.body).toMatchObject({
      locationId: 'loc-1',
      treatmentId: 'tr-1',
      start: '2027-03-01T09:15:00Z',
      contact: { firstName: 'Lena', lastName: 'Gast', email: 'lena@example.at' },
      acceptPrivacy: true,
      language: 'de',
    })
  })

  it('zeigt abweichende Preise je Uhrzeit und übernimmt sie in die Zusammenfassung', async () => {
    routes['GET /api/online/locations/loc-1/availability'] = () => ({
      status: 200,
      body: { ...availability, days: [{ ...availability.days[0], prices: [69, 79] }, availability.days[1]] },
    })
    render(<BookingApp />)
    fireEvent.click(await screen.findByRole('radio', { name: /Klassische Massage/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Weiter' }))
    const times = await screen.findByRole('group', { name: /Uhrzeit/ })

    expect(within(times).getByRole('radio', { name: '10:15' })).toBeTruthy()
    fireEvent.click(within(times).getByRole('radio', { name: /^10:00.*69,00/ }))
    await expectAccessible()
    fireEvent.click(screen.getByRole('button', { name: 'Weiter' }))

    expect(await screen.findByText(/Klassische Massage, .* 69,00/)).toBeTruthy()
  })

  it('verlangt die Zustimmung zur Ausfallgebühr und lässt danach die Karte hinterlegen', async () => {
    routes['GET /api/online/locations'] = () => ({ status: 200, body: [{ ...location, noShowFeePercent: 50 }] })
    const card = { feeAmount: 39.5, currency: 'EUR', feePercent: 50, setupUrl: null, testMode: true }
    routes['POST /api/online/bookings'] = () => ({
      status: 201,
      body: { appointment: { ...appointment, cardGuarantee: { ...card, status: 'Pending' } }, manageToken: 'verwalten-123', anamnesisToken: 'anamnese-456' },
    })
    routes['POST /api/online/bookings/verwalten-123/card/test'] = () => ({
      status: 200,
      body: { ...appointment, cardGuarantee: { ...card, status: 'Saved' } },
    })
    render(<BookingApp />)
    await chooseTreatmentAndTime()

    expect(screen.getByText(/später als 24 Stunden vorher absagen, berechnen wir .*39,50/)).toBeTruthy()
    fireEvent.change(screen.getByLabelText(/^Vorname/), { target: { value: 'Lena' } })
    fireEvent.change(screen.getByLabelText(/^Nachname/), { target: { value: 'Gast' } })
    fireEvent.change(screen.getByLabelText(/^E-Mail-Adresse/), { target: { value: 'lena@example.at' } })
    fireEvent.click(screen.getByRole('checkbox', { name: /Datenschutzhinweis/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Verbindlich buchen' }))
    // Der Fehler steht in der Übersicht (als Sprunglink) und am Feld.
    expect((await screen.findAllByText('Bitte stimmen Sie der Ausfallgebühr zu.')).length).toBe(2)
    expect(requests.some((r) => r.path === '/api/online/bookings')).toBe(false)
    await expectAccessible()

    fireEvent.click(screen.getByRole('checkbox', { name: /Ausfallgebühr von .*39,50/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Verbindlich buchen' }))
    expect(await screen.findByRole('heading', { name: 'Karte für die Ausfallgebühr' })).toBeTruthy()
    expect(requests.find((r) => r.path === '/api/online/bookings')?.body).toMatchObject({ acceptNoShowFee: true })
    await expectAccessible()

    fireEvent.click(screen.getByRole('button', { name: 'Karte hinterlegen (Test)' }))
    expect(await screen.findByText(/Karte hinterlegt/)).toBeTruthy()
  })

  it('verlangt bei Anzahlung die Zustimmung, zeigt statt der Karte die Zahlung und danach den Beleg', async () => {
    routes['GET /api/online/locations'] = () => ({ status: 200, body: [{ ...location, noShowFeePercent: 50, depositPercent: 30 }] })
    const deposit = { amount: 23.7, currency: 'EUR', percent: 30, paymentUrl: null, testMode: true, deadlineHours: 24, payUntil: null, receipt: null }
    routes['POST /api/online/bookings'] = () => ({
      status: 201,
      body: { appointment: { ...appointment, deposit: { ...deposit, status: 'Pending' } }, manageToken: 'verwalten-123', anamnesisToken: 'anamnese-456' },
    })
    routes['POST /api/online/bookings/verwalten-123/deposit/test'] = () => ({
      status: 200,
      body: { ...appointment, deposit: { ...deposit, status: 'Receipted', receipt: 'K1 Nr. 5, 01.03.2027' } },
    })
    render(<BookingApp />)
    await chooseTreatmentAndTime()

    expect(screen.getByText(/Anzahlung: Nach dem Buchen bezahlen Sie .*23,70 \(30 % des Preises\)/)).toBeTruthy()
    expect(screen.queryByText(/Ausfallgebühr:/)).toBeNull()
    fireEvent.change(screen.getByLabelText(/^Vorname/), { target: { value: 'Lena' } })
    fireEvent.change(screen.getByLabelText(/^Nachname/), { target: { value: 'Gast' } })
    fireEvent.change(screen.getByLabelText(/^E-Mail-Adresse/), { target: { value: 'lena@example.at' } })
    fireEvent.click(screen.getByRole('checkbox', { name: /Datenschutzhinweis/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Verbindlich buchen' }))
    expect((await screen.findAllByText('Bitte stimmen Sie den Bedingungen der Anzahlung zu.')).length).toBe(2)

    fireEvent.click(screen.getByRole('checkbox', { name: /Zahlung von .*23,70/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Verbindlich buchen' }))
    expect(await screen.findByRole('heading', { name: 'Anzahlung' })).toBeTruthy()
    expect(screen.queryByRole('heading', { name: 'Karte für die Ausfallgebühr' })).toBeNull()
    expect(requests.find((r) => r.path === '/api/online/bookings')?.body).toMatchObject({ acceptNoShowFee: true })
    await expectAccessible()

    fireEvent.click(screen.getByRole('button', { name: 'Zahlung bestätigen (Test)' }))
    expect(await screen.findByText(/23,70 bezahlt, Beleg K1 Nr\. 5/)).toBeTruthy()
    expect(requests.find((r) => r.path === '/api/online/bookings/verwalten-123/deposit/test')?.body).toEqual({ paid: true })
  })

  it('sagt, wenn kein Standort online buchbar ist', async () => {
    routes['GET /api/online/locations'] = () => ({ status: 200, body: [{ ...location, bookingEnabled: false }] })
    render(<BookingApp />)

    expect(await screen.findByText(/nicht online buchbar/)).toBeTruthy()
    expect(screen.queryByRole('radio')).toBeNull()
  })

  it('trägt als Gast auf die Warteliste ein, wenn nichts Passendes frei ist', async () => {
    const entry = {
      id: 'wl-1',
      locationId: 'loc-1',
      locationName: 'Wien Innere Stadt',
      treatmentId: 'tr-1',
      treatmentName: 'Klassische Massage',
      from: '2027-03-02',
      until: '2027-03-08',
      dayPart: 'Evening',
      status: 'Waiting',
      createdAt: '2027-02-20T10:00:00Z',
      lastOfferedAt: null,
    }
    routes['POST /api/online/waitlist'] = () => ({ status: 201, body: { entry, manageToken: 'warte-789' } })
    render(<BookingApp />)
    fireEvent.click(await screen.findByRole('radio', { name: /Klassische Massage/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Weiter' }))
    fireEvent.click(await screen.findByRole('radio', { name: /Di\./ }))

    fireEvent.click(screen.getByRole('button', { name: 'Auf die Warteliste' }))

    expect(await screen.findByRole('heading', { name: 'Warteliste' })).toBeTruthy()
    expect((screen.getByLabelText(/^Frühester Tag/) as HTMLInputElement).value).toBe('2027-03-02')
    expect((screen.getByLabelText(/^Spätester Tag/) as HTMLInputElement).value).toBe('2027-03-08')
    await expectAccessible()
    fireEvent.click(screen.getByRole('radio', { name: /Abends/ }))
    fireEvent.change(screen.getByLabelText(/^Vorname/), { target: { value: 'Lena' } })
    fireEvent.change(screen.getByLabelText(/^Nachname/), { target: { value: 'Gast' } })
    fireEvent.change(screen.getByLabelText(/^E-Mail-Adresse/), { target: { value: 'lena@example.at' } })
    fireEvent.click(screen.getByRole('checkbox', { name: /Datenschutzhinweis/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Auf die Warteliste setzen' }))

    expect(await screen.findByRole('heading', { name: 'Sie stehen auf der Warteliste' })).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Eintrag ansehen oder austragen' }).getAttribute('href')).toBe('#/warteliste/warte-789')
    await expectAccessible()
    expect(requests.find((r) => r.path === '/api/online/waitlist')?.body).toMatchObject({
      locationId: 'loc-1',
      treatmentId: 'tr-1',
      from: '2027-03-02',
      until: '2027-03-08',
      dayPart: 'Evening',
      contact: { firstName: 'Lena', lastName: 'Gast', email: 'lena@example.at' },
      acceptPrivacy: true,
    })
  })

  it('öffnet den Link aus der Mail der Warteliste direkt bei der Zeitwahl des Tages', async () => {
    window.location.hash = '#/?standort=00000000-0000-0000-0000-00000000000a&behandlung=00000000-0000-0000-0000-00000000000b&datum=2027-03-01'
    const id = (x: string) => `00000000-0000-0000-0000-00000000000${x}`
    routes['GET /api/online/locations'] = () => ({ status: 200, body: [{ ...location, id: id('a') }] })
    routes[`GET /api/online/locations/${id('a')}/treatments`] = () => ({ status: 200, body: [{ ...massage, id: id('b') }] })
    routes[`GET /api/online/locations/${id('a')}/availability`] = () => ({ status: 200, body: availability })
    render(<BookingApp />)

    const times = await screen.findByRole('group', { name: /Uhrzeit/ })
    expect(within(times).getByRole('radio', { name: '10:15' })).toBeTruthy()
    expect(screen.getByText(/Klassische Massage · 50 Minuten/)).toBeTruthy()
    const query = requests.find((r) => r.path.endsWith('/availability'))
    expect(query).toBeTruthy()
    await expectAccessible()
  })

  it('trägt über den Link aus der Mail von der Warteliste aus', async () => {
    const entry = {
      id: 'wl-1',
      locationId: 'loc-1',
      locationName: 'Wien Innere Stadt',
      treatmentId: 'tr-1',
      treatmentName: 'Klassische Massage',
      from: '2027-03-02',
      until: '2027-03-08',
      dayPart: 'Any',
      status: 'Waiting',
      createdAt: '2027-02-20T10:00:00Z',
      lastOfferedAt: '2027-02-25T08:00:00Z',
    }
    routes['GET /api/online/waitlist/warte-789'] = () => ({ status: 200, body: entry })
    routes['POST /api/online/waitlist/warte-789/cancel'] = () => ({ status: 200, body: { ...entry, status: 'Cancelled' } })
    window.location.hash = '#/warteliste/warte-789'
    render(<BookingApp />)

    expect(await screen.findByText('Wartet auf einen freien Termin')).toBeTruthy()
    await expectAccessible()
    fireEvent.click(screen.getByRole('button', { name: 'Austragen' }))

    expect(await screen.findByText('Sie sind von der Warteliste ausgetragen.')).toBeTruthy()
    expect(screen.getByText('Ausgetragen')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Austragen' })).toBeNull()
  })

  it('verkauft einen Wertgutschein mit Testzahlung und zeigt ihn zum Ausdrucken', async () => {
    const offer = {
      locationId: 'loc-1',
      locationName: 'Wien Innere Stadt',
      currency: 'EUR',
      available: true,
      reason: null,
      minAmount: 10,
      maxAmount: 500,
      suggestedAmounts: [25, 50, 100, 150],
      treatments: [massage],
      testPayment: true,
    }
    const order = {
      id: 'vo-1',
      status: 'Pending',
      kind: 'MultiPurpose',
      amount: 70,
      currency: 'EUR',
      locationName: 'Wien Innere Stadt',
      treatmentName: null,
      recipientName: 'Mama',
      message: 'Alles Gute!',
      code: null as string | null,
      validUntil: null,
      testPayment: true,
    }
    let current = order
    routes['GET /api/online/locations/loc-1/vouchers'] = () => ({ status: 200, body: offer })
    routes['POST /api/online/vouchers/orders'] = () => ({
      status: 201,
      body: { orderId: 'vo-1', token: 'gut-123', paymentUrl: `${window.location.origin}${window.location.pathname}#/gutschein/gut-123` },
    })
    routes['GET /api/online/vouchers/orders/gut-123'] = () => ({ status: 200, body: current })
    routes['POST /api/online/vouchers/orders/gut-123/test-payment'] = () => {
      current = { ...order, status: 'Issued', code: 'K7QM-2XPA-9RDT' }
      return { status: 200, body: { ...order, status: 'Paid' } }
    }
    window.location.hash = '#/gutschein'
    render(<BookingApp />)

    expect(await screen.findByText('Testbetrieb: Es wird kein Geld abgebucht.')).toBeTruthy()
    await expectAccessible()
    fireEvent.click(screen.getByRole('radio', { name: 'Anderer Betrag' }))
    fireEvent.change(screen.getByLabelText(/^Anderer Betrag \(EUR\)/), { target: { value: '70' } })
    fireEvent.change(screen.getByLabelText(/^Für \(freiwillig\)/), { target: { value: 'Mama' } })
    fireEvent.change(screen.getByLabelText(/^Persönliche Nachricht/), { target: { value: 'Alles Gute!' } })
    fireEvent.change(screen.getByLabelText(/^Vorname/), { target: { value: 'Gerda' } })
    fireEvent.change(screen.getByLabelText(/^Nachname/), { target: { value: 'Schenk' } })
    fireEvent.change(screen.getByLabelText(/^E-Mail-Adresse/), { target: { value: 'gerda@example.at' } })
    fireEvent.click(screen.getByRole('checkbox', { name: /Datenschutzhinweis/ }))
    expect(screen.getByText('€ 70,00')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Weiter zur Zahlung' }))

    expect(await screen.findByRole('heading', { name: 'Testzahlung' })).toBeTruthy()
    await expectAccessible()
    fireEvent.click(screen.getByRole('button', { name: 'Zahlung bestätigen (Test)' }))

    expect(await screen.findByText('K7QM-2XPA-9RDT', {}, { timeout: 4000 })).toBeTruthy()
    expect(screen.getByText('Mama')).toBeTruthy()
    await expectAccessible()
    expect(requests.find((r) => r.path === '/api/online/vouchers/orders')?.body).toMatchObject({
      locationId: 'loc-1',
      kind: 'MultiPurpose',
      amount: 70,
      treatmentId: null,
      buyer: { firstName: 'Gerda', lastName: 'Schenk', email: 'gerda@example.at' },
      recipientName: 'Mama',
      message: 'Alles Gute!',
      acceptPrivacy: true,
    })
  })

  it('bietet die Zahlungsseite des Anbieters per Knopf an', async () => {
    const order = {
      id: 'vo-2',
      status: 'Pending',
      kind: 'MultiPurpose',
      amount: 50,
      currency: 'EUR',
      locationName: 'Wien Innere Stadt',
      treatmentName: null,
      recipientName: null,
      message: null,
      code: null,
      validUntil: null,
      testPayment: false,
      paymentUrl: 'https://checkout.stripe.test/c/pay/cs_1',
    }
    routes['GET /api/online/vouchers/orders/gut-456'] = () => ({ status: 200, body: order })
    window.location.hash = '#/gutschein/gut-456'
    render(<BookingApp />)

    expect(await screen.findByText('Die Zahlung ist noch offen. Sie können sie jetzt abschließen.')).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Jetzt bezahlen' }).getAttribute('href')).toBe(order.paymentUrl)
    await expectAccessible()
  })

  it('sagt, warum es an einem Standort keine Gutscheine gibt', async () => {
    routes['GET /api/online/locations/loc-1/vouchers'] = () => ({
      status: 200,
      body: {
        locationId: 'loc-1',
        locationName: 'Wien Innere Stadt',
        currency: 'EUR',
        available: false,
        reason: 'An diesem Standort gibt es noch keine Kasse für den Gutscheinverkauf.',
        minAmount: 10,
        maxAmount: 500,
        suggestedAmounts: [],
        treatments: [],
        testPayment: false,
      },
    })
    window.location.hash = '#/gutschein'
    render(<BookingApp />)

    expect((await screen.findByRole('alert')).textContent).toContain('noch keine Kasse')
    expect(screen.queryByRole('button', { name: 'Weiter zur Zahlung' })).toBeNull()
  })

  it('schlägt zum Wunsch in eigenen Worten Zeiten vor und übernimmt die gewählte', async () => {
    routes['POST /api/online/locations/loc-1/assistant'] = () => ({
      status: 200,
      body: {
        text: 'Für Klassische Massage sind diese Zeiten frei: Mo 01.03. 10:15.',
        language: 'de',
        suggestions: [{ treatmentId: 'tr-1', treatmentName: 'Klassische Massage', start: '2027-03-01T09:15:00Z' }],
        disclosure: 'Vorschläge eines KI-Assistenten. Gebucht wird erst, wenn Sie die Buchung bestätigen.',
      },
    })
    render(<BookingApp />)

    fireEvent.change(await screen.findByLabelText(/^Ihr Wunsch in eigenen Worten/), { target: { value: 'Massage am 1. März vormittags' } })
    fireEvent.click(screen.getByRole('button', { name: 'Zeiten vorschlagen' }))
    const suggestions = await screen.findByRole('group', { name: 'Vorgeschlagene Zeiten' })
    expect(screen.getByText(/Vorschläge eines KI-Assistenten/)).toBeTruthy()
    await expectAccessible()
    fireEvent.click(within(suggestions).getByRole('button', { name: /Klassische Massage.*10:15/ }))

    await screen.findByRole('heading', { name: 'Ihre Angaben' })
    expect(screen.getAllByText(/10:15/).length).toBeGreaterThan(0)
    expect(requests.find((r) => r.path === '/api/online/locations/loc-1/assistant')?.body).toEqual({ question: 'Massage am 1. März vormittags' })
  })

  it('Given der Kundenbereich mit zwei Fehlern When abgeschickt wird Then nennt die Übersicht beide Felder als Links und ein Klick fokussiert das Feld ohne die Adresse zu ändern', async () => {
    render(<BookingApp />)
    await chooseTreatmentAndTime()
    fireEvent.change(screen.getByLabelText(/^Vorname/), { target: { value: 'Ida' } })
    fireEvent.change(screen.getByLabelText(/^Nachname/), { target: { value: 'Muster' } })
    const hashBefore = window.location.hash

    fireEvent.click(screen.getByRole('button', { name: 'Verbindlich buchen' }))

    const alert = await screen.findByRole('alert')
    const links = within(alert).getAllByRole('link')
    expect(links.map((l) => l.textContent)).toEqual(['E-Mail-Adresse: Bitte eine gültige E-Mail-Adresse eingeben.', 'Bitte bestätigen Sie den Datenschutzhinweis.'])
    fireEvent.click(links[0])
    expect(document.activeElement).toBe(screen.getByLabelText(/^E-Mail-Adresse/))
    fireEvent.click(links[1])
    expect(document.activeElement).toBe(screen.getByRole('checkbox', { name: /Datenschutzhinweis/ }))
    expect(window.location.hash).toBe(hashBefore)
  })

  it('Given das Absenden läuft When der Knopf angezeigt wird Then ist er nur aria-disabled und nicht disabled', async () => {
    render(<BookingApp />)
    await chooseTreatmentAndTime()
    fireEvent.change(screen.getByLabelText(/^Vorname/), { target: { value: 'Ida' } })
    fireEvent.change(screen.getByLabelText(/^Nachname/), { target: { value: 'Muster' } })
    fireEvent.change(screen.getByLabelText(/^E-Mail-Adresse/), { target: { value: 'ida@example.at' } })
    fireEvent.click(screen.getByRole('checkbox', { name: /Datenschutzhinweis/ }))

    routes['POST /api/online/bookings'] = () => new Promise<Reply>(() => {})
    const button = screen.getByRole('button', { name: 'Verbindlich buchen' })
    button.focus()
    fireEvent.click(button)

    await waitFor(() => expect(button.getAttribute('aria-disabled')).toBe('true'))
    expect((button as HTMLButtonElement).disabled).toBe(false)
    expect(document.activeElement).toBe(button)
  })

  it('nennt fehlende Angaben beim Feld und schickt nichts ab', async () => {
    render(<BookingApp />)
    await chooseTreatmentAndTime()

    fireEvent.click(screen.getByRole('button', { name: 'Verbindlich buchen' }))

    expect(await screen.findByText('Bitte prüfen Sie 4 Angaben.')).toBeTruthy()
    const firstName = screen.getByLabelText(/^Vorname/)
    expect(firstName.getAttribute('aria-invalid')).toBe('true')
    expect(document.getElementById(firstName.getAttribute('aria-describedby')!)?.textContent).toBe('Pflichtfeld')
    expect(requests.some((r) => r.path === '/api/online/bookings')).toBe(false)
    await expectAccessible()
  })

  it('führt zur Zeitwahl zurück, wenn die Zeit inzwischen vergeben ist', async () => {
    routes['POST /api/online/bookings'] = () => ({ status: 409, body: { message: 'vergeben', conflicts: [] } })
    render(<BookingApp />)
    await chooseTreatmentAndTime()
    fireEvent.change(screen.getByLabelText(/^Vorname/), { target: { value: 'Lena' } })
    fireEvent.change(screen.getByLabelText(/^Nachname/), { target: { value: 'Gast' } })
    fireEvent.change(screen.getByLabelText(/^E-Mail-Adresse/), { target: { value: 'lena@example.at' } })
    fireEvent.click(screen.getByRole('checkbox', { name: /Datenschutzhinweis/ }))

    fireEvent.click(screen.getByRole('button', { name: 'Verbindlich buchen' }))

    expect((await screen.findByRole('alert')).textContent).toContain('gerade vergeben')
    expect(await screen.findByRole('heading', { name: 'Tag und Uhrzeit' })).toBeTruthy()
  })

  it('braucht angemeldet mit Profil keine Kontaktdaten', async () => {
    routes['GET /api/online/me'] = () => ({
      status: 200,
      body: { email: 'mia@example.at', firstName: 'Mia', lastName: 'Konto', phone: null, hasProfile: true, passkeyCount: 0 },
    })
    routes['POST /api/online/bookings'] = () => ({ status: 201, body: { appointment, manageToken: 'a'.repeat(43), anamnesisToken: 'b'.repeat(43) } })
    render(<BookingApp />)
    await chooseTreatmentAndTime()

    expect(screen.getByText('Angemeldet als mia@example.at')).toBeTruthy()
    expect(screen.queryByLabelText(/^Vorname/)).toBeNull()
    fireEvent.click(screen.getByRole('checkbox', { name: /Datenschutzhinweis/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Verbindlich buchen' }))

    await screen.findByRole('heading', { name: 'Ihr Termin ist gebucht' })
    expect(requests.find((r) => r.path === '/api/online/bookings')?.body).toMatchObject({ contact: null })
  })

  it('nennt bei der Zeitwahl die Schließzeiten des Standorts mit dem Text des Spas oder dem Standardsatz, in der Sprache des Gastes', async () => {
    routes['GET /api/online/locations'] = () => ({
      status: 200,
      body: [
        {
          ...location,
          closures: [
            { fromDate: '2027-03-22', fromTime: null, untilDate: '2027-03-26', untilTime: null, kind: 'Vacation', text: 'Wir sind ab dem 27. März wieder für Sie da.' },
            { fromDate: '2027-04-02', fromTime: '13:00:00', untilDate: '2027-04-02', untilTime: null, kind: 'Outage', text: null },
          ],
        },
      ],
    })
    render(<BookingApp />)
    fireEvent.click(await screen.findByRole('radio', { name: /Klassische Massage/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Weiter' }))

    const notes = await screen.findByRole('note')
    expect(notes.textContent).toContain('Vom Montag, 22. März bis Freitag, 26. März geschlossen. Wir sind ab dem 27. März wieder für Sie da.')
    expect(notes.textContent).toContain('Am Freitag, 2. April ab 13:00 Uhr geschlossen. Vorübergehend geschlossen.')
    await expectAccessible()

    fireEvent.change(screen.getByLabelText('Sprache'), { target: { value: 'en' } })
    await waitFor(() => expect(screen.getByRole('note').textContent).toMatch(/Closed on Friday,? 2 April from 13:00\. Temporarily closed\./))
    fireEvent.change(screen.getByLabelText('Language'), { target: { value: 'fr' } })
    await waitFor(() => expect(screen.getByRole('note').textContent).toContain('Fermé du'))
  })

  it('wechselt auf Englisch, auch die Sprache der Seite', async () => {
    render(<BookingApp />)
    await screen.findByText('Schritt 1 von 3')

    fireEvent.change(screen.getByLabelText('Sprache'), { target: { value: 'en' } })

    expect(await screen.findByRole('heading', { name: 'Book an appointment' })).toBeTruthy()
    expect(document.documentElement.lang).toBe('en')
  })

  it('kann Französisch und Italienisch für die Schweiz, samt Erklärung zur Barrierefreiheit', async () => {
    render(<BookingApp />)
    await screen.findByText('Schritt 1 von 3')

    fireEvent.change(screen.getByLabelText('Sprache'), { target: { value: 'fr' } })
    expect(await screen.findByRole('heading', { name: 'Réserver un rendez-vous' })).toBeTruthy()
    expect(screen.getByText('Étape 1 sur 3')).toBeTruthy()
    expect(document.documentElement.lang).toBe('fr')
    await expectAccessible()

    fireEvent.change(screen.getByLabelText('Langue'), { target: { value: 'it' } })
    expect(await screen.findByRole('heading', { name: 'Prenota un appuntamento' })).toBeTruthy()
    fireEvent.click(screen.getByRole('link', { name: /accessibilità/i }))
    expect(await screen.findByRole('heading', { level: 2, name: 'Segnalare una barriera' })).toBeTruthy()
    expect(document.documentElement.lang).toBe('it')
  })

  it('zeigt das Impressum aus den Firmendaten und weist auf fehlende Angaben hin', async () => {
    routes['GET /api/online/imprint'] = () => ({
      status: 200,
      body: {
        spaName: 'Day Spa Huber',
        country: 'AT',
        legalName: 'Day Spa Huber GmbH',
        street: 'Ringstraße 1',
        postalCode: '1010',
        city: 'Wien',
        phone: null,
        email: null,
        website: null,
        vatId: 'ATU12345678',
        companyRegisterNumber: 'FN 123456a',
        companyRegisterCourt: 'Handelsgericht Wien',
        businessPurpose: null,
        supervisoryAuthority: null,
        chamberAndRules: null,
        complete: false,
      },
    })
    render(<BookingApp />)
    await screen.findByText('Schritt 1 von 3')

    fireEvent.click(screen.getByRole('link', { name: 'Impressum' }))

    expect(await screen.findByText('Day Spa Huber GmbH')).toBeTruthy()
    expect(screen.getByText('FN 123456a, Handelsgericht Wien')).toBeTruthy()
    expect(screen.getByText('ATU12345678')).toBeTruthy()
    expect(screen.getByText(/vervollständigt diese Angaben/)).toBeTruthy()
    await expectAccessible()
  })
})

describe('Anmeldung', () => {
  it('meldet per Code an und zeigt danach die eigenen Termine', async () => {
    let signedIn = false
    routes['POST /api/online/auth/code'] = () => ({ status: 202 })
    routes['POST /api/online/auth/code/verify'] = () => {
      signedIn = true
      return { status: 200, body: {} }
    }
    routes['GET /api/online/me'] = () =>
      signedIn
        ? { status: 200, body: { email: 'mia@example.at', firstName: 'Mia', lastName: 'Konto', phone: null, hasProfile: true, passkeyCount: 0 } }
        : { status: 401 }
    routes['GET /api/online/me/appointments'] = () => ({ status: 200, body: [appointment] })
    window.location.hash = '#/anmelden'
    render(<BookingApp />)

    fireEvent.change(await screen.findByLabelText(/^E-Mail-Adresse/), { target: { value: 'mia@example.at' } })
    fireEvent.click(screen.getByRole('button', { name: 'Code senden' }))
    fireEvent.change(await screen.findByLabelText(/^Code aus der E-Mail/), { target: { value: '123 456' } })
    await expectAccessible()
    fireEvent.click(screen.getByRole('button', { name: 'Anmelden' }))

    expect(await screen.findByRole('heading', { name: 'Meine Termine' })).toBeTruthy()
    expect(await screen.findByText('Klassische Massage')).toBeTruthy()
    expect(requests.find((r) => r.path === '/api/online/auth/code/verify')?.body).toEqual({ email: 'mia@example.at', code: '123456' })
    expect(screen.queryByRole('button', { name: 'Code senden' })).toBeNull()
    await expectAccessible()
  })
})

describe('Termin per Link', () => {
  it('storniert erst nach Rückfrage', async () => {
    routes['GET /api/online/bookings/tok'] = () => ({ status: 200, body: appointment })
    routes['POST /api/online/bookings/tok/cancel'] = () => ({ status: 200, body: { ...appointment, state: 'Cancelled', canCancel: false } })
    window.location.hash = '#/termin/tok'
    render(<BookingApp />)

    fireEvent.click(await screen.findByRole('button', { name: 'Termin stornieren' }))
    expect(requests.some((r) => r.path.endsWith('/cancel'))).toBe(false)
    // Fokus wandert mit: erst auf „Nein“ in der Rückfrage, nach „Nein“ zurück auf „Termin stornieren“.
    expect(document.activeElement).toBe(screen.getByRole('button', { name: 'Nein, behalten' }))
    expect(screen.getByRole('group', { name: 'Wirklich stornieren?' })).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Nein, behalten' }))
    expect(document.activeElement).toBe(screen.getByRole('button', { name: 'Termin stornieren' }))
    fireEvent.click(screen.getByRole('button', { name: 'Termin stornieren' }))
    fireEvent.click(screen.getByRole('button', { name: 'Ja, stornieren' }))

    expect(await screen.findByText('Storniert')).toBeTruthy()
    expect(screen.getAllByRole('status').map((s) => s.textContent).join(' ')).toContain('storniert')
    expect(document.activeElement?.textContent).toContain('storniert')
    await expectAccessible()
  })

  it('sagt per Knopf zu und zeigt den Hinweis der Behandlung', async () => {
    const hinted = { ...appointment, guestHint: 'Bitte 15 Minuten früher kommen.' }
    routes['GET /api/online/bookings/tok'] = () => ({ status: 200, body: hinted })
    routes['POST /api/online/bookings/tok/confirm'] = () => ({ status: 200, body: { ...hinted, confirmedAt: '2027-02-27T10:00:00Z' } })
    window.location.hash = '#/termin/tok'
    render(<BookingApp />)

    expect(await screen.findByText('Bitte 15 Minuten früher kommen.')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Ich komme' }))

    expect(await screen.findByText('Sie haben zugesagt.')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Ich komme' })).toBeNull()
    expect(screen.getAllByRole('status').map((s) => s.textContent).join(' ')).toContain('bestätigt')
    await expectAccessible()
  })

  it('plant die Route zum Standort ohne Standortabfrage und zeigt den Anreise-Text darunter', async () => {
    const geolocation = vi.fn()
    Object.defineProperty(navigator, 'geolocation', { value: { getCurrentPosition: geolocation, watchPosition: geolocation }, configurable: true })
    const withRoute = {
      ...appointment,
      locationContact: { street: 'Graben 1', postalCode: '1010', city: 'Wien', phone: null, email: null, directions: 'U1 bis Stephansplatz, Garage Am Graben 2 Minuten zu Fuß.' },
    }
    routes['GET /api/online/bookings/tok'] = () => ({ status: 200, body: withRoute })
    window.location.hash = '#/termin/tok'
    render(<BookingApp />)

    const route = await screen.findByRole('link', { name: /Route planen/ })
    expect(route.getAttribute('href')).toBe('https://www.google.com/maps/dir/?api=1&destination=Graben%201%2C%201010%20Wien')
    expect(route.getAttribute('target')).toBe('_blank')
    expect(route.getAttribute('rel')).toBe('noopener noreferrer')
    expect(screen.getByText('Graben 1, 1010 Wien')).toBeTruthy()
    expect(screen.getByText('U1 bis Stephansplatz, Garage Am Graben 2 Minuten zu Fuß.', { exact: false })).toBeTruthy()
    // Der Startpunkt ist der Standort des Geräts; den erfragt die Karten-App, nicht das Kundenportal.
    expect(geolocation).not.toHaveBeenCalled()
    await expectAccessible()
  })

  it('zeigt ohne Anschrift und Anreise-Text keinen Knopf „Route planen“', async () => {
    routes['GET /api/online/bookings/tok'] = () => ({
      status: 200,
      body: { ...appointment, locationContact: { street: null, postalCode: null, city: null, phone: '+43 1 234', email: null, directions: null } },
    })
    window.location.hash = '#/termin/tok'
    render(<BookingApp />)

    expect(await screen.findByText('Klassische Massage')).toBeTruthy()
    expect(screen.queryByRole('link', { name: /Route planen/ })).toBeNull()
    expect(screen.queryByText(/Anreise/)).toBeNull()
  })

  it('verschiebt den Termin selbst auf eine freie Zeit', async () => {
    const movable = { ...appointment, treatmentId: 'tr-1', canMove: true }
    routes['GET /api/online/bookings/tok'] = () => ({ status: 200, body: movable })
    routes['POST /api/online/bookings/tok/move'] = () => ({
      status: 200,
      body: { ...movable, start: '2027-03-01T09:00:00Z', end: '2027-03-01T09:50:00Z', canMove: false },
    })
    window.location.hash = '#/termin/tok'
    render(<BookingApp />)

    fireEvent.click(await screen.findByRole('button', { name: 'Verschieben' }))
    const times = await screen.findByRole('group', { name: /Uhrzeit/ })
    fireEvent.click(within(times).getByRole('radio', { name: '10:00' }))
    await expectAccessible()
    fireEvent.click(screen.getByRole('button', { name: 'Auf diese Zeit verschieben' }))

    expect(await screen.findByText(/Der Termin ist verschoben/)).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Verschieben' })).toBeNull()
    expect(requests.find((r) => r.path.endsWith('/move'))?.body).toEqual({ start: '2027-03-01T09:00:00Z' })
  })

  it('lässt über den Link der Rezeption erst zustimmen und dann die Karte hinterlegen', async () => {
    const card = { feeAmount: 39.5, currency: 'EUR', feePercent: 50, setupUrl: null, testMode: true, deadlineHours: 24 }
    routes['GET /api/online/bookings/tok'] = () => ({ status: 200, body: { ...appointment, cardGuarantee: { ...card, status: 'AwaitingConsent' } } })
    routes['POST /api/online/bookings/tok/card/accept'] = () => ({ status: 200, body: { ...appointment, cardGuarantee: { ...card, status: 'Pending' } } })
    window.location.hash = '#/termin/tok'
    render(<BookingApp />)

    const agree = await screen.findByRole('button', { name: 'Zustimmen und Karte hinterlegen' })
    expect(agree.hasAttribute('disabled')).toBe(true)
    fireEvent.click(screen.getByRole('checkbox', { name: /Ich stimme der Ausfallgebühr von .*39,50 zu/ }))
    fireEvent.click(agree)

    expect(await screen.findByRole('button', { name: 'Karte hinterlegen (Test)' })).toBeTruthy()
    expect(requests.some((r) => r.path.endsWith('/card/accept'))).toBe(true)
    await expectAccessible()
  })

  it('nennt die Frist für die Karte und meldet einen freigegebenen Termin', async () => {
    const card = { feeAmount: 39.5, currency: 'EUR', feePercent: 50, setupUrl: null, testMode: true, deadlineHours: 24 }
    routes['GET /api/online/bookings/tok'] = () => ({
      status: 200,
      body: { ...appointment, cardGuarantee: { ...card, status: 'Pending', holdUntil: '2027-03-01T10:00:00Z' } },
    })
    window.location.hash = '#/termin/tok'
    const { unmount } = render(<BookingApp />)

    expect(await screen.findByText(/Bitte bis .* hinterlegen, sonst geben wir den Termin/)).toBeTruthy()
    unmount()
    routes['GET /api/online/bookings/tok'] = () => ({
      status: 200,
      body: { ...appointment, state: 'Cancelled', canCancel: false, cardGuarantee: { ...card, status: 'Released', holdUntil: null } },
    })
    render(<BookingApp />)

    expect(await screen.findByText(/Ohne hinterlegte Karte haben wir den Termin freigegeben/)).toBeTruthy()
    await expectAccessible()
  })

  it('nimmt die Ausfallgebühr der Behandlung und erklärt die Regel für neue Gäste', async () => {
    routes['GET /api/online/locations'] = () => ({ status: 200, body: [{ ...location, noShowFeePercent: 50, noShowFeeNewCustomersOnly: true }] })
    routes['GET /api/online/locations/loc-1/treatments'] = () => ({ status: 200, body: [{ ...massage, noShowFeePercent: 20 }] })
    render(<BookingApp />)
    await chooseTreatmentAndTime()

    expect(screen.getByText(/berechnen wir .*15,80.*\(20 % des Preises\)/)).toBeTruthy()
    expect(screen.getByText(/nur für Gäste, die zum ersten Mal bei uns sind/)).toBeTruthy()
    await expectAccessible()
  })

  it('meldet einen ungültigen Link', async () => {
    window.location.hash = '#/termin/falsch'
    render(<BookingApp />)

    expect((await screen.findByRole('alert')).textContent).toContain('ungültig')
  })
})

describe('Einwilligung per Link', () => {
  it('bestätigt erst mit dem Knopf und lässt danach widerrufen', async () => {
    const consent = { spaName: 'Spa am See', firstName: 'Mara', state: 'Pending' }
    routes['GET /api/online/consent/einw-123'] = () => ({ status: 200, body: consent })
    routes['POST /api/online/consent/einw-123/confirm'] = () => ({ status: 200, body: { ...consent, state: 'Confirmed' } })
    routes['POST /api/online/consent/einw-123/withdraw'] = () => ({ status: 200, body: { ...consent, state: 'Withdrawn' } })
    window.location.hash = '#/einwilligung/einw-123'
    render(<BookingApp />)

    expect(await screen.findByText(/Spa am See erinnert Sie per Mail/)).toBeTruthy()
    expect(requests.some((r) => r.method === 'POST')).toBe(false)
    await expectAccessible()
    fireEvent.click(screen.getByRole('button', { name: 'Ja, erinnern Sie mich' }))

    expect(await screen.findByText('Danke, Ihre Einwilligung ist bestätigt.')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Ja, erinnern Sie mich' })).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: 'Keine Erinnerungen mehr' }))
    expect(await screen.findByText('Sie bekommen keine Erinnerungen an die nächste Behandlung mehr.')).toBeTruthy()
    expect(requests.filter((r) => r.method === 'POST').map((r) => r.path)).toEqual([
      '/api/online/consent/einw-123/confirm',
      '/api/online/consent/einw-123/withdraw',
    ])
  })
})

describe('Gesundheitsfragebogen', () => {
  const form = {
    version: '2026-10',
    language: 'de',
    intro: 'Ihre Angaben helfen uns.',
    consentText: 'Ich willige ein.',
    questions: [
      { key: 'allergies', kind: 'YesNo', label: 'Haben Sie Allergien?', detailsLabel: 'Welche?', required: true },
      { key: 'other', kind: 'Text', label: 'Sonst noch etwas?', detailsLabel: null, required: false },
    ],
  }

  it('verlangt Antworten und Einwilligung und sendet dann ab', async () => {
    routes['GET /api/online/anamnesis/form'] = () => ({ status: 200, body: form })
    routes['GET /api/online/anamnesis/tok'] = () => ({
      status: 200,
      body: { firstName: 'Lena', treatmentName: 'Klassische Massage', start: appointment.start, timeZone: 'Europe/Vienna', lastSubmittedAt: null, channel: 'Link' },
    })
    routes['POST /api/online/anamnesis/tok'] = () => ({ status: 204 })
    window.location.hash = '#/anamnese/tok'
    render(<BookingApp />)

    await screen.findByRole('group', { name: /Haben Sie Allergien/ })
    await expectAccessible()
    fireEvent.click(screen.getByRole('button', { name: 'Fragebogen absenden' }))
    expect(await screen.findByText('Bitte prüfen Sie 3 Angaben.')).toBeTruthy()

    fireEvent.click(within(screen.getByRole('group', { name: /Haben Sie Allergien/ })).getByRole('radio', { name: 'Ja' }))
    fireEvent.change(screen.getByLabelText('Welche?'), { target: { value: 'Nüsse' } })
    fireEvent.click(screen.getByRole('checkbox', { name: /^Ich willige ein\./ }))
    fireEvent.change(screen.getByLabelText(/^Vor- und Nachname/), { target: { value: 'Lena Gast' } })
    fireEvent.click(screen.getByRole('button', { name: 'Fragebogen absenden' }))

    expect(await screen.findByText(/Ihr Fragebogen ist bei uns angekommen/)).toBeTruthy()
    await waitFor(() =>
      expect(requests.find((r) => r.method === 'POST' && r.path === '/api/online/anamnesis/tok')?.body).toEqual({
        answers: [
          { key: 'allergies', yes: true, details: 'Nüsse' },
          { key: 'other', yes: null, details: null },
        ],
        consentHealthData: true,
        signature: 'Lena Gast',
        language: 'de',
      }),
    )
  })

  it('am Tablet: Sprache aus dem Link, danach zurück an die Rezeption ohne Links ins Konto', async () => {
    localStorage.setItem('aispa.sprache', 'de')
    routes['GET /api/online/anamnesis/form'] = () => ({ status: 200, body: { ...form, language: 'en' } })
    routes['GET /api/online/anamnesis/tab'] = () => ({
      status: 200,
      body: { firstName: 'Lena', treatmentName: null, start: null, timeZone: null, lastSubmittedAt: null, channel: 'Tablet' },
    })
    routes['POST /api/online/anamnesis/tab'] = () => ({ status: 204 })
    window.location.hash = '#/anamnese/tab?lang=en'
    render(<BookingApp />)

    await screen.findByRole('group', { name: /Haben Sie Allergien/ })
    expect(document.documentElement.lang).toBe('en')
    fireEvent.click(within(screen.getByRole('group', { name: /Haben Sie Allergien/ })).getByRole('radio', { name: 'No' }))
    fireEvent.click(screen.getByRole('checkbox', { name: /^Ich willige ein\./ }))
    fireEvent.change(screen.getByLabelText(/^First and last name/), { target: { value: 'Lena Gast' } })
    fireEvent.click(screen.getByRole('button', { name: 'Submit questionnaire' }))

    expect(await screen.findByText('Thank you! Please return the tablet to reception now.')).toBeTruthy()
    expect(screen.queryByRole('link', { name: 'Book another appointment' })).toBeNull()
  })
})
