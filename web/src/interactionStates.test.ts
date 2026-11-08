/// <reference types="node" />
import { readdirSync, readFileSync } from 'node:fs'
import { join, relative, resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

// Befunde B5, B6, B64: reduzierte Bewegung, Fokus im Kontrastdesign von Windows und Druckzustand der Knöpfe.
// Die Prüfung liest die CSS-Quelltexte, weil jsdom weder Medienabfragen noch :active auswertet.

const srcDir = resolve(import.meta.dirname)
const read = (path: string) => readFileSync(resolve(srcDir, path), 'utf-8')

type CssRule = { selector: string; body: string; media: string[] }

function cssRules(css: string): CssRule[] {
  const text = css.replace(/\/\*[\s\S]*?\*\//g, '')
  const rules: CssRule[] = []
  const stack: string[] = []
  let start = 0
  for (let i = 0; i < text.length; i++) {
    if (text[i] === '{') {
      const prelude = text.slice(start, i).trim()
      const end = text.indexOf('}', i)
      const inner = text.slice(i + 1, end)
      if (prelude.startsWith('@') || inner.includes('{')) {
        stack.push(prelude)
        start = i + 1
      } else {
        rules.push({ selector: prelude, body: inner, media: [...stack] })
        i = end
        start = end + 1
      }
    } else if (text[i] === '}') {
      stack.pop()
      start = i + 1
    }
  }
  return rules
}

function isButtonHover(selector: string): boolean {
  return selector
    .split(',')
    .map((part) => part.replace(/:not\([^)]*\)/g, ''))
    .some((part) => part.includes(':hover') && /(^|[\s>])(button|summary)\b|\.(button|help-button|companion-fab)\b/.test(part))
}

function cssFiles(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const path = join(dir, entry.name)
    if (entry.isDirectory()) return cssFiles(path)
    return entry.name.endsWith('.css') ? [path] : []
  })
}

const indexRules = cssRules(read('index.css'))

describe('Bewegung, Hochkontrast und Druckzustand', () => {
  it('liest Regeln samt @media und erkennt Knopf-Hover', () => {
    const rules = cssRules('/* a { } */ a{color:red}\n@media (hover: hover) { button:hover { x: 1 } }')
    expect(rules).toEqual([
      { selector: 'a', body: 'color:red', media: [] },
      { selector: 'button:hover', body: ' x: 1 ', media: ['@media (hover: hover)'] },
    ])
    expect(isButtonHover('.topbar .account summary:hover')).toBe(true)
    expect(isButtonHover('a:not(.button):hover')).toBe(false)
    expect(isButtonHover('input:hover:not(:disabled)')).toBe(false)
  })

  it('Given index.css When eine Prüfung die Datei liest Then schaltet ein Block prefers-reduced-motion Übergänge und Animationen für alle Elemente ab', () => {
    const block = indexRules.find(
      (r) =>
        r.media.includes('@media (prefers-reduced-motion: reduce)') &&
        r.selector
          .split(',')
          .map((s) => s.trim())
          .includes('*'),
    )
    expect(block?.body).toMatch(/transition-duration:\s*0s\s*!important/)
    expect(block?.body).toMatch(/animation:\s*none\s*!important/)
  })

  it('Given die Stylesheets unter src When eine Prüfung jede Regel mit outline: none sucht Then steht dort ein im Kontrastdesign sichtbarer Umriss', () => {
    // Bis Slice 103 nur index.css, stock.css und businessInfo.css; jetzt alle Stylesheets außer dem Kundenbereich. Dort nimmt
    // `.heading:focus, main:focus` den Umriss nur bei Fokus per Skript weg (Befund, in Slice 103 nicht geändert).
    const files = cssFiles(srcDir)
      .map((file) => relative(srcDir, file).replaceAll(String.fromCharCode(92), '/'))
      .filter((file) => file !== 'booking/booking.css')
    expect(files).toEqual(expect.arrayContaining(['index.css', 'signin/signIn.css', 'operator/operator.css', 'myday/myday.css']))
    const hidden = files.flatMap((file) =>
      cssRules(read(file))
        .filter((r) => /outline:\s*none/.test(r.body) && !/outline:\s*2px solid transparent/.test(r.body))
        .map((r) => `${file}: ${r.selector}`),
    )
    expect(hidden).toEqual([])
    const appointment = indexRules.find((r) => r.selector === '.appointment' && r.media.length === 0)
    expect(appointment?.body).toMatch(/border:\s*1px solid transparent/)
    expect(appointment?.body).not.toMatch(/border:\s*none/)
  })

  it('Given index.css When eine Prüfung nach :active sucht Then skalieren Knöpfe beim Drücken auf 0,97 und Knopf-Hover steht nur in @media (hover: hover)', () => {
    const pressed = indexRules.filter((r) => /^button\b.*:active:not\(:disabled\)/s.test(r.selector))
    expect(pressed.some((r) => /transform:\s*scale\(0\.97\)/.test(r.body))).toBe(true)
    const outside = cssFiles(srcDir).flatMap((file) =>
      cssRules(readFileSync(file, 'utf-8'))
        .filter((r) => isButtonHover(r.selector) && !r.media.includes('@media (hover: hover)'))
        .map((r) => `${relative(srcDir, file)}: ${r.selector}`),
    )
    expect(outside).toEqual([])
    // Zustände ohne Hover (geöffnet, gewählt) dürfen nicht mit in die Abfrage, sonst fehlen sie auf Touch-Geräten.
    const hidden = cssFiles(srcDir).flatMap((file) =>
      cssRules(readFileSync(file, 'utf-8'))
        .filter((r) => r.media.includes('@media (hover: hover)'))
        .flatMap((r) => r.selector.split(',').map((part) => part.trim()))
        .filter((part) => !part.includes(':hover'))
        .map((part) => `${relative(srcDir, file)}: ${part}`),
    )
    expect(hidden).toEqual([])
  })
})
