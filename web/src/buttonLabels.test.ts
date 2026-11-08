import { readdirSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'

function sourceFiles(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const path = join(dir, entry.name)
    if (entry.isDirectory()) return sourceFiles(path)
    return /\.tsx$/.test(entry.name) && !/\.test\.tsx$/.test(entry.name) ? [path] : []
  })
}

/** Beschriftungen als Text zwischen Tags oder als Zeichenkette, die mit „+ “ und einem Wort beginnt, sowie „OK“ allein als Knopftext. */
const FORBIDDEN = [
  { rule: 'Beschriftung beginnt mit „+ “', pattern: /(^\s*|>\s*|['"`]\s*)\+ [A-ZÄÖÜa-zäöü]/ },
  { rule: '„OK“ als Knopftext', pattern: /(^\s*OK\s*$|>\s*OK\s*<|['"`]OK['"`])/ },
]

describe('Beschriftungen in den Quelltexten', () => {
  it('Given die Quelltexte When nach „+ “ am Anfang oder „OK“ als Knopftext gesucht wird Then gibt es keinen Treffer', () => {
    const hits: string[] = []
    for (const file of sourceFiles(join(__dirname))) {
      readFileSync(file, 'utf8')
        .split('\n')
        .forEach((line, index) => {
          for (const { rule, pattern } of FORBIDDEN) {
            if (pattern.test(line)) hits.push(`${file.slice(__dirname.length + 1)}:${index + 1} ${rule}: ${line.trim()}`)
          }
        })
    }
    expect(hits).toEqual([])
  })
})
