import { api } from './api'

const KEY = 'aispa.spa'
const MAIL_LINK = /^#\/(termin|warteliste|einwilligung|gutschein|anamnese)\/[^/?]/

/**
 * Welches Spa diese Buchungsseite zeigt: aus dem Link (`?spa=…`), für die weitere Sitzung gemerkt.
 * Der Server liest es aus der Kopfzeile X-Aispa-Spa; Links aus Mails und angemeldete Konten kennt er auch ohne.
 * Ein gemerktes Spa geht bei einem Mail-Link nicht mit: Der Link kann zu einem anderen Spa gehören, und die Kopfzeile ginge vor.
 */
export function applySpaFromUrl() {
  const fromLink = new URLSearchParams(window.location.search).get('spa')
  let spa = fromLink
  try {
    // localStorage, weil die installierte App ohne ?spa= startet und keine Sitzung mit dem Browser teilt.
    if (spa) localStorage.setItem(KEY, spa)
    else spa = localStorage.getItem(KEY)
  } catch {
    // Ohne Speicher (privates Fenster) gilt das Spa nur, solange der Link in der Adresszeile steht.
  }
  if (!spa) return

  const header = spa
  api.use({
    onRequest({ request }) {
      const remembered = !fromLink && MAIL_LINK.test(window.location.hash)
      if (!remembered && new URL(request.url).pathname.startsWith('/api/online')) request.headers.set('X-Aispa-Spa', header)
      return request
    },
  })
}
