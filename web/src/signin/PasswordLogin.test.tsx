import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import axe from 'axe-core'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { PasswordLogin } from './PasswordLogin'

let sent: unknown
let loginStatus: number

beforeEach(() => {
  sent = undefined
  loginStatus = 200
  vi.stubGlobal(
    'fetch',
    vi.fn(async (request: Request) => {
      sent = JSON.parse(await request.clone().text())
      return new Response(JSON.stringify({}), { status: loginStatus, headers: { 'Content-Type': 'application/json' } })
    }),
  )
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

/** Pflichtfelder tragen einen Stern (aria-hidden) im Label-Text. */
const byLabel = (label: string) => screen.getByLabelText((text) => text.replace(/ [*]$/, '') === label)

describe('Anmeldung mit Passwort im Einzelplatz', () => {
  it('Given die Anmeldung ohne Eingaben When abgeschickt wird Then steht der Fehler am ersten Feld, der Fokus liegt dort und es geht kein Aufruf ab', () => {
    render(<PasswordLogin onSignedIn={async () => {}} />)

    fireEvent.click(screen.getByRole('button', { name: 'Anmelden' }))

    const userName = byLabel('Benutzername')
    const error = screen.getByText('Der Benutzername fehlt.')
    expect(userName.getAttribute('aria-describedby')).toBe(error.id)
    expect(userName.getAttribute('aria-invalid')).toBe('true')
    expect(document.activeElement).toBe(userName)
    expect(screen.getByText('Das Passwort fehlt.')).toBeTruthy()
    expect(sent).toBeUndefined()
  })

  it('Given ein falsches Passwort When abgeschickt wird Then steht die Meldung in einer Warnung und es wird nicht angemeldet', async () => {
    loginStatus = 401
    const onSignedIn = vi.fn(async () => {})
    render(<PasswordLogin onSignedIn={onSignedIn} />)
    fireEvent.change(byLabel('Benutzername'), { target: { value: 'katrin' } })
    fireEvent.change(byLabel('Passwort'), { target: { value: 'falsch' } })

    fireEvent.click(screen.getByRole('button', { name: 'Anmelden' }))

    expect((await screen.findByRole('alert')).textContent).toBe('Benutzername oder Passwort stimmt nicht. Beides prüfen und noch einmal versuchen.')
    expect(onSignedIn).not.toHaveBeenCalled()
  })

  it('Given die Anmeldung When axe läuft Then gibt es keinen Verstoß', async () => {
    const { container } = render(<PasswordLogin onSignedIn={async () => {}} />)
    const result = await axe.run(container, { rules: { 'color-contrast': { enabled: false } } })
    expect(result.violations.map((v) => `${v.id}: ${v.nodes.map((n) => n.html).join(' | ')}`)).toEqual([])
  }, 30000)
})
