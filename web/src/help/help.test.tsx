import { act, cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { useState } from 'react'
import { afterEach, beforeAll, describe, expect, it } from 'vitest'
import { listUnanswered, recordUnanswered } from '../assistant/unanswered'
import { closeHelp, openHelp } from './help'
import { HelpButton } from './HelpButton'
import { HelpDrawer } from './HelpDrawer'
import { helpLink, parseMarkdown } from './markdown'
import { searchHelp } from './search'
import { imageUrl, loadTopics, sectionBlocks, toTopic } from './topics'

// Der erste Import aller Hilfetexte dauert unter Last länger als die 1 s von findBy…; danach liegen sie im Speicher.
beforeAll(() => loadTopics(), 60_000)

const contents = import.meta.glob<string>('./content/*.md', { query: '?raw', import: 'default', eager: true })
const sources = import.meta.glob<string>(['../**/*.tsx', '!../**/*.test.tsx'], { query: '?raw', import: 'default', eager: true })
const topics = Object.entries(contents).map(([path, text]) => toTopic(path.replace(/^.*\/|\.md$/g, ''), text))

describe('Hilfe', () => {
  afterEach(() => {
    act(() => closeHelp())
    cleanup()
  })

  it('liest Überschriften mit Anker, Absätze, Listen und Hervorhebungen', () => {
    const blocks = parseMarkdown('# Titel\n\n## Erster Teil {#erst}\nText mit **fett**, `Code` und [Link](https://x.test).\nweiter\n\n- eins\n- zwei\n  noch zwei\n\n1. Schritt')
    expect(blocks[1]).toEqual({ kind: 'heading', level: 2, text: 'Erster Teil', id: 'erst' })
    expect(blocks[2]).toEqual({
      kind: 'paragraph',
      spans: [
        { text: 'Text mit ' },
        { text: 'fett', bold: true },
        { text: ', ' },
        { text: 'Code', code: true },
        { text: ' und ' },
        { text: 'Link', href: 'https://x.test' },
        { text: '. weiter' },
      ],
    })
    expect(blocks[3]).toEqual({ kind: 'list', ordered: false, items: [[{ text: 'eins' }], [{ text: 'zwei noch zwei' }]] })
    expect(blocks[4]).toEqual({ kind: 'list', ordered: true, items: [[{ text: 'Schritt' }]] })
    expect(parseMarkdown('## Öffnungszeiten & Räume')[0]).toMatchObject({ id: 'oeffnungszeiten-raeume' })
  })

  it('liest Kopfangaben, Bilder und Verweise in die Hilfe', () => {
    const topic = toTopic(
      'kalender-termin-verschieben',
      '---\nart: ablauf\nbereich: Kalender\noberflaeche: windows-app\nrollen: rezeption, leitung\nseite: kalender\nstichwoerter: verlegen, umbuchen\n---\n' +
        '# Termin verschieben\n\nEinleitung.\n\n## Per Ziehen {#ziehen}\n\n1. Ziehen.\n\n![Beim Ziehen](images/app/kalender-ziehen.png)\n\nSiehe [Stornieren](hilfe:kalender-termin-stornieren#no-show).',
    )
    expect(topic.title).toBe('Termin verschieben')
    expect(topic.meta).toEqual({
      kind: 'ablauf',
      area: 'Kalender',
      surfaces: ['windows-app'],
      roles: ['rezeption', 'leitung'],
      page: 'kalender',
      keywords: ['verlegen', 'umbuchen'],
    })
    expect(topic.summary).toBe('Einleitung.')
    expect(topic.blocks).toContainEqual({ kind: 'image', name: 'app/kalender-ziehen', alt: 'Beim Ziehen' })
    expect(helpLink('hilfe:kalender-termin-stornieren#no-show')).toEqual({ topic: 'kalender-termin-stornieren', section: 'no-show' })
    expect(sectionBlocks(topic, 'ziehen')).toHaveLength(4)
  })

  it('jeder Verweis findet sein Thema und jedes Bild ist erzeugt', () => {
    for (const topic of topics) {
      for (const block of topic.blocks) {
        if (block.kind === 'image') expect(imageUrl(block.name), `${topic.id}: Bild images/${block.name}.png fehlt (scripts/help-images.sh bzw. web/scripts/help-images.mjs)`).toBeDefined()
        const spans = block.kind === 'paragraph' ? block.spans : block.kind === 'list' ? block.items.flat() : []
        for (const span of spans) {
          const link = helpLink(span.href)
          if (!link) continue
          const target = topics.find((t) => t.id === link.topic)
          expect(target, `${topic.id}: Verweis auf ${link.topic}`).toBeDefined()
          if (link.section)
            expect(target!.blocks.some((b) => b.kind === 'heading' && b.id === link.section), `${topic.id}: Verweis auf ${link.topic}#${link.section}`).toBe(true)
        }
      }
    }
  })

  it('jede Seite der Windows-App und der Web-Apps hat eine Erklärung', () => {
    for (const page of ['kalender', 'kunden', 'anfragen', 'kassa', 'geraete', 'abgleich', 'sicherung', 'im-netz', 'netz', 'verwaltung', 'anmeldung', 'myday'])
      expect(topics.some((t) => t.meta.kind === 'seite' && t.meta.page === page), `Seitenthema für ${page}`).toBe(true)
  })

  it('die Suche findet Abläufe vor Einstellungen', () => {
    const hits = searchHelp(topics, 'Wie verschiebe ich einen Termin?', { role: 'rezeption', surface: 'windows-app' })
    expect(hits[0].topic.meta.kind).toBe('ablauf')
    expect(hits[0].topic.title.toLowerCase()).toContain('verschieb')
    expect(searchHelp(topics, 'wie kann ich')).toEqual([])
  })

  it('jedes Thema hat einen Titel und eindeutige Anker', () => {
    expect(topics.length).toBeGreaterThan(0)
    for (const topic of topics) {
      expect(topic.title, topic.id).not.toBe(topic.id)
      const ids = topic.blocks.flatMap((b) => (b.kind === 'heading' ? [b.id] : []))
      expect(new Set(ids).size, `${topic.id}: doppelte Anker`).toBe(ids.length)
    }
  })

  it('jedes Fragezeichen in der Oberfläche zeigt auf ein vorhandenes Thema und einen vorhandenen Abschnitt', () => {
    const links = Object.entries(sources).flatMap(([file, text]) =>
      [...text.matchAll(/<HelpButton\s+topic="([^"]+)"(?:\s+section="([^"]+)")?/g)].map((m) => ({ file, topic: m[1], section: m[2] })),
    )
    // Firmendaten: die Gruppen nennen ihren Abschnitt in der Liste der Gruppen.
    const company = sources['../admin/CompanyTab.tsx'] ?? ''
    links.push(...[...company.matchAll(/help: '([^']+)'/g)].map((m) => ({ file: '../admin/CompanyTab.tsx', topic: 'firmendaten', section: m[1] })))
    expect(links.length).toBeGreaterThan(0)
    for (const link of links) {
      const topic = topics.find((t) => t.id === link.topic)
      expect(topic, `${link.file}: Thema ${link.topic}`).toBeDefined()
      if (link.section) {
        expect(
          topic!.blocks.some((b) => b.kind === 'heading' && b.id === link.section),
          `${link.file}: Abschnitt ${link.topic}#${link.section}`,
        ).toBe(true)
      }
    }
  })

  it('Given die Liste als Rezeption When sie aufgerufen wird Then ist sie nicht sichtbar', async () => {
    localStorage.clear()
    recordUnanswered('Wie färbe ich die Wolken lila?', 'calendar')
    render(<HelpDrawer />)

    act(() => openHelp(null))
    await screen.findByRole('searchbox', { name: 'Hilfe durchsuchen' })
    expect(screen.queryByRole('heading', { name: /Offene Fragen an die Hilfe/ })).toBeNull()
    expect(screen.queryByText('Wie färbe ich die Wolken lila?')).toBeNull()
  })

  it('Given die Liste als Leitung When sie aufgerufen wird Then ist sie sichtbar mit Erledigt je Eintrag', async () => {
    localStorage.clear()
    recordUnanswered('Wie färbe ich die Wolken lila?', 'calendar')
    recordUnanswered('Wo stelle ich den Duft ein?', 'pos')
    render(<HelpDrawer canReviewQuestions />)

    act(() => openHelp(null))
    const list = await screen.findByRole('list', { name: 'Offene Fragen an die Hilfe' })
    const items = within(list).getAllByRole('listitem')
    expect(items).toHaveLength(2)
    expect(items.every((item) => within(item).queryByRole('button', { name: /^Erledigt/ }))).toBe(true)

    fireEvent.click(within(list).getByRole('button', { name: 'Erledigt: Wo stelle ich den Duft ein?' }))
    expect(within(list).getAllByRole('listitem')).toHaveLength(1)
    expect(listUnanswered().map((q) => q.question)).toEqual(['Wie färbe ich die Wolken lila?'])
  })

  it('das Fragezeichen öffnet die Hilfe beim Abschnitt und die Übersicht zeigt alle Themen', async () => {
    render(
      <>
        <HelpButton topic="kassa" section="barzahlungsrabatt" label="Barzahlungsrabatt" />
        <HelpDrawer />
      </>,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Hilfe zu Barzahlungsrabatt' }))
    const target = await screen.findByRole('heading', { name: /Barzahlungsrabatt/ })
    expect(target.className).toBe('help-target')
    expect(screen.getByRole('complementary', { name: 'Hilfe' }).textContent).toContain('Umsatzsteuer')

    fireEvent.click(screen.getByRole('button', { name: 'Alle Themen' }))
    const overview = screen.getByRole('complementary', { name: 'Hilfe' })
    // Der Einstieg je Rolle („Erste Schritte“) wiederholt Abläufe und zählt hier nicht mit.
    const listed = [...overview.querySelectorAll('.help-topics button')].filter((b) => !b.closest('.help-entry'))
    expect(listed).toHaveLength(topics.length)
    expect(screen.getByRole('heading', { name: 'Abläufe: Kalender' })).toBeTruthy()

    fireEvent.change(screen.getByRole('searchbox', { name: 'Hilfe durchsuchen' }), { target: { value: 'Rechnung mit Empfänger' } })
    fireEvent.click(screen.getAllByRole('button', { name: /Rechnung mit Empfänger/ })[0])
    const image = await screen.findByRole('img', { name: 'Rechnung mit Empfänger und UID' })
    // Maße im Markup, damit der Text beim Laden des Bildes nicht springt (B22).
    expect(Number(image.getAttribute('width'))).toBeGreaterThan(0)
    expect(Number(image.getAttribute('height'))).toBeGreaterThan(0)
    fireEvent.click(screen.getByRole('button', { name: 'Hilfe schließen' }))
    expect(screen.queryByRole('complementary', { name: 'Hilfe' })).toBeNull()
  })

  it('Given die Hilfe ist offen und ein Dialog darüber When Esc gedrückt wird Then schließt nur der Dialog', async () => {
    function Confirm() {
      const [open, setOpen] = useState(true)
      return open ? (
        <div
          role="dialog"
          aria-label="Rückfrage"
          onKeyDown={(e) => {
            if (e.key !== 'Escape') return
            e.preventDefault()
            setOpen(false)
          }}
        >
          <button type="button">Abbrechen</button>
        </div>
      ) : null
    }
    render(
      <>
        <HelpButton topic="kassa" section="barzahlungsrabatt" label="Barzahlungsrabatt" />
        <HelpDrawer />
        <Confirm />
      </>,
    )
    fireEvent.click(screen.getByRole('button', { name: 'Hilfe zu Barzahlungsrabatt' }))
    await screen.findByRole('heading', { name: /Barzahlungsrabatt/ })

    const cancel = screen.getByRole('button', { name: 'Abbrechen' })
    cancel.focus()
    fireEvent.keyDown(cancel, { key: 'Escape' })
    expect(screen.queryByRole('dialog', { name: 'Rückfrage' })).toBeNull()
    expect(screen.getByRole('complementary', { name: 'Hilfe' })).toBeTruthy()

    // Fokus außerhalb der Leiste: Esc gehört der Seite, nicht der Hilfe.
    fireEvent.keyDown(document.body, { key: 'Escape' })
    expect(screen.getByRole('complementary', { name: 'Hilfe' })).toBeTruthy()

    const drawer = screen.getByRole('complementary', { name: 'Hilfe' })
    fireEvent.keyDown(within(drawer).getByRole('button', { name: 'Hilfe schließen' }), { key: 'Escape' })
    expect(screen.queryByRole('complementary', { name: 'Hilfe' })).toBeNull()
  })

  it('Given die Hilfe wurde über ein Fragezeichen geöffnet When sie geschlossen wird Then liegt der Fokus wieder auf diesem Fragezeichen', async () => {
    render(
      <>
        <HelpButton topic="kassa" label="Kassa" />
        <HelpButton topic="kassa" section="barzahlungsrabatt" label="Barzahlungsrabatt" />
        <HelpDrawer />
      </>,
    )
    const trigger = screen.getByRole('button', { name: 'Hilfe zu Barzahlungsrabatt' })
    trigger.focus()
    fireEvent.click(trigger)
    await screen.findByRole('heading', { name: /Barzahlungsrabatt/ })
    const drawer = screen.getByRole('complementary', { name: 'Hilfe' })
    expect(document.activeElement).toBe(within(drawer).getByRole('heading', { level: 2 }))

    fireEvent.click(within(drawer).getByRole('button', { name: 'Hilfe schließen' }))
    expect(screen.queryByRole('complementary', { name: 'Hilfe' })).toBeNull()
    expect(document.activeElement).toBe(trigger)

    trigger.focus()
    fireEvent.click(trigger)
    fireEvent.keyDown(document.activeElement!, { key: 'Escape' })
    expect(screen.queryByRole('complementary', { name: 'Hilfe' })).toBeNull()
    expect(document.activeElement).toBe(trigger)
  })
})
