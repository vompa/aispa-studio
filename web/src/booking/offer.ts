import type { components } from '../api/schema'

type LocationContact = components['schemas']['LocationContact']

/** Anschrift in einer Zeile, z. B. „Kärntner Straße 1, 1010 Wien“; leer, wenn nichts gepflegt ist. */
export function address(contact: LocationContact) {
  const town = [contact.postalCode, contact.city].filter(Boolean).join(' ')
  return [contact.street, town].filter(Boolean).join(', ')
}

/**
 * Link zur Routenplanung mit dem Standort als Ziel. Ohne Startpunkt: die Karten-App nimmt den aktuellen Standort des Geräts und fragt selbst um Erlaubnis;
 * das Kundenportal fragt den Standort nie ab. Apple Karten auf iPhone, iPad und Mac, sonst Google Maps.
 */
export function routeUrl(destination: string, apple: boolean) {
  const target = encodeURIComponent(destination)
  return apple ? `https://maps.apple.com/?daddr=${target}` : `https://www.google.com/maps/dir/?api=1&destination=${target}`
}

/** Gerät von Apple (iPhone, iPad, Mac); iPadOS meldet sich im Browser als „Macintosh“. */
export function isAppleDevice(nav: Pick<Navigator, 'userAgent'> = navigator) {
  return /iPhone|iPad|iPod|Macintosh/.test(nav.userAgent)
}

/** Angebot nach Kategorie sortiert (alphabetisch, ohne Kategorie zuletzt), innerhalb der Kategorie in der gelieferten Reihenfolge. */
export function byCategory<T extends { category?: string | null }>(treatments: T[]): T[] {
  return treatments
    .map((t, i) => ({ t, i }))
    .sort((a, b) => {
      const x = a.t.category ?? ''
      const y = b.t.category ?? ''
      if (x === y) return a.i - b.i
      if (!x) return 1
      if (!y) return -1
      return x.localeCompare(y, 'de')
    })
    .map(({ t }) => t)
}
