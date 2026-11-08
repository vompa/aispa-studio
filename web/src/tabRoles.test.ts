/// <reference types="node" />
import { readdirSync, readFileSync } from 'node:fs'
import { join, relative, resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

// Befund B3: Reiterschienen gab es nur in der Web-Team-Oberfläche, mit der gemeinsamen Komponente `Tabs` (Pfeiltasten, Pos1/Ende,
// Roving-Tabindex, Tabpanel); beide entfielen in Slice 103. Die Prüfung liest den Quelltext, damit keine Schiene im Eigenbau
// entsteht; braucht eine Seite wieder Reiter, kommt zuerst eine gemeinsame Komponente nach dem ARIA-Muster und hierher.

const srcDir = resolve(import.meta.dirname)

function sourceFiles(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const path = join(dir, entry.name)
    if (entry.isDirectory()) return sourceFiles(path)
    return /\.tsx?$/.test(entry.name) && !/\.test\.tsx?$/.test(entry.name) ? [path] : []
  })
}

// Trifft `role="tab"`, `role = 'tab'`, `role: 'tablist'` und Ausdrücke wie `role={on ? 'tab' : undefined}`.
function tabRoles(source: string): string[] {
  return [...source.matchAll(/role\s*[=:]\s*(?:(['"`])tab(?:list)?\1|\{[^}]*?(['"`])tab(?:list)?\2[^}]*\})/g)].map((m) => m[0])
}

describe('Reiterschienen', () => {
  it('Given alle Quelltexte der Oberfläche When nach role="tablist" und role="tab" gesucht wird Then gibt es keine', () => {
    const found = sourceFiles(srcDir).flatMap((file) => tabRoles(readFileSync(file, 'utf-8')).map((role) => `${relative(srcDir, file)}: ${role}`))
    expect(found).toEqual([])
  })

  it('erkennt role="tab" und role="tablist" und lässt role="tabpanel" und andere Rollen durch', () => {
    expect(tabRoles('<nav role="tablist"><button role="tab" /><div role="tabpanel" /><p role="table" /><b role={\'tab\'} />')).toEqual([
      'role="tablist"',
      'role="tab"',
      "role={'tab'}",
    ])
    expect(tabRoles("<i role = 'tab' /><i role={on ? 'tab' : undefined} /><i {...{ role: 'tablist' }} /><i role={`tab`} />")).toHaveLength(4)
    expect(tabRoles("<i role={on ? 'tabpanel' : 'table'} />")).toEqual([])
  })
})
