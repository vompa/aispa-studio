import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { MyDayApp } from './MyDayApp'
import { START, calls, countSession, stubApi, therapist, towel } from './stockFixtures'

const BY_BARCODE = 'GET /api/v1/stock/items/by-barcode'
const lookup = (request: Request) => (new URL(request.url).searchParams.get('code') === towel.barcode ? { status: 200, body: towel } : { status: 404 })

beforeEach(() => {
  // Die Uhr steht still: „derselbe Code innerhalb 1 s“ gilt dann sicher, unabhängig von der Last.
  vi.useFakeTimers({ toFake: ['Date'] })
  vi.setSystemTime(new Date('2027-03-01T08:30:00Z'))
  localStorage.clear()
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
  vi.useRealTimers()
  Reflect.deleteProperty(window, 'BarcodeDetector')
})

async function openCount() {
  render(<MyDayApp />)
  fireEvent.click(await screen.findByRole('button', { name: 'Inventur' }))
  fireEvent.change(await screen.findByLabelText('Lagerort'), { target: { value: 'pl-2' } })
  fireEvent.click(screen.getByRole('button', { name: 'Zählung starten' }))
  await screen.findByLabelText(/Handtuch/)
}

/** Kamera und Erkennung als Stub: `stop` zählt die beendeten Spuren, `detect` liefert dauernd denselben Code. */
function stubCamera(getUserMedia = vi.fn(async () => ({ getTracks: () => [{ stop }] }))) {
  vi.stubGlobal('navigator', { ...navigator, mediaDevices: { getUserMedia } })
  HTMLMediaElement.prototype.play = vi.fn(async () => undefined)
  class FakeDetector {
    detect = vi.fn(async () => [{ rawValue: towel.barcode, format: 'ean_13' }])
  }
  Object.defineProperty(window, 'BarcodeDetector', { value: FakeDetector, configurable: true, writable: true })
  return { stop, getUserMedia }
}
const stop = vi.fn()

describe('Kamera-Scan', () => {
  beforeEach(() => stop.mockClear())

  it('Given_kein_BarcodeDetector_When_die_Maske_offen_ist_Then_steht_nur_das_Feld_Code_eingeben_und_ein_Code_springt_zur_Zeile', async () => {
    stubApi(therapist, { [START]: () => ({ status: 200, body: countSession('Open') }), [BY_BARCODE]: lookup })
    await openCount()

    expect(screen.queryByRole('button', { name: 'Kamera starten' })).toBeNull()
    expect(screen.getByText(/Kamera-Scan ist auf diesem Gerät nicht verfügbar/)).toBeTruthy()
    const field = screen.getByLabelText('Code eingeben')
    fireEvent.change(field, { target: { value: towel.barcode } })
    fireEvent.keyDown(field, { key: 'Enter' })

    await waitFor(() => expect(document.activeElement).toBe(screen.getByLabelText(/Handtuch/)))
  })

  it('Given_ein_BarcodeDetector_der_einen_Code_liefert_When_der_Scan_laeuft_Then_springt_die_Maske_zur_Zeile_und_die_Kamera_wird_beendet', async () => {
    stubCamera()
    stubApi(therapist, { [START]: () => ({ status: 200, body: countSession('Open') }), [BY_BARCODE]: lookup })
    await openCount()

    fireEvent.click(screen.getByRole('button', { name: 'Kamera starten' }))

    await waitFor(() => expect(document.activeElement).toBe(screen.getByLabelText(/Handtuch/)))
    expect(stop).not.toHaveBeenCalled()
    fireEvent.click(screen.getByRole('button', { name: 'Kamera beenden' }))
    expect(stop).toHaveBeenCalledTimes(1)
  })

  it('Given_die_Kamera_bleibt_auf_dem_Code_When_sie_mehrmals_liest_Then_wird_der_Code_nur_einmal_gesucht', async () => {
    stubCamera()
    stubApi(therapist, { [START]: () => ({ status: 200, body: countSession('Open') }), [BY_BARCODE]: lookup })
    await openCount()

    fireEvent.click(screen.getByRole('button', { name: 'Kamera starten' }))
    await waitFor(() => expect(calls('GET', '/api/v1/stock/items/by-barcode')).toHaveLength(1))
    await new Promise((resolve) => setTimeout(resolve, 900))

    expect(calls('GET', '/api/v1/stock/items/by-barcode')).toHaveLength(1)
  })

  it('Given_die_Kamera_laeuft_When_die_Seite_verlassen_wird_Then_wird_die_Kamera_beendet', async () => {
    stubCamera()
    stubApi(therapist, { [START]: () => ({ status: 200, body: countSession('Open') }), [BY_BARCODE]: lookup })
    await openCount()
    fireEvent.click(screen.getByRole('button', { name: 'Kamera starten' }))
    await screen.findByRole('button', { name: 'Kamera beenden' })

    fireEvent.click(screen.getByRole('button', { name: 'Tag' }))

    expect(stop).toHaveBeenCalledTimes(1)
  })

  it('Given_die_Kamera_wird_nicht_freigegeben_When_Lena_sie_starten_will_Then_bleibt_das_Feld_mit_einer_Meldung', async () => {
    stubCamera(vi.fn(async () => Promise.reject(new DOMException('no', 'NotAllowedError'))))
    stubApi(therapist, { [START]: () => ({ status: 200, body: countSession('Open') }) })
    await openCount()

    fireEvent.click(screen.getByRole('button', { name: 'Kamera starten' }))

    expect(await screen.findByText(/Die Kamera ist nicht freigegeben/)).toBeTruthy()
    expect(screen.getByLabelText('Code eingeben')).toBeTruthy()
  })
})
