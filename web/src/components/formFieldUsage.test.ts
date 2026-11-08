import { readdirSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'

const srcDir = join(__dirname, '..')

/** Quelltexte der Oberfläche ohne Tests. */
function sourceFiles(): string[] {
  return (readdirSync(srcDir, { recursive: true }) as string[])
    .map((p) => p.replaceAll(String.fromCharCode(92), '/'))
    .filter((p) => /\.(ts|tsx)$/.test(p) && !/\.test\.tsx?$/.test(p))
}

describe('Formularbaustein', () => {
  it('Given die Formulare der Anmeldung und der Betreiber-Seite When ein Prüftest eigene Row/Group-Definitionen sucht Then gibt es keine mehr', () => {
    const own = /(function|const)\s+(Row|Group)\b/
    const formPages = sourceFiles().filter((p) => /^(signin|operator)\//.test(p))
    const hits = formPages.filter((p) => own.test(readFileSync(join(srcDir, p), 'utf8')))

    expect(hits).toEqual([])
  })

  it('Given die Quelltexte When ein Prüftest focusFirstInvalid mit setTimeout sucht Then gibt es keinen Treffer', () => {
    const hits = sourceFiles().filter((p) => /focusFirstInvalid\s*=\s*\(\)\s*=>\s*setTimeout/.test(readFileSync(join(srcDir, p), 'utf8')))

    expect(hits).toEqual([])
  })
})
