import { act, cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, beforeAll, describe, expect, it } from 'vitest'
import { HelpIndex } from './HelpIndex'
import { closeHelp } from './help'
import { roleEntries } from './roleEntries'
import { loadTopics, type HelpTopic } from './topics'

let topics: HelpTopic[] = []

// Der erste Import aller Hilfetexte dauert unter Last länger als die Standardfrist von findBy….
beforeAll(async () => {
  topics = await loadTopics()
}, 60_000)

afterEach(() => {
  act(() => closeHelp())
  cleanup()
})

describe('Einstieg je Rolle auf der Startseite der Hilfe', () => {
  it('Given die Startseite der Hilfe When sie angezeigt wird Then gibt es je Rolle einen Einstieg mit mindestens drei Abläufen', () => {
    render(<HelpIndex topics={topics} />)
    const entry = screen.getByRole('region', { name: 'Erste Schritte' })
    for (const role of ['Rezeption', 'Therapeut:in', 'Leitung', 'Inhaber:in']) {
      const group = within(entry).getByRole('group', { name: role })
      expect(within(group).getAllByRole('button').length).toBeGreaterThanOrEqual(3)
    }
  })

  it('Given die Rezeption When „Erste Schritte“ geöffnet wird Then führt der erste Ablauf zu „Termin buchen“', () => {
    render(<HelpIndex topics={topics} />)
    const group = within(screen.getByRole('region', { name: 'Erste Schritte' })).getByRole('group', { name: 'Rezeption' })
    expect(within(group).getAllByRole('button')[0].textContent).toBe('Termin buchen')
  })

  it('verweist nur auf Abläufe, die es gibt', () => {
    const ids = new Set(topics.filter((t) => t.meta.kind === 'ablauf').map((t) => t.id))
    for (const entry of roleEntries) for (const id of entry.topics) expect(ids.has(id), id).toBe(true)
  })

  it('Given eine bekannte Rolle im Kontext When die Startseite erscheint Then steht deren Einstieg zuerst', () => {
    render(<HelpIndex topics={topics} context={{ role: 'inhaber' }} />)
    const groups = within(screen.getByRole('region', { name: 'Erste Schritte' })).getAllByRole('group')
    expect(within(groups[0]).getByRole('heading', { name: 'Inhaber:in' })).toBeTruthy()
    expect(screen.getByRole('group', { name: 'Inhaber:in' })).toBe(groups[0])
  })

  it('zeigt den Einstieg nicht mehr, sobald gesucht wird', () => {
    render(<HelpIndex topics={topics} />)
    fireEvent.change(screen.getByRole('searchbox', { name: 'Hilfe durchsuchen' }), { target: { value: 'Termin' } })
    expect(screen.queryByRole('region', { name: 'Erste Schritte' })).toBeNull()
  })
})
