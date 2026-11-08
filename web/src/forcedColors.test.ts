/// <reference types="node" />
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

// Befunde B82, B85: gewählte und gedrückte Zustände im Kontrastdesign von Windows.
// Die Prüfung liest die CSS-Quelltexte, weil jsdom weder Medienabfragen noch Farben auswertet.

const srcDir = resolve(import.meta.dirname)
const FORCED = '@media (forced-colors: active)'

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

// Je Stylesheet die Zustandsselektoren, die im Kontrastdesign eine eigene Regel brauchen.
const states: Record<string, string[]> = {
  'index.css': [
    "button[aria-pressed='true']",
    '.topbar button.selected',
    '.segmented button.selected',
    '.chip.selected',
    '.admin-list button.selected',
    '.choice .option.selected',
    '.mail.selected',
    '.appointment.selected',
    '.appointment.status-CheckedIn',
    '.appointment.status-Completed',
    '.appointment.status-NoShow',
  ],
  'booking/booking.css': ['.choice:has(input:checked)', '.day:has(input:checked)', '.time:has(input:checked)'],
  'myday/myday.css': ['.check:has(input:checked) > span', ".check:has(input[aria-disabled='true'])"],
  'operator/operator.css': ["input[aria-invalid='true']", "select[aria-invalid='true']", 'button:disabled'],
  'signin/signIn.css': ["input[aria-invalid='true']", "select[aria-invalid='true']", 'button:disabled'],
}

describe('Zustände im Kontrastdesign von Windows', () => {
  for (const [file, selectors] of Object.entries(states)) {
    it(`Given ${file} When ein Prüftest forced-colors: active sucht Then hat die Datei eine Regel für ihre Zustandsselektoren mit Systemfarben`, () => {
      const forced = cssRules(readFileSync(resolve(srcDir, file), 'utf-8')).filter((r) => r.media.includes(FORCED))
      const covered = new Set(forced.flatMap((r) => r.selector.split(',').map((s) => s.trim())))
      expect(selectors.filter((s) => !covered.has(s))).toEqual([])
      for (const rule of forced) {
        expect(rule.body, `${file}: ${rule.selector}`).not.toMatch(/#[0-9a-f]{3,8}\b|var\(|rgba?\(/i)
      }
    })
  }
})
