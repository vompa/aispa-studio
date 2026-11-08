import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import axe from 'axe-core'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { SignupForm } from './SignupForm'

vi.mock('../auth/passkeys', () => ({ addPasskey: vi.fn(async () => true) }))

function mockSignup(status: number, body: unknown) {
  const calls: unknown[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (request: Request) => {
      calls.push(JSON.parse(await request.clone().text()))
      return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
    }),
  )
  return calls
}

/** Pflichtfelder tragen einen Stern (aria-hidden) im Label-Text. */
const byLabel = (label: string) => screen.getByLabelText((text) => text.replace(/ [*]$/, '') === label)

function fill() {
  fireEvent.change(byLabel('Name des Spas'), { target: { value: 'Day Spa Linz' } })
  fireEvent.change(byLabel('Land'), { target: { value: 'DE' } })
  fireEvent.change(byLabel('Ihr Name'), { target: { value: 'Maria' } })
  fireEvent.change(byLabel('E-Mail'), { target: { value: 'maria@example.at' } })
  submit()
}

const submit = () => fireEvent.click(screen.getByRole('button', { name: 'Spa registrieren und Passkey anlegen' }))

describe('Spa registrieren', () => {
  afterEach(() => {
    cleanup()
    vi.unstubAllGlobals()
  })

  it('Given das Registrierformular When axe läuft Then gibt es keinen Verstoß', async () => {
    const { container } = render(<SignupForm onSignedUp={async () => {}} />)
    const result = await axe.run(container, { rules: { 'color-contrast': { enabled: false } } })
    expect(result.violations.map((v) => `${v.id}: ${v.nodes.map((n) => n.html).join(' | ')}`)).toEqual([])
  }, 30000)

  it('legt das Spa an und danach den Passkey', async () => {
    const calls = mockSignup(200, {})
    const onSignedUp = vi.fn(async () => {})
    render(<SignupForm onSignedUp={onSignedUp} />)

    fill()

    await vi.waitFor(() => expect(onSignedUp).toHaveBeenCalledWith(null))
    expect(calls[0]).toEqual({ spaName: 'Day Spa Linz', country: 'DE', ownerName: 'Maria', ownerEmail: 'maria@example.at' })
  })

  it('Given die Registrierung ohne Eingaben When abgeschickt wird Then steht der Fehler am ersten Pflichtfeld, der Fokus liegt dort und es geht kein Aufruf ab', () => {
    const calls = mockSignup(200, {})
    render(<SignupForm onSignedUp={vi.fn()} />)

    submit()

    const name = byLabel('Name des Spas')
    expect(name.getAttribute('aria-describedby')).toBe(screen.getByText('Der Name des Spas fehlt.').id)
    expect(name.getAttribute('aria-invalid')).toBe('true')
    expect(document.activeElement).toBe(name)
    expect(screen.getByText('Der eigene Name fehlt.')).toBeTruthy()
    expect(calls).toEqual([])
  })

  it('Given eine E-Mail ohne @ When abgeschickt wird Then steht der Fehler am Feld „E-Mail“', () => {
    const calls = mockSignup(200, {})
    render(<SignupForm onSignedUp={vi.fn()} />)
    fireEvent.change(byLabel('Name des Spas'), { target: { value: 'Day Spa Linz' } })
    fireEvent.change(byLabel('Ihr Name'), { target: { value: 'Maria' } })
    fireEvent.change(byLabel('E-Mail'), { target: { value: 'maria' } })

    submit()

    const email = byLabel('E-Mail')
    expect(email.getAttribute('aria-describedby')).toBe(screen.getByText('Die E-Mail-Adresse ist unvollständig. Sie braucht ein @.').id)
    expect(document.activeElement).toBe(email)
    expect(calls).toEqual([])
  })

  it('zeigt die Begründung des Servers', async () => {
    mockSignup(400, { errors: { OwnerEmail: ['Für diese E-Mail gibt es schon ein Konto.'] } })
    render(<SignupForm onSignedUp={vi.fn()} />)

    fill()

    expect((await screen.findByRole('alert')).textContent).toContain('schon ein Konto')
  })
})
