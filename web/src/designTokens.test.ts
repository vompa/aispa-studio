/// <reference types="node" />
import { readdirSync, readFileSync } from 'node:fs'
import { basename, resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

// Design-Brief Abschnitt 2 (G2): Web und Windows-App müssen dieselben Farben tragen. Die Paare stehen in
// docs/design-tokens.json; der Gegenstück-Test für die App ist tests/AispaStudio.Desktop.Tests/DesignTokenTests.cs.

type Theme = 'hell' | 'dunkel'

const repoRoot = resolve(import.meta.dirname, '../..')
const read = (path: string) => readFileSync(resolve(repoRoot, path), 'utf-8')
const indexCss = read('web/src/index.css')
const colorsAxaml = read('src/AispaStudio.Desktop/Themes/Colors.axaml')
const tokenList = read('docs/design-tokens.json')

const tokens = (JSON.parse(tokenList) as { tokens: { web: string; app: string }[] }).tokens

function hexColors(text: string): string[] {
  return [...text.matchAll(/#[0-9a-fA-F]{6,8}\b/g)].map((m) => m[0].toLowerCase())
}

function webValues(theme: Theme, name: string): string[] {
  const block =
    theme === 'hell'
      ? /:root\s*\{([^}]*)\}/.exec(indexCss)?.[1]
      : /@media \(prefers-color-scheme: dark\)\s*\{\s*:root\s*\{([^}]*)\}/.exec(indexCss)?.[1]
  const value = new RegExp(`${name}:\\s*([^;]+);`).exec(block ?? '')?.[1]
  return value === undefined ? [] : hexColors(value)
}

function appValues(theme: Theme, name: string): string[] {
  const key = theme === 'hell' ? 'Light' : 'Dark'
  const start = colorsAxaml.indexOf(`x:Key="${key}"`)
  const end = theme === 'hell' ? colorsAxaml.indexOf('x:Key="Dark"') : colorsAxaml.length
  const dictionary = colorsAxaml.slice(start, end)
  const solid = new RegExp(`<SolidColorBrush x:Key="${name}" Color="(#[0-9a-fA-F]+)"`).exec(dictionary)
  if (solid) return [solid[1].toLowerCase()]
  const gradient = new RegExp(`<LinearGradientBrush x:Key="${name}"[^>]*>([\\s\\S]*?)</LinearGradientBrush>`).exec(dictionary)
  return gradient ? hexColors(gradient[1]) : []
}

// WCAG-Kontrast (relative Leuchtdichte nach WCAG 2.2). Gegenstück: DesignTokenTests.cs (Erledigt hell).
function luminance(hex: string): number {
  const [red, green, blue] = [1, 3, 5].map((i) => {
    const channel = parseInt(hex.slice(i, i + 2), 16) / 255
    return channel <= 0.04045 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4
  })
  return 0.2126 * red + 0.7152 * green + 0.0722 * blue
}

function contrast(first: string, second: string): number {
  const [lighter, darker] = [luminance(first), luminance(second)].sort((a, b) => b - a)
  return (lighter + 0.05) / (darker + 0.05)
}

describe('Design-Token: Kontrast der Statusfarben', () => {
  // Brief 2.6: Text auf seiner Fläche mindestens 4,5:1.
  it('Erledigt hell: --st-done auf --st-done-bg mindestens 4,5:1', () => {
    const [text] = webValues('hell', '--st-done')
    const [background] = webValues('hell', '--st-done-bg')
    expect(contrast(text, background)).toBeGreaterThanOrEqual(4.5)
  })
})

describe('Design-Token: Web (index.css) und Windows-App (Colors.axaml) stimmen überein', () => {
  for (const theme of ['hell', 'dunkel'] as const) {
    it.each(tokens)(`$web = $app (${theme})`, ({ web, app }) => {
      const webValue = webValues(theme, web)
      expect(webValue.length, `${web} fehlt in index.css`).toBeGreaterThan(0)
      expect(appValues(theme, app), `${web} (Web) gegen ${app} (App)`).toEqual(webValue)
    })
  }
})

// Design-Brief Abschnitt 3 (F23): Schriftstufen und Größen. Gegenstück: tests/AispaStudio.Desktop.Tests/DesignTokenTests.Type.cs.
const typeScale = JSON.parse(tokenList) as {
  schriftgroessen: number[]
  schriftstufen: { name: string; groesse: number; zeile: number; gewicht: number; web?: string }[]
}

describe('Design-Token: Schriftstufen in index.css', () => {
  const levels = typeScale.schriftstufen.filter((level) => level.web)
  it.each(levels)('$name ($web) hat Größe, Zeilenhöhe und Gewicht des Briefs', ({ web, groesse, zeile, gewicht }) => {
    const block = new RegExp(String.raw`^${web} \{([^}]*)\}`, 'm').exec(indexCss)?.[1] ?? ''
    expect(Number(/font-size:\s*([0-9.]+)px/.exec(block)?.[1])).toBe(groesse)
    expect(Number(/line-height:\s*([0-9.]+)px/.exec(block)?.[1])).toBe(zeile)
    expect(Number(/font-weight:\s*(\d+)/.exec(block)?.[1])).toBe(gewicht)
  })

  it('kennt nur Schriftgrößen der Stufen', () => {
    const sizes = [...indexCss.matchAll(/font-size:\s*([0-9.]+)px/g)].map((m) => Number(m[1]))
    expect([...new Set(sizes.filter((size) => !typeScale.schriftgroessen.includes(size)))]).toEqual([])
  })
})

// Slice 41 (B28, B69): Die Palette steht nur in index.css (Team) und tokens.css (schlanke Oberflächen).
describe('Design-Token: Palette nur in index.css und tokens.css', () => {
  const sourceRoot = resolve(import.meta.dirname)
  const paletteFiles = ['index.css', 'tokens.css']
  const colorDeclaration = /^\s*--[\w-]+\s*:\s*[^;]*(#[0-9a-fA-F]{3,8}\b|rgba?\(|linear-gradient\()/gm

  const otherCssFiles = readdirSync(sourceRoot, { recursive: true })
    .map(String)
    .filter((path) => path.endsWith('.css') && !paletteFiles.includes(basename(path)))

  it.each(otherCssFiles)('%s definiert keine Farben als eigene Custom Property', (path) => {
    const css = readFileSync(resolve(sourceRoot, path), 'utf-8')
    expect([...css.matchAll(colorDeclaration)].map((m) => m[0].trim())).toEqual([])
  })
})
