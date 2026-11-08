import { useCallback, useEffect, useRef, useState } from 'react'
import { api, type StockCountSessionDto, type StockItemDto, type StoragePlaceDto } from '../api/client'
import { ConfirmDialog } from '../components/ConfirmDialog'
import { BarcodeScan } from './BarcodeScan'
import { problemText } from './format'
import { findItemByCode } from './stockLookup'

/** Pause nach der letzten Eingabe, dann geht die Zeile als Zwischenstand an die API. */
const SAVE_DELAY_MS = 600

/** `busy`: getippt oder wird gerade gespeichert; `failed`: das Speichern ist gescheitert. */
type LineState = { text: string; busy: boolean; failed: boolean; invalid: boolean }
type Setup = { kind: 'loading' } | { kind: 'ready'; places: StoragePlaceDto[]; items: Map<string, StockItemDto> } | { kind: 'problem'; text: string }

/** Text im Zählfeld als Menge: leer heißt „nicht gezählt“ (`null`), Komma oder Punkt, höchstens drei Nachkommastellen. */
function parseCount(text: string): number | null | 'invalid' {
  const trimmed = text.trim().replace(',', '.')
  if (trimmed === '') return null
  if (!/^\d{1,6}(\.\d{1,3})?$/.test(trimmed)) return 'invalid'
  return Number(trimmed)
}

const asText = (counted: number) => String(counted).replace('.', ',')

/**
 * Inventur am Handy: Lagerort wählen, Zählung starten oder fortsetzen, blind zählen (das Soll steht nur in der Windows-App),
 * Zwischenstand je Zeile speichern, abschicken. Buchen tut nur die Leitung. Geht nur mit Verbindung; nichts wird still auf dem Gerät gespeichert.
 */
