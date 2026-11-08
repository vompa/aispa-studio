import { readdirSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'

/** Quelltexte von Anmeldung, „Mein Tag“ und Tafel ohne Tests (bis Slice 103: Kalender, Posteingang und Kassa der Web-Team-Oberfläche). */
function sources(): { name: string; text: string }[] {
  return ['signin', 'myday', 'dashboard'].flatMap((dir) =>
    readdirSync(join(__dirname, dir))
      .filter((f) => /\.tsx$/.test(f) && !/\.test\.tsx$/.test(f))
      .map((f) => ({ name: `${dir}/${f}`, text: readFileSync(join(__dirname, dir, f), 'utf8') })),
  )
}

const FIELD_IN_LABEL = /<(input|select|textarea)\b(?:[^>]|=>)*>/g
const BUTTON = /<button\b(?:[^>]|=>)*>/g

describe('Formularbaustein in Anmeldung, „Mein Tag“ und Tafel', () => {
  it('Given die Dateien unter signin/, myday/ und dashboard/ When ein Prüftest Eingabefelder in einem umhüllenden <label> ohne FormField sucht Then gibt es keinen Treffer', () => {
    const hits: string[] = []
    for (const { name, text } of sources()) {
      for (const label of text.matchAll(/<label\b(?:[^>]|=>)*>([\s\S]*?)<\/label>/g)) {
        const fields = [...label[1].matchAll(FIELD_IN_LABEL)].filter((f) => !/type=["'](checkbox|radio)["']/.test(f[0]))
        if (fields.length > 0) hits.push(`${name}: ${label[0].slice(0, 60).replace(/\s+/g, ' ')}`)
      }
    }

    expect(hits).toEqual([])
  })

  it('Given die Dateien unter signin/, myday/ und dashboard/ When ein Prüftest <button ohne type innerhalb eines <form> sucht Then gibt es keinen Treffer', () => {
    const hits: string[] = []
    for (const { name, text } of sources()) {
      for (const form of text.matchAll(/<form\b[\s\S]*?<\/form>/g)) {
        for (const button of form[0].matchAll(BUTTON)) {
          if (!/\btype=/.test(button[0])) hits.push(`${name}: ${button[0].slice(0, 60).replace(/\s+/g, ' ')}`)
        }
      }
    }

    expect(hits).toEqual([])
  })

  it('Given die Dateien unter signin/, myday/ und dashboard/ When ein Prüftest jedes <button ohne type sucht Then gibt es keinen Treffer (B87)', () => {
    const hits: string[] = []
    for (const { name, text } of sources()) {
      for (const button of text.matchAll(BUTTON)) {
        if (!/\btype=/.test(button[0])) hits.push(`${name}: ${button[0].slice(0, 60).replace(/\s+/g, ' ')}`)
      }
    }

    expect(hits).toEqual([])
  })
})
