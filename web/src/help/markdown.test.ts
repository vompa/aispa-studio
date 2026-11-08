import { describe, expect, it } from 'vitest'
import { parseMarkdown } from './markdown'

describe('Hinweiskästen im Leser', () => {
  it('liest „> Hinweis:“, „> Tipp:“ und „> Achtung:“ als Kästen und führt Fortsetzungszeilen zusammen', () => {
    const blocks = parseMarkdown('Text.\n\n> Hinweis: Eins\n> zwei\n\n> Tipp: Drei\n\n> Achtung: Vier **fett**\n')
    expect(blocks.map((b) => b.kind)).toEqual(['paragraph', 'callout', 'callout', 'callout'])
    const callouts = blocks.flatMap((b) => (b.kind === 'callout' ? [b] : []))
    expect(callouts.map((b) => b.type)).toEqual(['hint', 'tip', 'warning'])
    expect(callouts[0].spans.map((s) => s.text).join('')).toBe('Eins zwei')
    expect(callouts[2].spans.some((s) => s.bold)).toBe(true)
  })

  it('lässt ein Zitat ohne Stichwort ein Absatz bleiben', () => {
    expect(parseMarkdown('> Irgendein Zitat\n').map((b) => b.kind)).toEqual(['paragraph'])
  })
})
