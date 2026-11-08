/// <reference types="node" />
import { readdirSync, readFileSync } from 'node:fs'
import { join, relative, resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

// Befund B4: jeder Tabellenkopf nennt dem Screenreader seine Richtung (scope="col" oder "row").
// Die Prüfung liest den Quelltext, damit auch bedingt gerenderte Köpfe ohne Testdaten auffallen.

const srcDir = resolve(import.meta.dirname)

function tsxFiles(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const path = join(dir, entry.name)
    if (entry.isDirectory()) return tsxFiles(path)
    return entry.name.endsWith('.tsx') ? [path] : []
  })
}

function headersWithoutScope(source: string): string[] {
  return [...source.matchAll(/<th(?=[\s>/])[^>]*>/g)].map((m) => m[0]).filter((tag) => !tag.includes('scope='))
}

describe('Tabellenköpfe', () => {
  it('jedes <th> in web/src nennt seine Richtung mit scope', () => {
    const missing = tsxFiles(srcDir).flatMap((file) =>
      headersWithoutScope(readFileSync(file, 'utf-8')).map((tag) => `${relative(srcDir, file)}: ${tag}`),
    )
    expect(missing).toEqual([])
  })

  it('erkennt Köpfe ohne scope und lässt thead und Köpfe mit scope durch', () => {
    expect(
      headersWithoutScope('<thead><tr><th>A</th><th className="amount" scope="col">B</th><th\n key={c}>C</th></tr></thead>'),
    ).toEqual(['<th>', '<th\n key={c}>'])
  })
})
