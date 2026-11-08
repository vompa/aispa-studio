import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { api } from './api'
import { applySpaFromUrl } from './spa'

const remembered = '11111111-1111-1111-1111-111111111111'
let sent: (string | null)[]

async function callApi() {
  await api.GET('/api/online/me')
  return sent.at(-1)
}

beforeEach(() => {
  sent = []
  localStorage.setItem('aispa.spa', remembered)
  window.history.replaceState(null, '', '/buchen/')
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: Request) => {
      sent.push(input.headers.get('X-Aispa-Spa'))
      return new Response('{}', { status: 200, headers: { 'Content-Type': 'application/json' } })
    }),
  )
})

afterEach(() => {
  localStorage.removeItem('aispa.spa')
  window.location.hash = ''
  vi.unstubAllGlobals()
})

describe('Gemerktes Spa', () => {
  it('Given ein gemerktes Spa When die App ohne Mail-Link läuft Then geht es mit; bei einem Mail-Link nicht', async () => {
    applySpaFromUrl()

    window.location.hash = '#/termine'
    expect(await callApi()).toBe(remembered)
    window.location.hash = '#/anamnese'
    expect(await callApi()).toBe(remembered)

    for (const link of ['#/termin/abc', '#/warteliste/abc', '#/einwilligung/abc', '#/gutschein/abc', '#/anamnese/abc?lang=en']) {
      window.location.hash = link
      expect(await callApi()).toBeNull()
    }
  })
})
