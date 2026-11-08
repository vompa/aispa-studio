import { readdirSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'

const srcDir = join(__dirname, '..')

/** Quelltexte der Oberfläche ohne Tests, den Testersatz und den Baustein mit `showModal()`: die Rückfrage. */
function sourceFiles(): string[] {
  return (readdirSync(srcDir, { recursive: true }) as string[])
    .map((p) => p.replaceAll('\\', '/'))
    .filter((p) => /\.(ts|tsx)$/.test(p) && !/\.test\.tsx?$/.test(p) && p !== 'vitest.setup.ts' && p !== 'components/ConfirmDialog.tsx')
}

describe('Rückfragen', () => {
  it('Given die Quelltexte When ein Prüftest showModal außerhalb von ConfirmDialog sucht Then gibt es keinen Treffer', () => {
    const hits = sourceFiles().filter((p) => /\bshowModal\b/.test(readFileSync(join(srcDir, p), 'utf8')))

    expect(hits).toEqual([])
  })
})
