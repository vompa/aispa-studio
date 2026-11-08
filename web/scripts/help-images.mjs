// Fotografiert Web-Seiten für die Hilfe (web/src/help/images/web/*.png), Liste in help-images.json; jede Aufnahme nennt ihren Pfad
// (`path`, z. B. `/mein-tag/`), denn unter `/` gibt es seit Slice 103 keine Team-Oberfläche mehr.
// Braucht eine laufende Umgebung mit Testdaten (z. B. `docker compose up -d --build` oder Aspire) und meldet sich mit den
// Testkonten an (POST /api/auth/dev/login, nur in der Entwicklung). Die Bilder der Windows-App macht scripts/help-images.sh.
// Aufruf: node scripts/help-images.mjs [Basisadresse, Standard http://localhost:5173] [Name …]
// Danach im Hauptordner: python3 scripts/help-images-shrink.py
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { chromium } from 'playwright-core'

const base = process.argv[2] ?? 'http://localhost:5173'
const only = process.argv.slice(3)
const here = import.meta.dirname
const shots = JSON.parse(readFileSync(join(here, 'help-images.json'), 'utf8'))
const accounts = {
  Owner: 'inhaberin',
  Manager: 'leitung.wien',
  Reception: 'empfang.wien',
  Therapist: 'lena.huber',
}

// Ein vorhandener Browser genügt: CHROMIUM_PATH oder sonst das installierte Google Chrome.
const launch = process.env.CHROMIUM_PATH ? { executablePath: process.env.CHROMIUM_PATH } : { channel: 'chrome' }
const browser = await chromium.launch({ ...launch, args: ['--lang=de-AT'], env: { ...process.env, LANG: 'de_AT.UTF-8' } })
let failed = 0
for (const shot of shots.filter((s) => only.length === 0 || only.includes(s.name))) {
  if (!shot.path?.startsWith('/')) {
    failed++
    console.error(`${shot.name}: Pfad fehlt (path, z. B. "/mein-tag/")`)
    continue
  }
  const phone = shot.path.startsWith('/mein-tag')
  const context = await browser.newContext({
    viewport: phone ? { width: 400, height: 860 } : { width: 1440, height: 900 },
    deviceScaleFactor: 1,
    locale: 'de-AT',
    timezoneId: 'Europe/Vienna',
    colorScheme: 'light',
  })
  const page = await context.newPage()
  try {
    const login = await page.request.post(`${base}/api/auth/dev/login`, {
      data: { email: `${accounts[shot.role] ?? shot.role}@demo.aispa.test`, persistent: false },
    })
    if (!login.ok()) throw new Error(`Anmeldung als ${shot.role}: ${login.status()}`)
    await page.goto(base + shot.path)
    await page.waitForLoadState('networkidle')
    if (shot.nav) await page.getByRole('button', { name: shot.nav, exact: true }).first().click()
    for (const step of shot.steps ?? []) await run(page, step)
    await page.waitForLoadState('networkidle')
    await page.waitForTimeout(400)
    await page.screenshot({ path: join(here, '..', 'src', 'help', 'images', 'web', `${shot.name}.png`), fullPage: false })
    console.log(`${shot.name}.png`)
  } catch (e) {
    failed++
    console.error(`${shot.name}: ${e.message.split('\n')[0]}`)
  }
  await context.close()
}
await browser.close()
process.exit(failed ? 1 : 0)

async function run(page, step) {
  if (step.click) await page.getByRole('button', { name: step.click, exact: step.exact ?? false }).first().click()
  if (step.tab) await page.getByRole(step.role ?? 'button', { name: step.tab, exact: true }).first().click()
  if (step.text) await page.getByText(step.text, { exact: step.exact ?? false }).first().click()
  if (step.css) await page.locator(step.css).nth(step.index ?? 0).click()
  if (step.fill) await page.getByLabel(step.fill[0]).first().fill(step.fill[1])
  if (step.select) await page.getByLabel(step.select[0]).first().selectOption({ label: step.select[1] })
  if (step.scroll) await page.getByText(step.scroll, { exact: step.exact ?? false }).first().scrollIntoViewIfNeeded()
  await page.waitForLoadState('networkidle')
  await page.waitForTimeout(step.wait ?? 200)
}
