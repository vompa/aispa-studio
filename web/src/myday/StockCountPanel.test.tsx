import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { MyDayApp } from './MyDayApp'
import { LINE, START, SUBMIT, calls, countLine, countSession, oil, reception, stubApi, therapist, towel } from './stockFixtures'

const SAVE_WAIT = { timeout: 4000 }
const LINES = '/api/v1/stock/locations/loc-1/count-sessions/cs-1/lines'

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date'] })
  vi.setSystemTime(new Date('2027-03-01T08:30:00Z'))
  localStorage.clear()
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
  vi.useRealTimers()
})

/** Reiter „Inventur“, Raumschrank 2 wählen und die Zählung starten. */
async function openCount() {
  render(<MyDayApp />)
  fireEvent.click(await screen.findByRole('button', { name: 'Inventur' }))
  fireEvent.change(await screen.findByLabelText('Lagerort'), { target: { value: 'pl-2' } })
  fireEvent.click(screen.getByRole('button', { name: 'Zählung starten' }))
  await screen.findByLabelText(/Massageöl neutral/)
}

describe('Inventur am Handy', () => {
  it('Given_Lena_gibt_zwei_Mengen_ein_When_sie_abschickt_Then_gehen_PUT_je_Zeile_und_submit_an_die_API', async () => {
    stubApi(therapist, {
      [START]: () => ({ status: 200, body: countSession('Open') }),
      [LINE(oil.id)]: () => ({ status: 200, body: countSession('Open', [countLine(oil, 120.5)]) }),
      [LINE(towel.id)]: () => ({ status: 200, body: countSession('Open', [countLine(oil, 120.5), countLine(towel, 8)]) }),
      [SUBMIT]: () => ({ status: 200, body: countSession('Submitted', [countLine(oil, 120.5), countLine(towel, 8)]) }),
    })
    await openCount()

    fireEvent.change(screen.getByLabelText(/Massageöl neutral/), { target: { value: '120,5' } })
    fireEvent.change(screen.getByLabelText(/Handtuch/), { target: { value: '8' } })
    await waitFor(() => expect(calls('PUT', `${LINES}/m-2`)).toHaveLength(1), SAVE_WAIT)
    expect(await screen.findByText('Gespeichert')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Abschicken' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Jetzt abschicken' }))

    expect(await screen.findByText('Die Leitung schließt die Zählung ab.')).toBeTruthy()
    expect(calls('PUT', `${LINES}/m-1`).map((r) => r.body)).toEqual([{ counted: 120.5 }])
    expect(calls('PUT', `${LINES}/m-2`).map((r) => r.body)).toEqual([{ counted: 8 }])
    expect(calls('POST', '/api/v1/stock/locations/loc-1/count-sessions/cs-1/submit')).toHaveLength(1)
  })

  it('Given_ein_Zwischenstand_When_die_Seite_neu_geladen_wird_Then_stehen_die_Mengen_wieder_im_Feld', async () => {
    stubApi(therapist, { [START]: () => ({ status: 200, body: countSession('Open', [countLine(oil, 120), countLine(towel, 8)]) }) })

    await openCount()

    expect((screen.getByLabelText(/Massageöl neutral/) as HTMLInputElement).value).toBe('120')
    expect((screen.getByLabelText(/Handtuch/) as HTMLInputElement).value).toBe('8')
  })

  it('Given_ein_Fehler_beim_Speichern_When_Lena_tippt_Then_steht_eine_Meldung_und_Abschicken_ist_gesperrt', async () => {
    stubApi(therapist, {
      [START]: () => ({ status: 200, body: countSession('Open', [countLine(towel, 8)]) }),
      [LINE(oil.id)]: () => ({ status: 500 }),
    })
    await openCount()

    fireEvent.change(screen.getByLabelText(/Massageöl neutral/), { target: { value: '120' } })

    const alert = await screen.findByRole('alert', {}, SAVE_WAIT)
    expect(alert.textContent).toContain('Nicht gespeichert')
    const submit = screen.getByRole('button', { name: 'Abschicken' })
    expect(submit.getAttribute('aria-disabled')).toBe('true')
    fireEvent.click(submit)
    expect(screen.queryByRole('alertdialog')).toBeNull()
    expect(calls('POST', '/api/v1/stock/locations/loc-1/count-sessions/cs-1/submit')).toHaveLength(0)
  })

  it('Given_kein_Netz_beim_Zaehlen_When_es_wieder_da_ist_Then_wird_die_gemerkte_Menge_gesendet_und_Abschicken_geht', async () => {
    let offline = true
    stubApi(therapist, {
      [START]: () => ({ status: 200, body: countSession('Open') }),
      [LINE(oil.id)]: () => {
        if (offline) throw new TypeError('Failed to fetch')
        return { status: 200, body: countSession('Open', [countLine(oil, 120)]) }
      },
    })
    await openCount()

    fireEvent.change(screen.getByLabelText(/Massageöl neutral/), { target: { value: '120' } })

    expect(await screen.findByText(/1 Menge wartet auf Verbindung\./, {}, SAVE_WAIT)).toBeTruthy()
    expect(screen.queryByText(/Nicht gespeichert/)).toBeNull()
    expect(screen.getByRole('button', { name: 'Abschicken' }).getAttribute('aria-disabled')).toBe('true')
    expect(JSON.parse(localStorage.getItem('aispa-mein-tag-count:cs-1')!)).toEqual({ 'm-1': '120' })

    offline = false
    window.dispatchEvent(new Event('online'))

    // Erneutes Senden ist ein Speichern wie jedes andere: dieselbe Wartezeit wie oben, sonst reicht unter Last in der CI eine Sekunde nicht.
    expect(await screen.findByText('Gespeichert', {}, SAVE_WAIT)).toBeTruthy()
    expect(screen.queryByText(/wartet auf Verbindung/)).toBeNull()
    expect(localStorage.getItem('aispa-mein-tag-count:cs-1')).toBeNull()
    expect(screen.getByRole('button', { name: 'Abschicken' }).getAttribute('aria-disabled')).toBeNull()
  })

  it('Given_eine_gemerkte_Menge_When_die_Zaehlung_fortgesetzt_wird_Then_steht_sie_im_Feld_und_geht_gleich_an_die_API', async () => {
    // Dasselbe Handy, dieselbe Person: Nur dann bleiben gemerkte Mengen über die Anmeldung hinweg stehen.
    localStorage.setItem('aispa-mein-tag-owner', therapist.id)
    localStorage.setItem('aispa-mein-tag-count:cs-1', JSON.stringify({ 'm-1': '95,5' }))
    localStorage.setItem('aispa-mein-tag-count:cs-alt', JSON.stringify({ 'm-2': '3' }))
    stubApi(therapist, {
      [START]: () => ({ status: 200, body: countSession('Open', [countLine(oil, 120)]) }),
      [LINE(oil.id)]: () => ({ status: 200, body: countSession('Open', [countLine(oil, 95.5)]) }),
    })

    await openCount()

    expect((screen.getByLabelText(/Massageöl neutral/) as HTMLInputElement).value).toBe('95,5')
    await waitFor(() => expect(calls('PUT', `${LINES}/m-1`).map((r) => r.body)).toEqual([{ counted: 95.5 }]), SAVE_WAIT)
    await waitFor(() => expect(localStorage.getItem('aispa-mein-tag-count:cs-1')).toBeNull())
    // Die gemerkten Mengen einer älteren Zählung sind weg: Sie ist abgeschlossen oder verworfen.
    expect(localStorage.getItem('aispa-mein-tag-count:cs-alt')).toBeNull()
  })

  it('Given_gemerkte_Mengen_einer_anderen_Person_When_Lena_sich_anmeldet_Then_sind_sie_geloescht', async () => {
    localStorage.setItem('aispa-mein-tag-owner', 'u-andere')
    localStorage.setItem('aispa-mein-tag-count:cs-1', JSON.stringify({ 'm-1': '95,5' }))
    stubApi(therapist, { [START]: () => ({ status: 200, body: countSession('Open', [countLine(oil, 120)]) }) })

    await openCount()

    expect((screen.getByLabelText(/Massageöl neutral/) as HTMLInputElement).value).toBe('120')
    expect(localStorage.getItem('aispa-mein-tag-count:cs-1')).toBeNull()
    expect(calls('PUT', `${LINES}/m-1`)).toHaveLength(0)
  })

  it('Given_eine_abgeschickte_Zaehlung_When_Lena_sie_oeffnet_Then_steht_sie_nur_lesend_da', async () => {
    stubApi(therapist, { [START]: () => ({ status: 200, body: countSession('Submitted', [countLine(oil, 120)]) }) })
    render(<MyDayApp />)
    fireEvent.click(await screen.findByRole('button', { name: 'Inventur' }))
    fireEvent.change(await screen.findByLabelText('Lagerort'), { target: { value: 'pl-2' } })
    fireEvent.click(screen.getByRole('button', { name: 'Zählung starten' }))

    expect(await screen.findByText('Die Leitung schließt die Zählung ab.')).toBeTruthy()
    expect((screen.getByLabelText(/Massageöl neutral/) as HTMLInputElement).readOnly).toBe(true)
    expect(screen.queryByRole('button', { name: 'Abschicken' })).toBeNull()
  })
})

