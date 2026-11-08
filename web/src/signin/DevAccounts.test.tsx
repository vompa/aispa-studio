import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { DevAccounts } from './DevAccounts'

const accounts = [
  { email: 'inhaberin@demo.aispa.test', displayName: 'Katrin Aigner', role: 'Owner', locations: ['alle Standorte'] },
  { email: 'empfang.wien@demo.aispa.test', displayName: 'Nina Kovač', role: 'Reception', locations: ['Wien Innere Stadt'] },
]

function mockApi(listStatus: number, loginStatus = 200) {
  const logins: unknown[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      if (url.endsWith('/dev/login')) {
        logins.push(JSON.parse(String(init?.body)))
        return new Response('{}', { status: loginStatus })
      }
      return new Response(JSON.stringify(listStatus === 200 ? accounts : {}), { status: listStatus })
    }),
  )
  return logins
}

describe('Testkonten', () => {
  afterEach(() => {
    cleanup()
    vi.unstubAllGlobals()
  })

  it('meldet mit dem gewählten Testkonto an', async () => {
    const logins = mockApi(200)
    const onSignedIn = vi.fn()
    render(<DevAccounts onSignedIn={onSignedIn} onFailed={vi.fn()} />)

    const select = await screen.findByLabelText('Testkonto des Beispiel-Spas')
    expect(screen.getByRole('option', { name: 'Nina Kovač · Rezeption · Wien Innere Stadt' })).toBeTruthy()
    fireEvent.change(select, { target: { value: 'empfang.wien@demo.aispa.test' } })
    fireEvent.click(screen.getByRole('button', { name: 'Als Testkonto anmelden (nur Entwicklung)' }))

    await vi.waitFor(() => expect(onSignedIn).toHaveBeenCalledWith('Angemeldet als Nina Kovač.'))
    expect(logins).toEqual([{ email: 'empfang.wien@demo.aispa.test' }])
  })

  it('Given die Anmeldung mit dem Testkonto schlägt fehl When geklickt wird Then gilt die Person nicht als angemeldet und es kommt eine Fehlermeldung', async () => {
    mockApi(200, 500)
    const onSignedIn = vi.fn()
    const onFailed = vi.fn()
    render(<DevAccounts onSignedIn={onSignedIn} onFailed={onFailed} />)

    fireEvent.click(await screen.findByRole('button', { name: 'Als Testkonto anmelden (nur Entwicklung)' }))

    await vi.waitFor(() => expect(onFailed).toHaveBeenCalledTimes(1))
    expect(onSignedIn).not.toHaveBeenCalled()
  })

  it('zeigt nichts, wenn die API keine Testkonten anbietet', async () => {
    mockApi(404)
    const { container } = render(<DevAccounts onSignedIn={vi.fn()} onFailed={vi.fn()} />)
    await new Promise((resolve) => setTimeout(resolve, 20))
    expect(container.innerHTML).toBe('')
  })
})
