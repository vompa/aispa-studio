import { type ReactNode, useCallback, useEffect, useState } from 'react'
import { api, type CurrentUser, type DayPlanDto, type RestockLine } from '../api/client'
import { addPasskey, passkeysSupported } from '../auth/passkeys'
import { AbsencePanel } from './AbsencePanel'
import { addDays, dateIn, dayLabel, problemText, standLabel, timeIn } from './format'
import { CACHED_AT_HEADER, OFFLINE_HEADER, bindOfflineDataTo, clearOfflineData } from './offline'
import { PlanCard, type Highlight } from './PlanCard'
import { ReceivePanel } from './ReceivePanel'
import { Reminders } from './Reminders'
import { SignIn } from './SignIn'
import { StockCountPanel } from './StockCountPanel'
import { Today } from './Today'
import { Week } from './Week'

type Place = { id: string; name: string; timeZone: string }

type Session =
  | { kind: 'loading' }
  | { kind: 'signedOut' }
  | { kind: 'blocked'; text: string }
  | { kind: 'unreachable' }
  // user ist null, wenn ohne Netz gestartet wurde; dann gilt der zuletzt gewählte Standort.
  | { kind: 'ready'; user: CurrentUser | null; places: Place[] }

type Plan = { data: DayPlanDto; cachedAt: string | null; offline: boolean }

type View = 'day' | 'week' | 'absence' | 'count' | 'receive' | 'today'

const VIEWS: { key: View; label: string }[] = [
  { key: 'day', label: 'Tag' },
  { key: 'week', label: 'Woche' },
  { key: 'absence', label: 'Abwesenheit' },
  { key: 'count', label: 'Inventur' },
]

// Wareneingang bucht: nur mit dem Recht „Buchen“ (Rezeption, Leitung); die API prüft dasselbe Recht noch einmal.
const RECEIVE_VIEW: { key: View; label: string } = { key: 'receive', label: 'Wareneingang' }

// Nur für die Leitung (Recht „Berichte der Leitung“); die API prüft dasselbe Recht noch einmal.
const TODAY_VIEW: { key: View; label: string } = { key: 'today', label: 'Heute im Spa' }

const PLACE_KEY = 'aispa.meinTag.standort'

function storedPlace(): Place | null {
  try {
    const raw = localStorage.getItem(PLACE_KEY)
    const place = raw ? (JSON.parse(raw) as Place) : null
    return place?.id && place.timeZone ? place : null
  } catch {
    return null
  }
}

function storePlace(place: Place) {
  try {
    localStorage.setItem(PLACE_KEY, JSON.stringify(place))
  } catch {
    // Privater Modus: dann eben jedes Mal der erste Standort.
  }
}

/** Wer ist angemeldet und an welchen Standorten? Ohne Netz gilt der zuletzt gewählte Standort. */
async function loadSession(): Promise<{ session: Session; placeId: string | null }> {
  const blocked = (text: string) => ({ session: { kind: 'blocked', text } as Session, placeId: null })
  const remembered = storedPlace()
  let me
  try {
    me = await api.GET('/api/v1/auth/me')
  } catch {
    return remembered
      ? { session: { kind: 'ready', user: null, places: [remembered] }, placeId: remembered.id }
      : { session: { kind: 'unreachable' }, placeId: null }
  }
  if (me.response.status === 401) return { session: { kind: 'signedOut' }, placeId: null }
  const user = me.data
  if (!user) return blocked(problemText(me.error, 'Die Anmeldung konnte nicht geprüft werden.'))
  if (!user.access) return blocked('Dieses Konto gehört zu keinem Spa.')
  if (!user.employeeId) return blocked('Dieses Konto ist mit keiner Person im Team verknüpft. Bitte an die Standortleitung wenden.')
  let places: Place[] = []
  try {
    const { data } = await api.GET('/api/v1/locations')
    places = (data ?? []).map((l) => ({ id: l.id, name: l.name, timeZone: l.timeZone }))
  } catch {
    if (remembered) places = [remembered]
  }
  if (places.length === 0) return blocked('Für dieses Konto ist kein Standort freigegeben.')
  // Ab hier nur noch eigene Daten, auch wenn die Rolle mehr darf (Slice 104). Scheitert der Wechsel, gelten die Rechte der Rolle; die App ruft ohnehin nur Eigenes.
  if (user.channel !== 'StaffApp') await api.POST('/api/v1/auth/channel/staff-app').catch(() => undefined)
  return { session: { kind: 'ready', user, places }, placeId: places.find((p) => p.id === remembered?.id)?.id ?? places[0].id }
}

