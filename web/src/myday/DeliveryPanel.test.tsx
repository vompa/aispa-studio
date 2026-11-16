import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { MyDayApp } from './MyDayApp'
import { calls, manager, reception, stubApi } from './stockFixtures'

const ORDERS = 'GET /api/v1/stock/locations/loc-1/orders'
const SCAN = 'POST /api/v1/stock/orders/po-1/delivery-note-scan'
const DELIVER = 'POST /api/v1/stock/orders/po-1/deliveries'

const line = (id: string, itemName: string, unit: string, quantity: number, received = 0) => ({
  id,
  itemId: `item-${id}`,
  itemName,
  unit,
  supplierArticleNumber: null,
  quantity,
  unitCost: 0.1,
  received,
  packageText: null,
})
const order = (status = 'Ordered') => ({
  id: 'po-1',
  number: 12,
  locationId: 'loc-1',
  supplier: 'Aroma GmbH',
  status,
  note: null,
  createdAt: '2027-02-25T08:00:00Z',
  orderedAt: '2027-02-25T09:00:00Z',
  closedAt: null,
  total: 120,
  lines: [line('l-1', 'Arganöl', 'ml', 1000), line('l-2', 'Meersalz', 'g', 2000)],
})
const photo = () => new File([new Uint8Array([0xff, 0xd8, 0xff])], 'lieferschein.jpg', { type: 'image/jpeg' })

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

async function openDelivery() {
  render(<MyDayApp />)
  fireEvent.click(await screen.findByRole('button', { name: 'Lieferung' }))
  await screen.findByLabelText('Bestellung')
}

describe('Lieferung am Handy', () => {
  it('Given_ein_Lieferschein_Foto_When_die_KI_es_gelesen_hat_Then_stehen_die_Mengen_zum_Pruefen_da_und_gebucht_wird_erst_mit_dem_Knopf', async () => {
    stubApi(manager, {
      [ORDERS]: () => ({ status: 200, body: [order(), { ...order('Received'), id: 'po-0', number: 11 }] }),
      [SCAN]: () => ({
        status: 200,
        body: {
          read: true,
          deliveryNote: 'LS-118',
          lines: [
            { lineId: 'l-1', quantity: 1000, batch: 'L-77', expiresOn: '2028-06-30', uncertain: false, reason: null },
            { lineId: 'l-2', quantity: 1500.5, batch: null, expiresOn: null, uncertain: true, reason: 'Menge von Hand korrigiert' },
          ],
          unmatched: ['1 Karton Duftkerzen'],
          note: null,
        },
      }),
      [DELIVER]: () => ({ status: 200, body: order('PartiallyReceived') }),
    })
    await openDelivery()
    // Nur offene Bestellungen stehen zur Wahl.
    expect(screen.getAllByRole('option').map((o) => o.textContent)).toEqual(['Nr. 12 · Aroma GmbH'])

    fireEvent.change(screen.getByLabelText('Lieferschein fotografieren (optional)'), { target: { files: [photo()] } })

    expect(await screen.findByText('Von der KI gelesen.')).toBeTruthy()
    expect((screen.getByLabelText('Geliefert (ml): Arganöl') as HTMLInputElement).value).toBe('1000')
    expect((screen.getByLabelText('Charge (optional): Arganöl') as HTMLInputElement).value).toBe('L-77')
    expect((screen.getByLabelText('Geliefert (g): Meersalz') as HTMLInputElement).value).toBe('1500,5')
    expect(screen.getByText('Bitte prüfen: Menge von Hand korrigiert')).toBeTruthy()
    expect(screen.getByText('1 Karton Duftkerzen')).toBeTruthy()
    expect((screen.getByLabelText('Lieferscheinnummer (optional)') as HTMLInputElement).value).toBe('LS-118')
    expect(calls('POST', '/api/v1/stock/orders/po-1/deliveries')).toHaveLength(0)

    fireEvent.change(screen.getByLabelText('Geliefert (g): Meersalz'), { target: { value: '1500' } })
    expect(screen.queryByText(/Bitte prüfen/)).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: 'Lieferung buchen' }))

    expect(await screen.findByText('Lieferung zu Bestellung 12 gebucht: 2 Positionen.')).toBeTruthy()
    expect(calls('POST', '/api/v1/stock/orders/po-1/deliveries').map((r) => r.body)).toEqual([
      {
        lines: [
          { lineId: 'l-1', quantity: 1000, batch: 'L-77', expiresOn: '2028-06-30' },
          { lineId: 'l-2', quantity: 1500, batch: null, expiresOn: null },
        ],
        deliveryNote: 'LS-118',
      },
    ])
  })

  it('Given_keine_KI_eingerichtet_When_ein_Foto_gewaehlt_wird_Then_steht_der_Hinweis_und_von_Hand_geht_es_weiter', async () => {
    stubApi(manager, {
      [ORDERS]: () => ({ status: 200, body: [order()] }),
      [SCAN]: () => ({ status: 200, body: { read: false, deliveryNote: null, lines: [], unmatched: [], note: 'Das Lesen von Lieferscheinen ist nicht eingerichtet. Bitte die Mengen von Hand eintragen.' } }),
      [DELIVER]: () => ({ status: 200, body: order('PartiallyReceived') }),
    })
    await openDelivery()

    fireEvent.change(screen.getByLabelText('Lieferschein fotografieren (optional)'), { target: { files: [photo()] } })
    expect((await screen.findByRole('alert')).textContent).toContain('nicht eingerichtet')

    fireEvent.click(screen.getByRole('button', { name: 'Lieferung buchen' }))
    expect(await screen.findByText('Bitte bei mindestens einer Position die gelieferte Menge eintragen.')).toBeTruthy()
    fireEvent.change(screen.getByLabelText('Geliefert (ml): Arganöl'), { target: { value: '250' } })
    fireEvent.click(screen.getByRole('button', { name: 'Lieferung buchen' }))

    expect(await screen.findByText('Lieferung zu Bestellung 12 gebucht: 1 Position.')).toBeTruthy()
    expect(calls('POST', '/api/v1/stock/orders/po-1/deliveries').map((r) => r.body)).toEqual([
      { lines: [{ lineId: 'l-1', quantity: 250, batch: null, expiresOn: null }], deliveryNote: null },
    ])
  })

  it('Given_die_Rezeption_When_Mein_Tag_offen_ist_Then_fehlt_der_Reiter_Lieferung', async () => {
    stubApi(reception)
    render(<MyDayApp />)

    await screen.findByRole('button', { name: 'Wareneingang' })

    expect(screen.queryByRole('button', { name: 'Lieferung' })).toBeNull()
  })

  it('Given_keine_offene_Bestellung_Then_sagt_die_Maske_das', async () => {
    stubApi(manager, { [ORDERS]: () => ({ status: 200, body: [order('Received')] }) })
    render(<MyDayApp />)
    fireEvent.click(await screen.findByRole('button', { name: 'Lieferung' }))

    expect(await screen.findByText('Keine offene Bestellung an diesem Standort.')).toBeTruthy()
  })
})
