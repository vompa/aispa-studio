import { closeSync, openSync, readdirSync, readSync } from 'node:fs'
import { join, resolve } from 'node:path'
import react from '@vitejs/plugin-react'
import { defineConfig, type Plugin } from 'vitest/config'

const helpImageSizesId = 'virtual:help-image-sizes'

/** Liest Breite und Höhe der Handbuchbilder aus den PNG-Köpfen, damit die Hilfe sie als `width`/`height` setzen kann (Befund B22). */
function helpImageSizes(): Plugin {
  const imagesDir = resolve(import.meta.dirname, 'src/help/images')
  return {
    name: 'help-image-sizes',
    resolveId: (source) => (source === helpImageSizesId ? `\0${helpImageSizesId}` : undefined),
    load(id) {
      if (id !== `\0${helpImageSizesId}`) return undefined
      const sizes: Record<string, [number, number]> = {}
      const head = Buffer.alloc(24)
      for (const set of ['app', 'web']) {
        for (const file of readdirSync(join(imagesDir, set)).filter((f) => f.endsWith('.png'))) {
          const fd = openSync(join(imagesDir, set, file), 'r')
          try {
            readSync(fd, head, 0, 24, 0)
          } finally {
            closeSync(fd)
          }
          sizes[`${set}/${file.slice(0, -4)}`] = [head.readUInt32BE(16), head.readUInt32BE(20)]
        }
      }
      return `export default ${JSON.stringify(sizes)}`
    },
  }
}

// Aspire setzt API_HTTPS/API_HTTP auf die Adresse der API; ohne Aspire die Standardadresse aus launchSettings.json.
const apiTarget = process.env.API_HTTPS ?? process.env.API_HTTP ?? 'http://localhost:5086'

export default defineConfig({
  plugins: [react(), helpImageSizes()],
  build: {
    rollupOptions: {
      // Sechs getrennte Oberflächen: die Anmeldung (/anmelden/, Slice 103), der Kundenbereich (/buchen/), „Mein Tag“ fürs
      // Handy der Therapeut:innen (/mein-tag/), die Seite des Betreibers (/betreiber/), die Hilfe (/help/, ADR 0079) und die
      // Tafel im Pausenraum (/dashboard/, ADR 0084). Eine Team-Oberfläche im Web gibt es nicht mehr (ADR 0084);
      // gemeinsam ist nur React und der kleine API-Client.
      input: {
        anmelden: resolve(import.meta.dirname, 'anmelden/index.html'),
        buchen: resolve(import.meta.dirname, 'buchen/index.html'),
        'mein-tag': resolve(import.meta.dirname, 'mein-tag/index.html'),
        betreiber: resolve(import.meta.dirname, 'betreiber/index.html'),
        help: resolve(import.meta.dirname, 'help/index.html'),
        dashboard: resolve(import.meta.dirname, 'dashboard/index.html'),
        // Erklärseite ohne Anmeldung (/ueber/): Betriebsarten und angebundene Dienste, statisch ohne Skript.
        ueber: resolve(import.meta.dirname, 'ueber/index.html'),
      },
    },
  },
  server: {
    port: Number(process.env.PORT ?? 5173),
    proxy: {
      // Host-Header bleibt erhalten, damit die Passkey-Prüfung den Ursprung (localhost) wiedererkennt.
      '/api': { target: apiTarget, secure: false },
      '/hubs': { target: apiTarget, secure: false, ws: true },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/vitest.setup.ts'],
    // Ohne Grenze startet Vitest so viele Worker wie Kerne (20) und läuft unter Last in 5-s-Timeouts.
    maxWorkers: 3,
  },
})
