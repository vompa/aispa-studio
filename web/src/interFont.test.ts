/// <reference types="node" />
import { existsSync, readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

// Befund B62: Die Hilfe liefert die Hausschrift Inter selbst aus (über index.css), damit sie im WebView2 dieselbe Schrift
// zeigt wie die Windows-App (Design-Brief Abschnitt 3); bis Slice 103 tat das die Web-Team-Oberfläche. Kundenbereich und
// Anmeldung bleiben bei der Systemschrift. Die Prüfung des gebauten `dist` steht in scripts/check-bundles.mjs.

const webDir = resolve(import.meta.dirname, '..')
const read = (path: string) => readFileSync(resolve(webDir, path), 'utf-8')
const fontPath = 'src/assets/fonts/inter-latin-var.woff2'

function fontFaceBlocks(css: string): string[] {
  return [...css.replace(/\/\*[\s\S]*?\*\//g, '').matchAll(/@font-face\s*\{([^}]*)\}/g)].map((m) => m[1])
}

describe('Hausschrift Inter (Design-Brief 3, B62)', () => {
  it('Given das CSS der Hilfe (index.css) When es gelesen wird Then enthält es ein @font-face für Inter mit font-display swap und Gewicht 400 bis 600', () => {
    const inter = fontFaceBlocks(read('src/index.css')).filter((block) => /font-family:\s*Inter\s*;/.test(block))
    expect(inter).toHaveLength(1)
    expect(inter[0]).toMatch(/font-display:\s*swap\s*;/)
    expect(inter[0]).toMatch(/font-weight:\s*400 600\s*;/)
    expect(inter[0]).toMatch(/font-style:\s*normal\s*;/)
    expect(inter[0]).toContain("src: url('./assets/fonts/inter-latin-var.woff2') format('woff2')")
  })

  it('Given die Schriftdatei When sie gelesen wird Then ist sie eine WOFF2 und liegt mit Lizenz neben dem CSS', () => {
    expect(existsSync(resolve(webDir, fontPath))).toBe(true)
    expect(readFileSync(resolve(webDir, fontPath)).subarray(0, 4).toString('latin1')).toBe('wOF2')
    expect(read('src/assets/fonts/LICENSE.txt')).toContain('SIL Open Font License')
  })

  it('Given die Seite der Hilfe When ihr Kopf gelesen wird Then lädt sie die Inter-Datei mit preload und crossorigin vor', () => {
    expect(read('src/help/main.tsx')).toMatch(/import ['"]\.\.\/index\.css['"]/)
    expect(read('help/index.html')).toContain(
      `<link rel="preload" href="/${fontPath}" as="font" type="font/woff2" crossorigin />`,
    )
  })

  it('Given der Kundenbereich und die Anmeldung When Seite und CSS gelesen werden Then nennen sie weder Inter noch eine Schriftdatei', () => {
    for (const text of [read('buchen/index.html'), read('src/booking/booking.css'), read('anmelden/index.html'), read('src/signin/signIn.css')]) {
      expect(text).not.toMatch(/@font-face|\.woff2?\b|\.ttf\b|\bInter\b/)
    }
  })
})
