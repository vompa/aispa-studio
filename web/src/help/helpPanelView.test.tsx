import { act, cleanup, render, screen } from '@testing-library/react'
import { afterEach, beforeAll, describe, expect, it } from 'vitest'
import { HelpApp } from './HelpApp'
import { closeHelp } from './help'
import { buildHelpUrl, readHelpUrl } from './helpUrl'
import { loadTopics } from './topics'

// Der erste Import aller Hilfetexte dauert unter Last länger als die 1 s von findBy…; danach liegen sie im Speicher.
beforeAll(() => loadTopics(), 60_000)

function openPanel(search: string) {
  window.history.replaceState(null, '', `/help/${search}`)
  return render(<HelpApp />)
}

// Hilfe als Leiste (ADR 0079, Nachtrag 17.11.2026): die Windows-App zeigt die Hilfe zu einem Fragezeichen in der Leiste „KI und Hilfe“,
// mit derselben Hilfe-Seite im Modus `ansicht=leiste`: nur der Abschnitt, ohne Kopf und Navigation.
describe('Hilfe in der Leiste (ansicht=leiste)', () => {
  afterEach(() => {
    act(() => closeHelp())
    cleanup()
  })

  it('liest die Ansicht aus der Adresse und behält sie beim Weiterblättern', () => {
    expect(readHelpUrl('?thema=firmendaten&abschnitt=unternehmen&ansicht=leiste').view).toBe('leiste')
    expect(readHelpUrl('?thema=firmendaten&ansicht=irgendwas').view).toBeUndefined()
    expect(buildHelpUrl({ topic: 'kassa' }, { topic: null, view: 'leiste' })).toBe('?thema=kassa&ansicht=leiste')
  })

  it('zeigt nur den Abschnitt ohne Navigation, Titel und Überschrift des Abschnitts', async () => {
    openPanel('?thema=firmendaten&abschnitt=unternehmen&ansicht=leiste&eingebettet=1&oberflaeche=windows-app')

    expect(await screen.findByText(/Angaben, die das Gesetz auf Rechnungen/)).toBeTruthy()
    expect(screen.getByRole('heading', { name: 'Firmenwortlaut mit Rechtsform' }).tagName).toBe('H4')
    expect(screen.queryByRole('navigation')).toBeNull()
    expect(screen.queryByRole('heading', { level: 1 })).toBeNull()
    expect(screen.queryByRole('heading', { name: 'Unternehmen' })).toBeNull()
    expect(screen.queryByRole('heading', { name: 'Bankverbindung' })).toBeNull()
    expect(screen.getByRole('main').className).toContain('help-panel-view')
  })

  it('zeigt ohne Abschnitt oder bei unbekanntem Abschnitt das ganze Thema', async () => {
    openPanel('?thema=firmendaten&abschnitt=gibt-es-nicht&ansicht=leiste')

    expect(await screen.findByRole('heading', { name: 'Unternehmen' })).toBeTruthy()
    expect(screen.getByRole('heading', { name: 'Bankverbindung' })).toBeTruthy()
    expect(screen.queryByRole('navigation')).toBeNull()
  })

  it('nennt ein unbekanntes Thema ohne Navigation', async () => {
    openPanel('?thema=gibt-es-nicht&ansicht=leiste')

    expect(await screen.findByText('Zu dieser Stelle gibt es noch keinen Hilfetext.')).toBeTruthy()
    expect(screen.queryByRole('navigation')).toBeNull()
  })
})
