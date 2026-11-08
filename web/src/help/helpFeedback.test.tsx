import { act, cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import { closeHelp, openHelp } from './help'
import { HelpApp } from './HelpApp'
import { HelpDrawer } from './HelpDrawer'
import { listFeedback } from './feedback'
import { loadTopics } from './topics'

// Der erste Import aller Hilfetexte dauert unter Last länger als die 1 s von findBy…; danach liegen sie im Speicher.
beforeAll(() => loadTopics(), 60_000)

const TOPIC = 'kalender-termin-verschieben'

beforeEach(() => localStorage.clear())
afterEach(() => {
  act(() => closeHelp())
  cleanup()
})

async function openTopic() {
  render(<HelpDrawer />)
  act(() => openHelp(TOPIC))
  await screen.findByText('War das hilfreich?')
}

describe('Rückmeldung je Hilfethema', () => {
  it('Given ein Hilfethema When „Nein“ und ein Text gesendet werden Then liegt genau ein Eintrag im Speicher und die Seite zeigt den Dank', async () => {
    await openTopic()

    fireEvent.click(screen.getByRole('button', { name: 'Nein' }))
    fireEvent.change(screen.getByRole('textbox', { name: /ergänzen/ }), { target: { value: 'Das Ziehen auf dem Tablet fehlt.' } })
    fireEvent.click(screen.getByRole('button', { name: 'Senden' }))

    const entries = listFeedback()
    expect(entries).toHaveLength(1)
    expect(entries[0]).toMatchObject({ topic: TOPIC, answer: 'no', text: 'Das Ziehen auf dem Tablet fehlt.' })
    expect(Number.isNaN(Date.parse(entries[0].at))).toBe(false)
    expect(screen.getByRole('status').textContent).toContain('Danke für die Rückmeldung')
    expect(screen.queryByText('War das hilfreich?')).toBeNull()
    expect(document.activeElement).toBe(screen.getByRole('status'))
  })

  it('Given eine gesendete Rückmeldung When das Thema erneut geöffnet wird Then steht die Frage nicht noch einmal', async () => {
    await openTopic()
    fireEvent.click(screen.getByRole('button', { name: 'Ja' }))
    fireEvent.click(screen.getByRole('button', { name: 'Senden' }))
    cleanup()
    act(() => closeHelp())

    render(<HelpDrawer />)
    act(() => openHelp(TOPIC))
    await screen.findByRole('navigation', { name: 'Brotkrumen' })
    expect(screen.queryByText('War das hilfreich?')).toBeNull()
    expect(listFeedback()).toHaveLength(1)
  })

  it('Given eine Rückmeldung When der Eintrag geprüft wird Then enthält er nur Themenkennung, Antwort, Text und Zeit', async () => {
    await openTopic()
    fireEvent.click(screen.getByRole('button', { name: 'Ja' }))
    fireEvent.click(screen.getByRole('button', { name: 'Senden' }))

    expect(Object.keys(listFeedback()[0]).sort()).toEqual(['answer', 'at', 'text', 'topic'])
    expect(JSON.parse(localStorage.getItem('aispa.help.feedback') ?? '[]')).toEqual(listFeedback())
  })

  it('Given die Hilfe-Seite When ein anderes Thema gewählt wird Then fragt sie dort neu', async () => {
    window.history.replaceState(null, '', `/help/?thema=${TOPIC}`)
    render(<HelpApp />)
    await screen.findByText('War das hilfreich?')
    fireEvent.click(screen.getByRole('button', { name: 'Ja' }))
    fireEvent.click(screen.getByRole('button', { name: 'Senden' }))

    act(() => openHelp('kalender-termin-stornieren'))
    await screen.findByRole('heading', { level: 1, name: /stornieren/i })
    expect(screen.getByText('War das hilfreich?')).toBeTruthy()
  })

  it('Given die Hilfe-Seite mit gesendeter Rückmeldung When sie neu geöffnet wird Then steht die Frage nicht noch einmal', async () => {
    window.history.replaceState(null, '', `/help/?thema=${TOPIC}`)
    render(<HelpApp />)
    await screen.findByText('War das hilfreich?')
    fireEvent.click(screen.getByRole('button', { name: 'Nein' }))
    fireEvent.click(screen.getByRole('button', { name: 'Senden' }))
    cleanup()

    render(<HelpApp />)
    await screen.findByRole('heading', { level: 1 })
    expect(screen.queryByText('War das hilfreich?')).toBeNull()
  })

  it('Given ein Speicher, der nicht schreibt When gesendet wird Then bleibt die Hilfe bedienbar', async () => {
    await openTopic()
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('voll')
    })
    fireEvent.click(screen.getByRole('button', { name: 'Ja' }))
    fireEvent.click(screen.getByRole('button', { name: 'Senden' }))
    expect(screen.getByRole('status').textContent).toContain('Danke')
    vi.restoreAllMocks()
  })
})
