import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

// Die Seite „Über AISPA Studio“ (/ueber/) ist statisches HTML; geprüft wird die Datei, wie Vite sie ausliefert.
const html = readFileSync(resolve(import.meta.dirname, '../../ueber/index.html'), 'utf8')
const page = new DOMParser().parseFromString(html, 'text/html')
const helpIndex = readFileSync(resolve(import.meta.dirname, '../help/HelpIndex.tsx'), 'utf8')

describe('Seite „Über AISPA Studio“', () => {
  it('Given die Seite When sie geladen wird Then hat sie genau einen Titel h1 und Deutsch als Sprache', () => {
    expect(page.documentElement.lang).toBe('de')
    expect(page.querySelectorAll('h1')).toHaveLength(1)
  })

  it('Given jeder Sprunglink When er auf einen Anker zeigt Then gibt es das Ziel', () => {
    const anchors = [...page.querySelectorAll<HTMLAnchorElement>('a[href^="#"]')].map((a) => a.getAttribute('href')!.slice(1))
    expect(anchors).toContain('inhalt')
    for (const id of anchors) expect(page.getElementById(id), `#${id}`).not.toBeNull()
  })

  it('Given jedes Bild When es angezeigt wird Then hat es einen Alternativtext und feste Maße', () => {
    for (const img of page.querySelectorAll('img')) {
      expect(img.getAttribute('alt')?.length, img.getAttribute('src') ?? '').toBeGreaterThan(20)
      expect(img.getAttribute('width')).toBeTruthy()
      expect(img.getAttribute('height')).toBeTruthy()
    }
  })

  it('Given die Betriebsarten When sie gezeigt werden Then hat jedes Schaubild eine Beschreibung und die drei Zonen', () => {
    const figures = page.querySelectorAll('figure')
    expect(figures).toHaveLength(3)
    for (const figure of figures) expect(page.getElementById(figure.getAttribute('aria-labelledby') ?? '')).not.toBeNull()
    const multi = page.querySelector('[aria-labelledby="fig-multi"]')!
    expect(multi.querySelectorAll('.zone-spa, .zone-cloud, .zone-external')).toHaveLength(3)
  })

  it('Given der Ausblick auf die Browser-Fassung When er gezeigt wird Then steht dort, dass sie noch nicht verfügbar ist', () => {
    expect(page.getElementById('ausblick')?.textContent).toContain('Versuch, noch nicht verfügbar')
  })

  it('Given der Seitentext When er gelesen wird Then redet er niemanden mit „du“ an (Textregel T1)', () => {
    const text = page.body.textContent ?? ''
    expect(text).not.toMatch(/\b(du|dich|dir|dein\w*)\b/i)
  })

  it('Given Kopf und Abschnitt Entwicklung When sie gelesen werden Then nennen sie AISPA Studio eine Konzeptstudie zur KI-nativen Softwareentwicklung', () => {
    expect(page.querySelector('.about-eyebrow')?.textContent).toContain('Konzeptstudie zur KI-nativen Softwareentwicklung')
    expect(page.getElementById('entwicklung')?.textContent).toContain('Konzeptstudie zur KI-nativen Softwareentwicklung')
    expect(helpIndex).toContain('Konzeptstudie zur KI-nativen Softwareentwicklung')
  })

  it('Given die Hilfe-Seite When die Themen stehen Then verlinkt sie die Seite mit index.html (Einzelplatz)', () => {
    expect(helpIndex).toContain('href="/ueber/index.html"')
  })
})
