import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeAll, describe, expect, it } from 'vitest'
import { HelpApp } from './HelpApp'
import { closeHelp } from './help'
import { buildHelpUrl, readHelpUrl } from './helpUrl'
import { loadTopics } from './topics'

// Der erste Import aller Hilfetexte dauert unter Last länger als die 1 s von findBy…; danach liegen sie im Speicher.
beforeAll(() => loadTopics(), 60_000)

function openPage(search: string) {
  window.history.replaceState(null, '', `/help/${search}`)
  return render(<HelpApp />)
}

describe('Hilfe-Seite (/help/)', () => {
  afterEach(() => {
    act(() => closeHelp())
    cleanup()
  })

  it('liest und baut die Adresse mit Thema, Abschnitt, Oberfläche und Rolle', () => {
    expect(readHelpUrl('?thema=kassa&abschnitt=barzahlungsrabatt&oberflaeche=windows-app&rolle=rezeption&n=3')).toEqual({
      topic: 'kassa',
      section: 'barzahlungsrabatt',
      surface: 'windows-app',
      role: 'rezeption',
    })
    expect(readHelpUrl('')).toEqual({ topic: null, section: undefined, surface: undefined, role: undefined })
    expect(buildHelpUrl({ topic: 'kassa', section: 'barzahlungsrabatt' }, { topic: null, surface: 'windows-app', role: 'rezeption' })).toBe(
      '?thema=kassa&abschnitt=barzahlungsrabatt&oberflaeche=windows-app&rolle=rezeption',
    )
    expect(buildHelpUrl({ topic: null }, { topic: null })).toBe('')
    expect(buildHelpUrl({ topic: null, section: 'x' }, { topic: null })).toBe('')
  })

  it('öffnet das Thema und den Abschnitt aus der Adresse', async () => {
    openPage('?thema=kassa&abschnitt=barzahlungsrabatt&n=2')

    const target = await screen.findByRole('heading', { name: /Barzahlungsrabatt/ })
    expect(target.className).toBe('help-target')
    // Unter dem Titel (h1) stehen die Abschnitte als h2, ihre Unterabschnitte als h3 (in der Schublade h3 und h4).
    expect(target.tagName).toBe('H3')
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1)
    expect(within(screen.getByRole('main')).getByRole('heading', { level: 1 }).textContent).toBe('Kassa')
    expect(screen.getByRole('navigation', { name: 'Themen' }).querySelector('[aria-current="page"]')?.textContent).toBe('Kassa')
    await waitFor(() => expect(document.title).toBe('Kassa · Hilfe · AISPA Studio'))
    // Der erste Stand ersetzt den Eintrag und lässt die laufende Nummer weg.
    await waitFor(() => expect(window.location.search).toBe('?thema=kassa&abschnitt=barzahlungsrabatt'))
  })

  it('wechselt das Thema über die Liste und die Suche und schreibt es in die Adresse', async () => {
    openPage('?thema=kassa')
    await screen.findByRole('heading', { level: 1, name: 'Kassa' })

    fireEvent.change(screen.getByRole('searchbox', { name: 'Hilfe durchsuchen' }), { target: { value: 'Termin verschieben' } })
    fireEvent.click(screen.getAllByRole('button', { name: /Termin verschieben/ })[0])

    expect(await screen.findByRole('heading', { level: 1, name: 'Termin verschieben' })).toBeTruthy()
    await waitFor(() => expect(window.location.search).toMatch(/^\?thema=kalender-termin-verschieben/))
    // Bilder ohne Verweis auf die volle Größe: die Seite zeigt sie so groß, wie sie passen.
    expect(screen.getAllByRole('img').length).toBeGreaterThan(0)
    expect(screen.getAllByRole('img')[0].closest('a')).toBeNull()
  })

  it('sagt die Trefferzahl der Suche höflich in einer Statuszeile an', async () => {
    openPage('')
    await screen.findByRole('heading', { level: 1, name: 'Hilfe' })
    // Die Zeile steht schon vor der ersten Suche im Dokument (sonst wird die erste Zahl nicht angesagt) und ist leer.
    const status = screen.getByRole('status')
    expect(status.textContent).toBe('')
    expect(status.getAttribute('aria-live')).toBe('polite')

    const search = screen.getByRole('searchbox', { name: 'Hilfe durchsuchen' })
    fireEvent.change(search, { target: { value: 'Termin verschieben' } })
    expect(screen.getByRole('status')).toBe(status)
    expect(status.textContent).toMatch(/^(Ein|Die \d+ besten|\d+) Treffer$/)

    fireEvent.change(search, { target: { value: 'qzxwvu' } })
    expect(status.textContent).toBe('Nichts gefunden. Anders formulieren oder die KI fragen.')

    fireEvent.change(search, { target: { value: '' } })
    expect(status.textContent).toBe('')
  })

  it('zeigt ohne Thema die Übersicht und nennt ein unbekanntes Thema', async () => {
    openPage('')
    expect(await screen.findByRole('heading', { level: 1, name: 'Hilfe' })).toBeTruthy()
    // Seit Slice 145 ordnet die Navigation die Themen nach Bereich in einklappbaren Gruppen.
    expect(screen.getByText('Kalender', { selector: 'summary' })).toBeTruthy()
    expect(screen.getByRole('heading', { level: 2, name: 'Erste Schritte' })).toBeTruthy()
    act(() => closeHelp())
    cleanup()

    openPage('?thema=gibt-es-nicht')
    expect((await screen.findByText(/gibt es noch keinen Hilfetext/)).textContent).toContain('gibt-es-nicht')
  })

  it('nennt ein unbekanntes Thema als „Thema nicht gefunden“ und führt mit „Übersicht öffnen“ zurück', async () => {
    openPage('?thema=gibt-es-nicht&abschnitt=x')
    expect(await screen.findByRole('heading', { level: 1, name: 'Thema nicht gefunden' })).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: 'Übersicht öffnen' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Hilfe' })).toBeTruthy()
    expect(screen.queryByText(/gibt es noch keinen Hilfetext/)).toBeNull()
    await waitFor(() => expect(window.location.search).toBe(''))
  })

  it('Zurück und Vor gehen durch die besuchten Themen', async () => {
    openPage('?thema=kassa')
    await screen.findByRole('heading', { level: 1, name: 'Kassa' })

    fireEvent.click(screen.getAllByRole('button', { name: 'Anmelden' })[0])
    await screen.findByRole('heading', { level: 1, name: 'Anmelden' })

    window.history.back()
    await new Promise((resolve) => setTimeout(resolve, 50))
    expect(await screen.findByRole('heading', { level: 1, name: 'Kassa' })).toBeTruthy()
  })
})