describe('Wareneingang am Handy', () => {
  const BY_BARCODE = 'GET /api/v1/stock/items/by-barcode'
  const MOVEMENTS = 'POST /api/v1/stock/locations/loc-1/movements'

  it('Given_die_Rezeption_When_Wareneingang_gescannt_und_gebucht_wird_Then_geht_GoodsReceipt_mit_Menge_an_die_API', async () => {
    stubApi(reception, {
      [BY_BARCODE]: (request) => (new URL(request.url).searchParams.get('code') === towel.barcode ? { status: 200, body: towel } : { status: 404 }),
      [MOVEMENTS]: () => ({ status: 200, body: [] }),
    })
    render(<MyDayApp />)
    fireEvent.click(await screen.findByRole('button', { name: 'Wareneingang' }))

    fireEvent.change(await screen.findByLabelText('Code eingeben'), { target: { value: towel.barcode } })
    fireEvent.click(screen.getByRole('button', { name: 'Suchen' }))
    fireEvent.change(await screen.findByLabelText('Menge (Stk)'), { target: { value: '24' } })
    fireEvent.change(screen.getByLabelText('Charge (optional)'), { target: { value: 'L-77' } })
    fireEvent.click(screen.getByRole('button', { name: 'Wareneingang buchen' }))

    expect(await screen.findByText('24 Stk Handtuch gebucht.')).toBeTruthy()
    expect(calls('POST', '/api/v1/stock/locations/loc-1/movements').map((r) => r.body)).toMatchObject([
      { itemId: towel.id, kind: 'GoodsReceipt', quantity: 24, batch: 'L-77' },
    ])
  })

  it('Given_eine_Therapeutin_When_Mein_Tag_offen_ist_Then_fehlt_der_Reiter_Wareneingang', async () => {
    stubApi(therapist)
    render(<MyDayApp />)

    await screen.findByRole('button', { name: 'Inventur' })

    expect(screen.queryByRole('button', { name: 'Wareneingang' })).toBeNull()
  })
})
