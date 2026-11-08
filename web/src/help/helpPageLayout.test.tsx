import { act, cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest'
import { HelpApp } from './HelpApp'
import { closeHelp } from './help'
import { loadTopics, sectionsOf } from './topics'

// Slice 145: ruhiges Layout der Hilfe-Seite mit Bereichen, Rollenfilter und Inhaltsverzeichnis neben dem Text.
let topics: Awaited<ReturnType<typeof loadTopics>>
beforeAll(async () => {
  topics = await loadTopics()
}, 60_000)

function setViewportWide(wide: boolean) {
  vi.stubGlobal('matchMedia', (query: string) => ({
    matches: wide,
    media: query,
    addEventListener: () => undefined,
    removeEventListener: () => undefined,
    addListener: () => undefined,
    removeListener: () => undefined,
    onchange: null,
    dispatchEvent: () => false,
  }))
}

function openPage(search: string) {
  window.history.replaceState(null, '', `/help/${search}`)
  return render(<HelpApp />)
}

afterEach(() => {
  act(() => closeHelp())
  cleanup()
  vi.unstubAllGlobals()
})

describe('Hilfe-Seite: Navigation nach Bereichen', () => {
  it('Given „Status setzen“ ist ein Einstieg der Rezeption When man die Navigation öffnet Then steht es genau einmal, unter „Erste Schritte“ statt unter „Kalender“', async () => {
    setViewportWide(true)
    openPage('?rolle=rezeption')
    const nav = await screen.findByRole('navigation', { name: 'Themen' })
    await screen.findByRole('button', { name: /Meine Rolle: Rezeption/ })

    const entries = nav.querySelectorAll('[data-topic="kalender-status-setzen"]')
    expect(entries).toHaveLength(1)
    expect(entries[0].closest('details')?.querySelector('summary')?.textContent).toBe('Erste Schritte')
  })

  // T-028 (Ä380): „Erste Schritte“ ist eine eigene Gruppe der Navigation, mit Rolle nur die Einstiege dieser Rolle.
  it('Given als Rezeption angemeldet When man die Navigation öffnet Then ist „Erste Schritte“ die erste Gruppe und hält die Einstiege der Rezeption in Lernreihenfolge', async () => {
    setViewportWide(true)
    openPage('?rolle=rezeption')
    const nav = await screen.findByRole('navigation', { name: 'Themen' })
    await screen.findByRole('button', { name: /Meine Rolle: Rezeption/ })

    const first = nav.querySelector('details.help-group')!
    expect(first.querySelector('summary')?.textContent).toBe('Erste Schritte')
    const ids = [...first.querySelectorAll('[data-topic]')].map((e) => e.getAttribute('data-topic'))
    expect(ids).toEqual([
      'kalender-termin-buchen',
      'kalender-freie-zeit-suchen',
      'kalender-status-setzen',
      'kassa-termin-kassieren',
      'kassa-tagesabschluss',
    ])
  })

  it('Given der Chip steht auf „Alle“ When man die Navigation ansieht Then hält „Erste Schritte“ die Einstiege aller Rollen je einmal', async () => {
    setViewportWide(true)
    openPage('?rolle=rezeption')
    const nav = await screen.findByRole('navigation', { name: 'Themen' })
    fireEvent.click(await screen.findByRole('button', { name: /Meine Rolle: Rezeption/ }))

    const first = nav.querySelector('details.help-group')!
    expect(first.querySelector('summary')?.textContent).toBe('Erste Schritte')
    const ids = [...first.querySelectorAll('[data-topic]')].map((e) => e.getAttribute('data-topic'))
    expect(ids).toContain('kassa-gutschein-verkaufen')
    expect(ids).toContain('hygiene-tagesstart-abhaken')
    expect(new Set(ids).size).toBe(ids.length)
  })

  it('Given als Rezeption angemeldet When man die Hilfe öffnet Then filtert der Chip „Meine Rolle: Rezeption“ und lässt sich auf „Alle“ stellen', async () => {
    setViewportWide(true)
    openPage('?rolle=rezeption')
    const nav = await screen.findByRole('navigation', { name: 'Themen' })
    const chip = await screen.findByRole('button', { name: 'Meine Rolle: Rezeption' })
    expect(chip.getAttribute('aria-pressed')).toBe('true')

    const rezeption = topics.filter((t) => t.meta.roles.includes('rezeption')).length
    expect(rezeption).toBeGreaterThan(0)
    expect(rezeption).toBeLessThan(topics.length)
    expect(nav.querySelectorAll('[data-topic]').length).toBe(rezeption)

    fireEvent.click(chip)
    expect(chip.getAttribute('aria-pressed')).toBe('false')
    expect(chip.textContent).toBe('Alle')
    expect(nav.querySelectorAll('[data-topic]').length).toBe(topics.length)
  })

  it('zeigt ohne Rolle in der Adresse keinen Rollenchip und jedes Thema genau einmal', async () => {
    setViewportWide(true)
    openPage('')
    const nav = await screen.findByRole('navigation', { name: 'Themen' })
    await screen.findByRole('searchbox', { name: 'Hilfe durchsuchen' })
    expect(screen.queryByRole('button', { name: /Meine Rolle/ })).toBeNull()
    const ids = [...nav.querySelectorAll('[data-topic]')].map((e) => e.getAttribute('data-topic'))
    expect(ids).toHaveLength(topics.length)
    expect(new Set(ids).size).toBe(ids.length)
  })

  it('klappt die Gruppe des offenen Themas auf', async () => {
    setViewportWide(true)
    openPage('?thema=kassa')
    const nav = await screen.findByRole('navigation', { name: 'Themen' })
    await screen.findByRole('heading', { level: 1, name: 'Kassa' })
    const current = nav.querySelector('[aria-current="page"]')
    expect(current?.closest('details')?.open).toBe(true)
  })
})

describe('Hilfe-Seite: Inhaltsverzeichnis', () => {
  const manySections = () => topics.find((t) => sectionsOf(t).length >= 10)

  it('Given ein Thema mit vielen Abschnitten bei breitem Fenster When man es öffnet Then steht das Inhaltsverzeichnis rechts neben dem Text', async () => {
    setViewportWide(true)
    const topic = manySections()
    expect(topic).toBeTruthy()
    openPage(`?thema=${topic!.id}`)
    await screen.findByRole('heading', { level: 1, name: topic!.title })

    const toc = screen.getByRole('navigation', { name: 'Auf dieser Seite' })
    expect(toc.closest('aside.help-toc-side')).not.toBeNull()
    expect(toc.closest('details')).toBeNull()
    expect(within(toc).getAllByRole('link')).toHaveLength(sectionsOf(topic!).length)
    const main = screen.getByRole('main')
    expect(main.querySelector('article')?.contains(toc)).toBe(false)
  })

  it('Given dasselbe Thema bei schmalem Fenster (offenes Panel) When man es öffnet Then ist das Verzeichnis ein zugeklapptes „Inhalt“ über dem Text', async () => {
    setViewportWide(false)
    const topic = manySections()
    openPage(`?thema=${topic!.id}`)
    await screen.findByRole('heading', { level: 1, name: topic!.title })

    const fold = document.querySelector('details.help-toc-fold') as HTMLDetailsElement
    expect(fold).not.toBeNull()
    expect(fold.open).toBe(false)
    expect(fold.querySelector('summary')?.textContent).toBe('Inhalt')
    const article = screen.getByRole('main').querySelector('article')!
    expect(article.contains(fold)).toBe(true)
    expect(document.querySelector('aside.help-toc-side')).toBeNull()
    const h1 = within(article).getByRole('heading', { level: 1 })
    expect(h1.compareDocumentPosition(fold) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
  })

  it('zeigt kein Verzeichnis bei einem Thema ohne Zwischenüberschrift', async () => {
    setViewportWide(true)
    const topic = topics.find((t) => sectionsOf(t).length === 0)
    expect(topic).toBeTruthy()
    openPage(`?thema=${topic!.id}`)
    await screen.findByRole('heading', { level: 1, name: topic!.title })
    expect(screen.queryByRole('navigation', { name: 'Auf dieser Seite' })).toBeNull()
  })
})
