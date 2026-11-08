// Prüft nach `vite build`, dass die Oberflächen klein bleiben und nichts aus den anderen laden: die Anmeldung (/anmelden/),
// der Kundenbereich (/buchen/), „Mein Tag“ (/mein-tag/), die Betreiber-Seite (/betreiber/), die Hilfe (/help/) und die Tafel im Pausenraum (/dashboard/).
// Aufruf: node scripts/check-bundles.mjs
import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { gzipSync } from 'node:zlib'

const dist = join(import.meta.dirname, '..', 'dist')
const fontFile = /[\w./-]+\.(?:woff2?|ttf|otf)\b/g

// Budget in kB gzip für alles, was die Seite beim ersten Aufruf lädt; `foreign` sind Einstiege, aus denen sie nichts laden darf.
const entries = [
  // Anmeldung (Slice 103): Passkey, Passwort im Einzelplatz, Einladung und Code für die Windows-App; ohne SignalR und ohne Schrift.
  { name: 'Anmelden', dir: 'anmelden', budgetKb: 100, foreign: ['buchen', 'mein-tag', 'betreiber', 'help', 'dashboard'] },
  { name: 'Kundenbereich', dir: 'buchen', budgetKb: 100, foreign: ['anmelden', 'mein-tag', 'betreiber', 'help', 'dashboard'] },
  { name: 'Mein Tag', dir: 'mein-tag', budgetKb: 120, foreign: ['anmelden', 'buchen', 'betreiber', 'help', 'dashboard'] },
  { name: 'Betreiber', dir: 'betreiber', budgetKb: 100, foreign: ['anmelden', 'buchen', 'mein-tag', 'help', 'dashboard'] },
  // Hilfe (ADR 0079): React und die Stile aus index.css (zusammen etwa 76 kB gzip) plus Suche und Leser; die Themen und Bilder laden erst bei Bedarf.
  { name: 'Hilfe', dir: 'help', budgetKb: 100, foreign: ['anmelden', 'buchen', 'mein-tag', 'betreiber', 'dashboard'] },
  // Tafel im Pausenraum (ADR 0084): als Einzige der schlanken Oberflächen mit SignalR für das Signal zum Neuladen.
  { name: 'Dashboard', dir: 'dashboard', budgetKb: 100, foreign: ['anmelden', 'buchen', 'mein-tag', 'betreiber', 'help'], signalR: true },
]

const problems = []
for (const entry of entries) {
  const html = readFileSync(join(dist, entry.dir, 'index.html'), 'utf8')
  const assets = [...html.matchAll(/(?:src|href)="(?:\.\.)?\/(assets\/[^"]+\.(?:js|css))"/g)].map((m) => m[1])

  console.log(`\n/${entry.dir}/`)
  let total = 0
  for (const asset of assets) {
    const content = readFileSync(join(dist, asset))
    const size = gzipSync(content).length
    total += size
    console.log(`${asset.padEnd(40)} ${(size / 1024).toFixed(1).padStart(6)} kB gzip`)
    for (const other of entry.foreign) {
      if (asset.startsWith(`assets/${other}-`)) problems.push(`${entry.name}: ${asset} gehört zu /${other}/`)
    }
    if (!entry.signalR && asset.endsWith('.js') && content.includes('signalr')) problems.push(`${entry.name}: ${asset} enthält SignalR`)
  }

  console.log(`${'Summe'.padEnd(40)} ${(total / 1024).toFixed(1).padStart(6)} kB gzip (Budget ${entry.budgetKb} kB)`)
  if (assets.length === 0) problems.push(`${entry.name}: keine Dateien in ${entry.dir}/index.html gefunden`)
  if (total > entry.budgetKb * 1024) problems.push(`${entry.name} ist größer als ${entry.budgetKb} kB gzip`)

  // Die Hausschrift Inter liefert nur die Hilfe aus (über index.css, B62); die übrigen laden keine Schrift.
  if (entry.dir !== 'help') {
    const css = assets.filter((asset) => asset.endsWith('.css')).map((asset) => readFileSync(join(dist, asset), 'utf8'))
    for (const font of [html, ...css].flatMap((text) => [...text.matchAll(fontFile)].map((m) => m[0]))) {
      problems.push(`${entry.name}: lädt die Schrift ${font}`)
    }
  }
}

// Hausschrift der Hilfe (B62): Preload im Kopf und @font-face im CSS zeigen auf dieselbe Inter-Datei.
const helpHtml = readFileSync(join(dist, 'help', 'index.html'), 'utf8')
const preload = /<link rel="preload" href="(?:\.\.)?\/(assets\/inter-latin-var-[^"]+\.woff2)" as="font" type="font\/woff2" crossorigin\s*\/?>/.exec(helpHtml)?.[1]
console.log(`\n/help/ Hausschrift: ${preload ?? 'kein Preload'}`)
if (preload === undefined) {
  problems.push('Hilfe: kein Preload für Inter in help/index.html')
} else {
  if (!existsSync(join(dist, preload))) problems.push(`Hilfe: ${preload} fehlt in dist`)
  const helpCss = [...helpHtml.matchAll(/href="(?:\.\.)?\/(assets\/[^"]+\.css)"/g)].map((m) => readFileSync(join(dist, m[1]), 'utf8'))
  const fontFace = helpCss.flatMap((css) => css.match(/@font-face\{[^}]*\}/g) ?? []).find((block) => /font-family:\s*Inter\b/.test(block))
  if (fontFace === undefined) problems.push('Hilfe: kein @font-face für Inter im CSS')
  else if (!fontFace.includes(`/${preload}`)) problems.push(`Hilfe: @font-face für Inter zeigt nicht auf ${preload}`)
}

if (problems.length > 0) {
  for (const p of problems) console.error(`::error::${p}`)
  process.exit(1)
}
