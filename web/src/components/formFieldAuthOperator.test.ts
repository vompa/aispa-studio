import { readdirSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'

/** Quelltexte der Seite „Anmelden“ und der Betreiber-Seite ohne Tests. */
function sources(): { name: string; text: string }[] {
  return ['signin', 'operator'].flatMap((dir) =>
    readdirSync(join(__dirname, '..', dir))
      .filter((f) => /\.tsx$/.test(f) && !/\.test\.tsx$/.test(f))
      .map((f) => ({ name: `${dir}/${f}`, text: readFileSync(join(__dirname, '..', dir, f), 'utf8') })),
  )
}

describe('Formularbaustein in Anmeldung und Betreiber', () => {
  it('Given die Formulare unter signin/ und operator/ When ein Prüftest required ohne noValidate am Formular sucht Then gibt es keinen Treffer', () => {
    const hits: string[] = []
    for (const { name, text } of sources()) {
      for (const form of text.matchAll(/<form\b((?:[^>]|=>)*)>([\s\S]*?)<\/form>/g)) {
        if (/\brequired\b/.test(form[2]) && !/\bnoValidate\b/.test(form[1])) hits.push(`${name}: ${form[1].slice(0, 60).replace(/\s+/g, ' ')}`)
      }
    }

    expect(hits).toEqual([])
  })

  it('Given die Formulare unter signin/ und operator/ When ein Prüftest <button ohne type innerhalb eines <form> sucht Then gibt es keinen Treffer', () => {
    const hits: string[] = []
    for (const { name, text } of sources()) {
      for (const form of text.matchAll(/<form\b[\s\S]*?<\/form>/g)) {
        for (const button of form[0].matchAll(/<button\b(?:[^>]|=>)*>/g)) {
          if (!/\btype=/.test(button[0])) hits.push(`${name}: ${button[0].slice(0, 60).replace(/\s+/g, ' ')}`)
        }
      }
    }

    expect(hits).toEqual([])
  })

  it('Given die Formulare unter signin/ und operator/ When ein Prüftest Eingabefelder in einem umhüllenden <label> sucht Then gibt es keinen Treffer', () => {
    const hits: string[] = []
    for (const { name, text } of sources()) {
      for (const label of text.matchAll(/<label\b(?:[^>]|=>)*>([\s\S]*?)<\/label>/g)) {
        if (/<(input|select|textarea)\b/.test(label[1])) hits.push(`${name}: ${label[0].slice(0, 60).replace(/\s+/g, ' ')}`)
      }
    }

    expect(hits).toEqual([])
  })
})
