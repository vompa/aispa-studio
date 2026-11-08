import { useEffect, useState } from 'react'

// Einfache Hash-Routen: Der Kundenbereich ist eine einzige Seite (/buchen/), Codes aus Mail-Links stehen hinter dem #
// und gehen deshalb nicht in Server- oder Proxy-Logs.

/** Vorbelegung der Buchung aus der Mail der Warteliste: #/?standort=…&behandlung=…&datum=2027-03-01 */
export type BookPrefill = { locationId: string; treatmentId: string; date: string | null }

export type Route =
  | { page: 'book'; prefill?: BookPrefill }
  | { page: 'mine' }
  | { page: 'signin' }
  | { page: 'manage'; token: string }
  | { page: 'waitlist'; token: string }
  | { page: 'consent'; token: string }
  | { page: 'voucher' }
  | { page: 'voucherOrder'; token: string }
  | { page: 'anamnesis'; token?: string }
  | { page: 'accessibility' }
  | { page: 'imprint' }

/** Teil hinter „?“ in der Hash-Route, z. B. die Sprache eines Papierbogens: #/anamnese/{code}?lang=en */
export function hashParam(hash: string, name: string): string | null {
  const query = hash.split('?')[1]
  return query ? new URLSearchParams(query).get(name) : null
}

export function parseRoute(hash: string): Route {
  const [first, second] = hash.replace(/^#\/?/, '').split('?')[0].split('/')
  switch (first) {
    case 'termine':
      return { page: 'mine' }
    case 'anmelden':
      return { page: 'signin' }
    case 'termin':
      return second ? { page: 'manage', token: second } : { page: 'book' }
    case 'anamnese':
      return second ? { page: 'anamnesis', token: second } : { page: 'anamnesis' }
    case 'gutschein':
      return second ? { page: 'voucherOrder', token: second } : { page: 'voucher' }
    case 'warteliste':
      return second ? { page: 'waitlist', token: second } : { page: 'book' }
    case 'einwilligung':
      return second ? { page: 'consent', token: second } : { page: 'book' }
    case 'barrierefreiheit':
      return { page: 'accessibility' }
    case 'impressum':
      return { page: 'imprint' }
    default:
      return { page: 'book', prefill: prefill(hash) }
  }
}

const guid = /^[0-9a-f-]{36}$/i

function prefill(hash: string): BookPrefill | undefined {
  const locationId = hashParam(hash, 'standort')
  const treatmentId = hashParam(hash, 'behandlung')
  const date = hashParam(hash, 'datum')
  if (!locationId || !treatmentId || !guid.test(locationId) || !guid.test(treatmentId)) return undefined
  return { locationId, treatmentId, date: date && /^\d{4}-\d{2}-\d{2}$/.test(date) ? date : null }
}

export function href(route: Route): string {
  switch (route.page) {
    case 'book':
      return route.prefill
        ? `#/?standort=${route.prefill.locationId}&behandlung=${route.prefill.treatmentId}${route.prefill.date ? `&datum=${route.prefill.date}` : ''}`
        : '#/'
    case 'mine':
      return '#/termine'
    case 'signin':
      return '#/anmelden'
    case 'manage':
      return `#/termin/${route.token}`
    case 'waitlist':
      return `#/warteliste/${route.token}`
    case 'consent':
      return `#/einwilligung/${route.token}`
    case 'voucher':
      return '#/gutschein'
    case 'voucherOrder':
      return `#/gutschein/${route.token}`
    case 'anamnesis':
      return route.token ? `#/anamnese/${route.token}` : '#/anamnese'
    case 'accessibility':
      return '#/barrierefreiheit'
    case 'imprint':
      return '#/impressum'
  }
}

export function navigate(route: Route) {
  window.location.hash = href(route)
}

export function useRoute(): Route {
  const [route, setRoute] = useState(() => parseRoute(window.location.hash))
  useEffect(() => {
    const update = () => setRoute(parseRoute(window.location.hash))
    window.addEventListener('hashchange', update)
    return () => window.removeEventListener('hashchange', update)
  }, [])
  return route
}
