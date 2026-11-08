/// <reference types="node" />
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

// ADR 0084 (Runde R05, Slice 92): Die Prüfungen „jede Seite und jeder Reiter der Web-Verwaltung steht im Masken-Inventar“
// galten bis Slice 103; mit der Web-Team-Oberfläche sind sie entfallen. Die Sprungziele der Windows-App prüft dort ein
// Scan-Test gegen das Inventar. Hier bleibt die Prüfung des ADR-Index.

const repoRoot = resolve(import.meta.dirname, '../..')
const read = (path: string) => readFileSync(resolve(repoRoot, path), 'utf-8')

describe('Masken-Inventar (ADR 0084)', () => {
  it('Der ADR-Index führt 0084 und ADR 0053 ist durch 0084 ersetzt', () => {
    expect(read('docs/entscheidungen/README.md')).toMatch(/^\| 0084 \| \[/m)
    expect(read('docs/entscheidungen/0053-verwaltung-in-der-windows-app-eingebettet.md')).toMatch(
      /^- \*\*Status:\*\* ersetzt durch \[?0084/m,
    )
  })
})
