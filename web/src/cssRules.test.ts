import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

const css = (file: string) => readFileSync(resolve(import.meta.dirname, file), 'utf8')

/** Der Block `{ … }` der ersten Regel mit genau diesem Selektor. */
function ruleBody(source: string, selector: string): string {
  const start = source.indexOf(`\n${selector} {`)
  expect(start, `Regel ${selector} fehlt`).toBeGreaterThanOrEqual(0)
  return source.slice(start, source.indexOf('}', start))
}

describe('Regeln im CSS (Befunde B20, B32, B63)', () => {
  it('Given index.css When ein Prüftest `font-weight: 700` sucht Then gibt es keinen Treffer', () => {
    expect(css('index.css')).not.toMatch(/font-weight:\s*700/)
  })

  it('Given das Fragezeichen When gemessen Then ist die Klickfläche mindestens 24 × 24 px', () => {
    const body = ruleBody(css('index.css'), '.help-button')
    expect(Number(/(?:^|\s)width:\s*(\d+)px/.exec(body)?.[1])).toBeGreaterThanOrEqual(24)
    expect(Number(/(?:^|\s)height:\s*(\d+)px/.exec(body)?.[1])).toBeGreaterThanOrEqual(24)
  })

  it('Given der Kundenbereich im Kontrastdesign When die Zusammenfassung nur durch den Hintergrund auffällt Then hat sie einen Rand', () => {
    // Im Kontrastdesign (forced-colors) fällt der Hintergrund weg; ein durchsichtiger Rand wird dort sichtbar.
    expect(ruleBody(css('booking/booking.css'), '.summary')).toMatch(/border:\s*1px solid transparent/)
  })

  it('Given der Kundenbereich When eine Regel nur den Hintergrund --soft setzt Then setzt sie auch einen Rand', () => {
    const rules = [...css('booking/booking.css').matchAll(/([^{}]+)\{([^{}]*)\}/g)]
    const soft = rules.filter(([, , body]) => /background:\s*var\(--soft\)/.test(body))
    expect(soft.length).toBeGreaterThan(0)
    for (const [, selector, body] of soft) expect(body, selector.trim()).toMatch(/border(?:-\w+)*:\s*[^;]*\d+px/)
  })
})
