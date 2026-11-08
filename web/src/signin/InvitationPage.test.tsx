import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import axe from 'axe-core'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { InvitationPage } from './InvitationPage'

vi.mock('../auth/passkeys', () => ({ addPasskey: vi.fn(async () => true) }))

const preview = { displayName: 'Lena Huber', email: 'lena@spa.test', role: 'Reception', locationNames: ['Wien'] }

function mockInvitation(acceptStatus: number) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (request: Request) => {
      const path = new URL(request.url).pathname
      if (path.endsWith('/accept')) return new Response(null, { status: acceptStatus })
      return new Response(JSON.stringify(preview), { status: 200, headers: { 'Content-Type': 'application/json' } })
    }),
  )
}

describe('Einladung annehmen', () => {
  afterEach(() => {
    cleanup()
    vi.unstubAllGlobals()
  })

  it('Given eine gültige Einladung When axe läuft Then gibt es keinen Verstoß', async () => {
    mockInvitation(200)
    const { container } = render(<InvitationPage token="t" onAccepted={async () => {}} />)
    await screen.findByRole('heading', { name: 'Willkommen, Lena Huber' })
    const result = await axe.run(container, { rules: { 'color-contrast': { enabled: false } } })
    expect(result.violations.map((v) => `${v.id}: ${v.nodes.map((n) => n.html).join(' | ')}`)).toEqual([])
  }, 30000)

  it('Given der Server lehnt das Annehmen ab When der Knopf gedrückt wird Then steht die Meldung in einer Warnung (role=alert) und der Knopf ist wieder frei', async () => {
    mockInvitation(500)
    render(<InvitationPage token="t" onAccepted={async () => {}} />)
    fireEvent.click(await screen.findByRole('button', { name: 'Einladung annehmen und Passkey anlegen' }))

    expect((await screen.findByRole('alert')).textContent).toBe('Die Einladung konnte nicht angenommen werden.')
    expect(screen.getByRole('button', { name: 'Einladung annehmen und Passkey anlegen' }).getAttribute('aria-disabled')).toBeNull()
  })
})
