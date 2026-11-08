/// <reference types="node" />
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

// Ruling 14 aus Runde R01: Jede Slice kennzeichnet in der Befundliste, welche Befunde sie erledigt hat. Die
// Zuordnung steht in der Slice-Tabelle der Rundenakte; die Befundliste trägt den Zusatz „erledigt in Slice NN“
// in der Spalte „Einstufung“. Der Test gilt nur für Runde R01; die Slices späterer Runden sind noch offen.

const repoRoot = resolve(import.meta.dirname, '../..')
const read = (path: string) => readFileSync(resolve(repoRoot, path), 'utf-8')
const findingsList = read('docs/umsetzung/aenderungsliste-oberflaechen-2026-10-23.md')

/**
 * Liest die Slice-Tabelle der Rundenakte. Nach einem Eintrag „W-Muster 2“ gehören nackte Zahlen bis zur
 * nächsten Kennung zum selben Präfix („W-Muster 2, 3“ meint W-Muster 2 und W-Muster 3).
 */
function slicesByFinding(roundRecord: string): Map<string, string[]> {
  const result = new Map<string, string[]>()
  const section = roundRecord.split(/^## /m).find((part) => part.startsWith('Slices\n')) ?? ''
  for (const line of section.split('\n')) {
    const cells = line.split('|').map((cell) => cell.trim())
    const slice = cells[1]
    if (!/^\d{2}$/.test(slice ?? '')) continue
    let prefix = ''
    for (const token of (cells[3] ?? '').split(',').map((t) => t.trim())) {
      const named = /^(W-Muster|W|B)\s?(\d+)$/.exec(token)
      if (named) prefix = named[1] === 'W-Muster' ? 'W-Muster ' : named[1]
      const id = named ? `${prefix}${named[2]}` : /^\d+$/.test(token) ? `${prefix}${token}` : undefined
      if (!id) throw new Error(`Unbekannte Kennung „${token}“ in Slice ${slice}`)
      result.set(id, [...(result.get(id) ?? []), slice])
    }
  }
  return result
}

function findingRow(list: string, id: string): string | undefined {
  return list.split('\n').find((line) => line.replace(/\s+/g, ' ').startsWith(`| ${id} |`))
}

describe('Befundliste der Oberflächen-Durchsicht', () => {
  it('Given die Befunde der Slices 01–29 aus der Rundenakte R01 When die Befundliste gelesen wird Then trägt jede Befundzeile „erledigt in Slice NN“ mit allen zuständigen Nummern', () => {
    const expected = slicesByFinding(read('work/rounds/R01-ui-review.md'))
    expect(expected.size).toBe(78)
    const missing: string[] = []
    for (const [id, slices] of expected) {
      const row = findingRow(findingsList, id)
      expect(row, `Zeile für ${id}`).toBeDefined()
      if (!row?.includes(`erledigt in Slice ${slices.join(', ')}`)) missing.push(`${id} → ${slices.join(', ')}`)
    }
    expect(missing).toEqual([])
  })

  it('Given work/conventions.md When der Punkt „Muss mitgeändert werden“ gelesen wird Then nennt er die Befundliste', () => {
    const rule = read('work/conventions.md')
      .split('\n')
      .find((line) => line.startsWith('- Muss mitgeändert werden'))
    expect(rule).toContain('aenderungsliste-oberflaechen-2026-10-23.md')
  })
})