export function StockCountPanel({ locationId, onSignedOut }: { locationId: string; onSignedOut: () => void }) {
  const [setup, setSetup] = useState<Setup>({ kind: 'loading' })
  const [placeId, setPlaceId] = useState('')
  const [session, setSession] = useState<StockCountSessionDto | null>(null)
  const [lines, setLines] = useState<Record<string, LineState>>({})
  const [extraItems, setExtraItems] = useState<StockItemDto[]>([])
  const [problem, setProblem] = useState<string | null>(null)
  const [everSaved, setEverSaved] = useState(false)
  const [confirming, setConfirming] = useState(false)
  const [submitError, setSubmitError] = useState<string | null>(null)
  const [focusRequest, setFocusRequest] = useState<{ id: string; n: number } | null>(null)
  const timers = useRef(new Map<string, number>())
  const inputs = useRef(new Map<string, HTMLInputElement>())
  const sessionRef = useRef(session)
  const linesRef = useRef(lines)

  useEffect(() => {
    sessionRef.current = session
    linesRef.current = lines
  })

  useEffect(() => {
    let cancelled = false
    Promise.all([api.GET('/api/v1/stock/locations/{locationId}/storage-places', { params: { path: { locationId } } }), api.GET('/api/v1/stock/items')]).then(
      ([placesResult, itemsResult]) => {
        if (cancelled) return
        if (placesResult.response.status === 401) return onSignedOut()
        if (!placesResult.data || !itemsResult.data) {
          setSetup({ kind: 'problem', text: problemText(placesResult.error ?? itemsResult.error, 'Die Lagerorte konnten nicht geladen werden.') })
          return
        }
        const places = placesResult.data.filter((p) => p.isActive).sort((a, b) => a.position - b.position)
        setSetup({ kind: 'ready', places, items: new Map(itemsResult.data.filter((i) => i.isActive).map((i) => [i.id, i])) })
        setPlaceId(places[0]?.id ?? '')
      },
      () => !cancelled && setSetup({ kind: 'problem', text: 'Die Inventur geht nur mit Verbindung.' }),
    )
    return () => {
      cancelled = true
    }
  }, [locationId, onSignedOut])

  useEffect(() => {
    if (!focusRequest) return
    inputs.current.get(focusRequest.id)?.focus()
  }, [focusRequest])

  async function start() {
    setProblem(null)
    try {
      const { data, error, response } = await api.POST('/api/v1/stock/locations/{locationId}/storage-places/{placeId}/count-sessions', {
        params: { path: { locationId, placeId } },
      })
      if (response.status === 401) return onSignedOut()
      if (!data) return setProblem(problemText(error, 'Die Zählung konnte nicht gestartet werden.'))
      setLines(Object.fromEntries(data.lines.map((l) => [l.itemId, { text: asText(l.counted), busy: false, failed: false, invalid: false }])))
      setExtraItems([])
      setSession(data)
    } catch {
      setProblem('Zählen geht nur mit Verbindung. Es wird nichts auf diesem Gerät gespeichert.')
    }
  }

  const save = useCallback(
    async (itemId: string) => {
      const current = sessionRef.current
      const state = linesRef.current[itemId]
      if (!current || !state) return
      const counted = parseCount(state.text)
      if (counted === 'invalid') return
      let ok = false
      try {
        const { data, response } = await api.PUT('/api/v1/stock/locations/{locationId}/count-sessions/{sessionId}/lines/{itemId}', {
          params: { path: { locationId, sessionId: current.id, itemId } },
          body: { counted },
        })
        if (response.status === 401) return onSignedOut()
        ok = data !== undefined
      } catch {
        ok = false
      }
      // Wurde währenddessen weitergetippt, bleibt die Zeile busy, bis der Timer sie erneut speichert.
      setLines((all) => (all[itemId].text === state.text ? { ...all, [itemId]: { ...all[itemId], busy: false, failed: !ok } } : all))
      if (ok) setEverSaved(true)
    },
    [locationId, onSignedOut],
  )

  // Beim Verlassen der Maske gehen noch wartende Eingaben sofort an die API, statt still zu verfallen.
  useEffect(() => {
    const pending = timers.current
    return () =>
      pending.forEach((timer, itemId) => {
        window.clearTimeout(timer)
        void save(itemId)
      })
  }, [save])

  function change(itemId: string, text: string) {
    const invalid = parseCount(text) === 'invalid'
    setLines((all) => ({ ...all, [itemId]: { text, busy: !invalid, failed: false, invalid } }))
    window.clearTimeout(timers.current.get(itemId))
    if (invalid) return
    timers.current.set(
      itemId,
      window.setTimeout(() => {
        timers.current.delete(itemId)
        // Der Zustand der Zeile steht erst nach dem Rendern in linesRef.
        void save(itemId)
      }, SAVE_DELAY_MS),
    )
  }

  async function onCode(code: string): Promise<string | null> {
    const found = await findItemByCode(code)
    if ('problem' in found) return found.problem
    const { item } = found
    setExtraItems((all) => (all.some((i) => i.id === item.id) ? all : [...all, item]))
    setFocusRequest((request) => ({ id: item.id, n: (request?.n ?? 0) + 1 }))
    return null
  }

  async function submit() {
    if (!session) return
    setSubmitError(null)
    try {
      const { data, error } = await api.POST('/api/v1/stock/locations/{locationId}/count-sessions/{sessionId}/submit', {
        params: { path: { locationId, sessionId: session.id } },
      })
      if (!data) return setSubmitError(problemText(error, 'Die Zählung konnte nicht abgeschickt werden.'))
      setConfirming(false)
      setSession(data)
    } catch {
      setSubmitError('Keine Verbindung. Die Zählung ist noch nicht abgeschickt.')
    }
  }

  if (setup.kind === 'loading') {
    return (
      <p className="muted" role="status">
        Lade Lagerorte …
      </p>
    )
  }
  if (setup.kind === 'problem') return <p role="alert">{setup.text}</p>

  const place = setup.places.find((p) => p.id === placeId)
  const states = Object.values(lines)
  const saving = states.some((l) => l.busy)
  const failed = states.some((l) => l.failed)
  const invalid = states.some((l) => l.invalid)
  const hasValue = states.some((l) => parseCount(l.text) !== null && parseCount(l.text) !== 'invalid')
  const canSubmit = !saving && !failed && !invalid && hasValue
  const submitted = session?.status === 'Submitted'
  const known = new Map([...setup.items, ...extraItems.map((i): [string, StockItemDto] => [i.id, i])])
  const rowIds = session
    ? [...new Set([...(place?.minimums.map((m) => m.itemId) ?? []), ...session.lines.map((l) => l.itemId), ...extraItems.map((i) => i.id)])]
        .filter((id) => known.has(id))
        .sort((a, b) => known.get(a)!.name.localeCompare(known.get(b)!.name, 'de'))
    : []

  return (
    <section aria-labelledby="count-heading">
      <h2 id="count-heading">Inventur</h2>
      <p className="muted">Zählen geht nur mit Verbindung. Die Menge steht erst, wenn „Gespeichert“ erscheint. Den Bestand sehen Sie hier nicht, die Leitung bucht die Zählung.</p>

      {!session && (
        <div className="panel">
          <div className="field">
            <label htmlFor="count-place">Lagerort</label>
            <select id="count-place" value={placeId} onChange={(e) => setPlaceId(e.target.value)}>
              {setup.places.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </select>
          </div>
          <div className="actions">
            <button type="button" aria-disabled={!placeId || undefined} onClick={() => placeId && void start()}>
              Zählung starten
            </button>
          </div>
          {problem && <p role="alert">{problem}</p>}
        </div>
      )}

      {session && (
        <>
          <h3>{session.storagePlaceName}</h3>
          {submitted ? (
            <div className="notice">
              <p>
                <strong>Abgeschickt.</strong>
              </p>
              <p>Die Leitung schließt die Zählung ab.</p>
            </div>
          ) : (
            <BarcodeScan onCode={onCode} />
          )}

          <ul className="count-lines">
            {rowIds.map((id) => {
              const item = known.get(id)!
              const line = lines[id]
              return (
                <li key={id}>
                  <label htmlFor={`count-${id}`}>
                    {item.name} ({item.unit})
                  </label>
                  <input
                    id={`count-${id}`}
                    ref={(el) => {
                      if (el) inputs.current.set(id, el)
                      else inputs.current.delete(id)
                    }}
                    type="text"
                    inputMode="decimal"
                    autoComplete="off"
                    value={line?.text ?? ''}
                    readOnly={submitted}
                    aria-invalid={line?.invalid || line?.failed || undefined}
                    aria-describedby={line?.invalid ? `count-${id}-hint` : undefined}
                    onChange={(e) => change(id, e.target.value)}
                  />
                  {line?.invalid && (
                    <span id={`count-${id}-hint`} className="count-hint">
                      Menge ab 0, höchstens drei Nachkommastellen
                    </span>
                  )}
                </li>
              )
            })}
          </ul>

          {!submitted && (
            <>
              <p role="status" className="message">
                {saving ? 'Speichert …' : everSaved && !failed ? 'Gespeichert' : ''}
              </p>
              {failed && (
                <div className="notice">
                  <p role="alert">Nicht gespeichert. Bitte die Verbindung prüfen und noch einmal versuchen.</p>
                  <div className="actions">
                    <button
                      type="button"
                      className="secondary"
                      onClick={() =>
                        Object.entries(lines)
                          .filter(([, l]) => l.failed)
                          .forEach(([id]) => void save(id))
                      }
                    >
                      Erneut speichern
                    </button>
                  </div>
                </div>
              )}
              <div className="actions">
                <button type="button" aria-disabled={!canSubmit || undefined} onClick={() => canSubmit && setConfirming(true)}>
                  Abschicken
                </button>
              </div>
            </>
          )}
        </>
      )}

      {confirming && (
        <ConfirmDialog
          title="Zählung abschicken?"
          text="Danach kann dieses Handy nichts mehr ändern. Die Leitung schließt die Zählung ab und bucht den Bestand."
          confirmLabel="Jetzt abschicken"
          danger={false}
          error={submitError}
          onConfirm={() => void submit()}
          onCancel={() => setConfirming(false)}
        />
      )}
    </section>
  )
}
