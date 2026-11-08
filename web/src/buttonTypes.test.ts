import { describe, expect, it } from 'vitest'

const sources = import.meta.glob(['./signin/*.tsx', './operator/*.tsx', './myday/*.tsx', './dashboard/*.tsx', './components/*.tsx', '!./**/*.test.tsx'], {
  query: '?raw',
  import: 'default',
  eager: true,
}) as Record<string, string>

/** Gibt die Zeilen aller `<button`-Tags zurück, die kein `type` tragen (Klammern in Ausdrücken werden übersprungen). */
function buttonsWithoutType(source: string): number[] {
  const lines: number[] = []
  for (const match of source.matchAll(/<button\b/g)) {
    let i = match.index + match[0].length
    let depth = 0
    let quote: string | null = null
    for (; i < source.length; i++) {
      const c = source[i]
      if (quote) {
        if (c === quote) quote = null
      } else if (depth === 0 && (c === '"' || c === "'")) quote = c
      else if (c === '{') depth++
      else if (c === '}') depth--
      else if (c === '>' && depth === 0 && source[i - 1] !== '=') break
    }
    const head = source.slice(match.index + match[0].length, i)
    if (!/(^|\s)type=/.test(head)) lines.push(source.slice(0, match.index).split('\n').length)
  }
  return lines
}

describe('Knöpfe der Anmeldung, der Betreiber-Seite, von „Mein Tag“, der Tafel und der Bausteine', () => {
  it('Given jede Seite der Gruppe When die Knöpfe gelesen werden Then trägt jeder ein type', () => {
    expect(Object.keys(sources).length).toBeGreaterThan(10)
    const missing = Object.entries(sources).flatMap(([file, source]) => buttonsWithoutType(source).map((line) => `${file}:${line}`))
    expect(missing).toEqual([])
  })
})
