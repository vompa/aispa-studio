import { act, cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, beforeAll, describe, expect, it } from 'vitest'
import { HelpApp } from './HelpApp'
import { HelpBlocks } from './HelpDrawer'
import { HelpIndex } from './HelpIndex'
import { TopicFoot, TopicHead } from './HelpTopicParts'
import { closeHelp } from './help'
import { highlight, searchHelp } from './search'
import { loadTopics, relatedTopics, toTopic } from './topics'
import shiftSource from './content/kalender-termin-verschieben.md?raw'

beforeAll(() => loadTopics(), 60_000)

const topicOf = (source: string, id = 'probe') => toTopic(id, source)

const threeSections = `---
art: ablauf
bereich: Kalender
rollen: rezeption, leitung
---
# Probe

Kurzsatz.

## Eins {#eins}

Text mit [Kassa](hilfe:kassa) und [Anmelden](hilfe:anmeldung-anmelden).

## Zwei {#zwei}

Siehe [Kassa](hilfe:kassa) und [Probe selbst](hilfe:probe).

## Drei {#drei}

Ende.
`

afterEach(() => {
  act(() => closeHelp())
  cleanup()
})

describe('Seitenaufbau der Hilfe', () => {
  it('Given ein Thema mit drei h2 When es angezeigt wird Then stehen Brotkrumen, drei Einträge unter „Auf dieser Seite“ und die verwandten Themen aus seinen Links', async () => {
    const topics = await loadTopics()
    const topic = topicOf(threeSections)
    render(
      <>
        <TopicHead topic={topic} />
        <TopicFoot topic={topic} topics={topics} />
      </>,
    )
    const crumbs = screen.getByRole('navigation', { name: 'Brotkrumen' })
    expect(within(crumbs).getByText('Kalender')).toBeTruthy()
    expect(within(crumbs).getByText('Probe').getAttribute('aria-current')).toBe('page')
    const toc = screen.getByRole('navigation', { name: 'Auf dieser Seite' })
    expect(within(toc).getAllByRole('link').map((a) => a.textContent)).toEqual(['Eins', 'Zwei', 'Drei'])
    const related = screen.getByRole('navigation', { name: 'Verwandte Themen' })
    // Zwei Ziele, einmal je Thema; der Verweis auf das Thema selbst zählt nicht.
    expect(within(related).getAllByRole('button').map((b) => b.textContent)).toEqual(['Kassa', 'Anmelden'])
    expect(screen.getByText(/Für:.*Rezeption.*Leitung/)).toBeTruthy()
  })

  it('zeigt auf der Seite eines echten Themas einen Eintrag je h2 und die Brotkrumen mit Bereich', async () => {
    window.history.replaceState(null, '', '/help/?thema=kalender-termin-verschieben')
    render(<HelpApp />)
    await screen.findByRole('heading', { level: 1, name: 'Termin verschieben' })
    const expected = (shiftSource.match(/^## /gm) ?? []).length
    const toc = screen.getByRole('navigation', { name: 'Auf dieser Seite' })
    expect(within(toc).getAllByRole('link')).toHaveLength(expected)
    expect(within(screen.getByRole('navigation', { name: 'Brotkrumen' })).getByText('Kalender')).toBeTruthy()
    expect(screen.getByRole('navigation', { name: 'Verwandte Themen' })).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Zurück zur Übersicht' }))
    expect(await screen.findByRole('heading', { level: 1, name: 'Hilfe' })).toBeTruthy()
  }, 20_000)

  it('Given ein Thema ohne h2 und ohne Links When es angezeigt wird Then gibt es weder Inhaltsverzeichnis noch verwandte Themen', async () => {
    const topics = await loadTopics()
    const topic = topicOf('# Kurz\n\nNur ein Satz.\n', 'kurz')
    render(
      <>
        <TopicHead topic={topic} />
        <TopicFoot topic={topic} topics={topics} />
      </>,
    )
    expect(screen.queryByRole('navigation', { name: 'Auf dieser Seite' })).toBeNull()
    expect(screen.queryByRole('navigation', { name: 'Verwandte Themen' })).toBeNull()
    expect(screen.getByRole('button', { name: 'Zurück zur Übersicht' })).toBeTruthy()
  })

  it('findet verwandte Themen nur unter den vorhandenen', async () => {
    const topics = await loadTopics()
    const topic = topicOf('# X\n\nSiehe [nichts](hilfe:gibt-es-nicht).\n')
    expect(relatedTopics(topic, topics)).toEqual([])
  })
})

describe('Hinweiskästen', () => {
  it('Given ein Absatz „> Achtung: …“ When gerendert Then ist er ein Kasten mit Symbol und role="note"', () => {
    const topic = topicOf(
      '# T\n\n> Achtung: Erst speichern.\n> Sonst geht es verloren.\n\n> Tipp: Mit **Esc** abbrechen.\n\n> Hinweis: Nur lesen.\n',
    )
    const { container } = render(<HelpBlocks blocks={topic.blocks} />)
    const notes = screen.getAllByRole('note')
    expect(notes).toHaveLength(3)
    expect(notes[0].className).toContain('notice')
    expect(notes[0].className).toContain('warning')
    expect(notes[0].textContent).toBe('Achtung: Erst speichern. Sonst geht es verloren.')
    expect(notes[1].className).toContain('ok')
    expect(notes[2].className).not.toMatch(/warning|error|ok/)
    // Das Symbol zeichnet `.notice::before`; im Text trägt das fette Stichwort die Art.
    expect(container.querySelectorAll('.notice > strong:first-child')).toHaveLength(3)
    expect(notes[1].querySelector('strong')?.textContent).toBe('Tipp:')
  })

  it('zeigt für ein fehlendes Bild einen Platzhalter mit Hinweis', () => {
    const topic = topicOf('# T\n\n![Das Fenster](images/app/gibt-es-nicht.png)\n')
    render(<HelpBlocks blocks={topic.blocks} />)
    expect(screen.getByText(/Bild folgt: Das Fenster/)).toBeTruthy()
  })
})

describe('Suche', () => {
  it('Given die Suche nach „Kasse“ When Treffer da sind Then nennt ein role="status" die Zahl und jeder Treffer hebt „Kasse“ hervor', async () => {
    const topics = await loadTopics()
    render(<HelpIndex topics={topics} />)
    fireEvent.change(screen.getByRole('searchbox', { name: 'Hilfe durchsuchen' }), { target: { value: 'Kasse' } })
    expect(screen.getByRole('status').textContent).toMatch(/^(Ein|Die \d+ besten|\d+) Treffer$/)
    const items = screen.getAllByRole('listitem')
    expect(items.length).toBeGreaterThan(0)
    for (const item of items) {
      const marks = [...item.querySelectorAll('mark')].map((m) => m.textContent?.toLowerCase())
      expect(
        marks.some((m) => m?.startsWith('kass')),
        item.textContent ?? '',
      ).toBe(true)
      expect(item.querySelector('.help-hit-area')?.textContent).toBeTruthy()
    }
  })

  it('nennt ohne Treffer Alternativen', async () => {
    const topics = await loadTopics()
    render(<HelpIndex topics={topics} />)
    fireEvent.change(screen.getByRole('searchbox', { name: 'Hilfe durchsuchen' }), { target: { value: 'qzxwvu' } })
    expect(screen.getByRole('status').textContent).toBe('Nichts gefunden. Anders formulieren oder die KI fragen.')
    expect(screen.getByRole('button', { name: 'Alle Themen anzeigen' })).toBeTruthy()
    expect(screen.getByText(/Bereiche:.*Kalender/)).toBeTruthy()
  })

  it('liefert den Bereich je Treffer', async () => {
    const topics = await loadTopics()
    const hit = searchHelp(topics, 'Termin verschieben')[0]
    expect(hit.area).toBe('Kalender')
  })

  it('hebt Wörter hervor, auch mit Umlauten und über den Wortstamm', () => {
    const parts = highlight('Räume und Kassa, kassieren', ['raeume', 'kasse'])
    expect(parts).toEqual([
      { text: 'Räume', mark: true },
      { text: ' und ', mark: false },
      { text: 'Kassa', mark: true },
      { text: ', ', mark: false },
      { text: 'kassieren', mark: true },
    ])
    expect(highlight('Nichts', ['kasse'])).toEqual([{ text: 'Nichts', mark: false }])
  })
})