/** „Mein Tag“: die Handy-App für Mitarbeitende mit Tag, Woche und Abwesenheit; gestempelt wird hier nicht (T-015). Eigenes Bundle unter /mein-tag/. */
export function MyDayApp() {
  const [session, setSession] = useState<Session>({ kind: 'loading' })
  const [placeId, setPlaceId] = useState<string | null>(null)
  const [date, setDate] = useState<string | null>(null)
  const [plan, setPlan] = useState<Plan | null>(null)
  const [planError, setPlanError] = useState<string | null>(null)
  const [reload, setReload] = useState(0)
  const [now, setNow] = useState(() => Date.now())
  const [view, setView] = useState<View>('day')
  const [passkeyMessage, setPasskeyMessage] = useState<string | null>(null)
  const [passkeyAdded, setPasskeyAdded] = useState(false)

  const signedOut = useCallback(() => {
    clearOfflineData()
    setPlan(null)
    setSession({ kind: 'signedOut' })
  }, [])

  const start = useCallback(
    () =>
      loadSession().then((result) => {
        if (result.session.kind === 'signedOut') clearOfflineData()
        if (result.session.kind === 'ready' && result.session.user) bindOfflineDataTo(result.session.user.id)
        setPlaceId(result.placeId)
        setSession(result.session)
      }),
    [],
  )

  useEffect(() => {
    void start()
  }, [start])

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 30_000)
    return () => window.clearInterval(timer)
  }, [])

  const places = session.kind === 'ready' ? session.places : []
  const place = places.find((p) => p.id === placeId) ?? null
  const today = place ? dateIn(place.timeZone, new Date(now)) : null
  const shown = date ?? today
  const placeKey = place?.id
  const placeZone = place?.timeZone

  useEffect(() => {
    if (!placeKey || !placeZone || !shown) return
    let cancelled = false
    api
      .GET('/api/v1/reports/locations/{locationId}/day-plan', { params: { path: { locationId: placeKey }, query: { date: shown } } })
      .then(({ data, error, response }) => {
        if (cancelled) return
        if (response.status === 401) return signedOut()
        if (!data) {
          setPlan(null)
          setPlanError(problemText(error, 'Der Tagesplan konnte nicht geladen werden.'))
          return
        }
        setPlanError(null)
        setPlan({ data, offline: response.headers.get(OFFLINE_HEADER) === '1', cachedAt: response.headers.get(CACHED_AT_HEADER) })
      })
      .catch(() => {
        if (cancelled) return
        setPlan(null)
        setPlanError('Keine Verbindung, und dieser Tag ist auf diesem Gerät nicht gespeichert.')
      })
    return () => {
      cancelled = true
    }
  }, [placeKey, placeZone, shown, reload, signedOut])

  function goTo(next: string | null) {
    setPlan(null)
    setPlanError(null)
    setDate(next)
  }

  function choosePlace(id: string) {
    const next = places.find((p) => p.id === id)
    if (!next) return
    storePlace(next)
    setPlaceId(id)
    goTo(null)
  }

  async function signOut() {
    clearOfflineData()
    try {
      await api.POST('/api/v1/auth/logout')
    } catch {
      // Ohne Netz bleibt das Cookie, die gespeicherten Pläne sind aber weg.
    }
    signedOut()
  }

  async function createPasskey() {
    try {
      const added = await addPasskey()
      setPasskeyAdded(added)
      setPasskeyMessage(added ? 'Passkey eingerichtet.' : 'Der Passkey wurde nicht eingerichtet. Bitte noch einmal versuchen.')
    } catch {
      setPasskeyMessage('Der Passkey wurde nicht eingerichtet. Bitte noch einmal versuchen.')
    }
  }

  const reloadPlan = useCallback(() => setReload((r) => r + 1), [])

  const setStep = useCallback((appointmentId: string, stepId: string, done: boolean) => {
    setPlan((p) =>
      p && {
        ...p,
        data: {
          ...p.data,
          cards: p.data.cards.map((c) =>
            c.appointmentId === appointmentId ? { ...c, todo: c.todo.map((t) => (t.stepId === stepId ? { ...t, done } : t)) } : c,
          ),
        },
      },
    )
  }, [])

  const setRestock = useCallback((appointmentId: string, restock: RestockLine[]) => {
    setPlan((p) => p && { ...p, data: { ...p.data, cards: p.data.cards.map((c) => (c.appointmentId === appointmentId ? { ...c, restock } : c)) } })
  }, [])

  if (session.kind === 'loading') {
    return (
      <Frame>
        <p className="muted" role="status">
          Lade …
        </p>
      </Frame>
    )
  }
  if (session.kind === 'signedOut') {
    return (
      <Frame>
        <SignIn
          onSignedIn={() => {
            setSession({ kind: 'loading' })
            void start()
          }}
        />
      </Frame>
    )
  }
  if (session.kind === 'blocked' || session.kind === 'unreachable') {
    return (
      <Frame>
        <p role="alert">{session.kind === 'blocked' ? session.text : 'Keine Verbindung. Auf diesem Gerät ist noch kein Tag gespeichert.'}</p>
        <div className="actions">
          <button
            type="button"
            onClick={() => {
              setSession({ kind: 'loading' })
              void start()
            }}
          >
            Erneut versuchen
          </button>
        </div>
      </Frame>
    )
  }

  const zone = place?.timeZone ?? 'Europe/Vienna'
  const offline = plan?.offline ?? false
  const cards = plan ? [...plan.data.cards].sort((a, b) => a.start.localeCompare(b.start)) : []
  const highlights = new Map<string, Highlight>()
  if (shown === today) {
    const active = cards.filter((c) => c.status !== 'Cancelled' && c.status !== 'NoShow' && c.status !== 'Completed')
    const current = active.find((c) => c.startedAt && c.status === 'CheckedIn') ?? active.find((c) => Date.parse(c.start) <= now && now < Date.parse(c.end))
    const next = active.find((c) => Date.parse(c.start) > now)
    if (current) highlights.set(current.appointmentId, 'now')
    else if (next) highlights.set(next.appointmentId, 'next')
  }

  return (
    <Frame
      tools={
        <>
          {places.length > 1 && (
            <div className="field compact">
              <label htmlFor="place">Standort</label>
              <select id="place" value={placeId ?? ''} onChange={(e) => choosePlace(e.target.value)}>
                {places.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.name}
                  </option>
                ))}
              </select>
            </div>
          )}
          {session.user?.passkeyCount === 0 && !passkeyAdded && passkeysSupported() && (
            <button type="button" className="link" onClick={() => void createPasskey()}>
              Passkey einrichten
            </button>
          )}
          {session.user && (
            <button type="button" className="link" onClick={() => void signOut()}>
              Abmelden
            </button>
          )}
        </>
      }
    >
      {passkeyMessage && <p role="status">{passkeyMessage}</p>}
      <nav className="views" aria-label="Ansicht">
        {[...VIEWS, ...(session.user?.rights.includes('Book') ? [RECEIVE_VIEW] : []), ...(session.user?.rights.includes('ViewFinance') ? [TODAY_VIEW] : [])].map((v) => (
          <button key={v.key} type="button" className="secondary" aria-current={view === v.key ? 'page' : undefined} onClick={() => setView(v.key)}>
            {v.label}
          </button>
        ))}
      </nav>

      {view === 'week' && place && today && (
        <Week
          locationId={place.id}
          timeZone={zone}
          today={today}
          onSignedOut={signedOut}
          onOpenDay={(day) => {
            goTo(day === today ? null : day)
            setView('day')
          }}
        />
      )}
      {view === 'absence' && today && <AbsencePanel today={today} onSignedOut={signedOut} />}
      {view === 'count' && place && session.user && <StockCountPanel key={place.id} locationId={place.id} onSignedOut={signedOut} />}
      {view === 'receive' && place && session.user && <ReceivePanel key={place.id} locationId={place.id} onSignedOut={signedOut} />}
      {view === 'today' && place && today && <Today key={place.id} locationId={place.id} today={today} onSignedOut={signedOut} />}

      {view === 'day' && shown && (
        <section aria-labelledby="day-heading">
          <nav className="day-nav" aria-label="Tag wählen">
            <button type="button" className="secondary" onClick={() => goTo(addDays(shown, -1))}>
              ‹ Vortag
            </button>
            {shown !== today && (
              <button type="button" className="secondary" onClick={() => goTo(null)}>
                Heute
              </button>
            )}
            <button type="button" className="secondary" onClick={() => goTo(addDays(shown, 1))}>
              Folgetag ›
            </button>
          </nav>
          <h2 id="day-heading">
            {shown === today ? 'Heute, ' : ''}
            {dayLabel(shown)}
          </h2>
          {shown === today && <Reminders />}

          {offline && (
            <div className="notice" role="status">
              <p>
                <strong>
                  Offline, Stand {plan?.cachedAt ? standLabel(plan.cachedAt, zone, today) : 'unbekannt'}
                </strong>
              </p>
              <p>Melden, Abhaken und Notizen gehen wieder, sobald das Handy Netz hat.</p>
            </div>
          )}
          {planError && <p role="alert">{planError}</p>}
          {!plan && !planError && (
            <p className="muted" role="status">
              Lade Tagesplan …
            </p>
          )}

          {plan && (
            <>
              <p className="shifts">
                {plan.data.shifts.length === 0
                  ? 'Kein Dienst an diesem Standort.'
                  : `Dienst ${plan.data.shifts.map((s) => `${timeIn(s.start, zone)}–${timeIn(s.end, zone)}`).join(', ')}`}
              </p>
              {plan.data.hints.length > 0 && (
                <ul className="hints">
                  {plan.data.hints.map((h) => (
                    <li key={h}>{h}</li>
                  ))}
                </ul>
              )}
              {cards.length === 0 ? (
                <p className="empty">{shown === today ? 'Heute keine Termine' : 'An diesem Tag keine Termine'}</p>
              ) : (
                <ol className="cards" aria-label="Termine">
                  {cards.map((c) => (
                    <li key={c.appointmentId}>
                      <PlanCard card={c} timeZone={zone} isToday={shown === today} highlight={highlights.get(c.appointmentId) ?? null} offline={offline} onStep={setStep} onRestock={setRestock} onReported={reloadPlan} />
                    </li>
                  ))}
                </ol>
              )}
            </>
          )}
          {(plan || planError) && (
            <div className="actions">
              <button type="button" className="secondary" onClick={() => setReload((r) => r + 1)}>
                Aktualisieren
              </button>
            </div>
          )}
        </section>
      )}
    </Frame>
  )
}

function Frame({ tools, children }: { tools?: ReactNode; children: ReactNode }) {
  return (
    <>
      <header className="bar">
        <h1>Mein Tag</h1>
        {tools}
      </header>
      <main>{children}</main>
    </>
  )
}
