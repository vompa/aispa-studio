import { useCallback, useEffect, useState } from 'react'
import { api, type ManagementReportDto } from '../api/client'
import { dayLabel, problemText } from './format'

type State = { kind: 'loading' } | { kind: 'ready'; report: ManagementReportDto } | { kind: 'problem'; text: string }

const euro = new Intl.NumberFormat('de-AT', { style: 'currency', currency: 'EUR' })
const percent = (value: number) => `${value.toLocaleString('de-AT', { maximumFractionDigits: 1 })} %`

/**
 * „Heute im Spa“: die Kennzahlen des heutigen Tages für Inhaberin und Managerin, nur lesend. Die Zahlen kommen unverändert aus dem
 * Bericht der Leitung; hier wird nichts gerechnet. Geldbeträge bleiben im Speicher der Seite und nie im Offline-Speicher des Geräts.
 */
export function Today({ locationId, today, onSignedOut }: { locationId: string; today: string; onSignedOut: () => void }) {
  const [state, setState] = useState<State>({ kind: 'loading' })
  const [reload, setReload] = useState(0)

  useEffect(() => {
    let cancelled = false
    api
      .GET('/api/v1/reports/locations/{locationId}/management', { params: { path: { locationId }, query: { from: today, until: today } } })
      .then(({ data, error, response }) => {
        if (cancelled) return
        if (response.status === 401) return onSignedOut()
        setState(data ? { kind: 'ready', report: data } : { kind: 'problem', text: problemText(error, 'Die Kennzahlen konnten nicht geladen werden.') })
      })
      .catch(() => {
        if (!cancelled) setState({ kind: 'problem', text: 'Kennzahlen brauchen eine Verbindung.' })
      })
    return () => {
      cancelled = true
    }
  }, [locationId, today, reload, onSignedOut])

  const refresh = useCallback(() => {
    setState({ kind: 'loading' })
    setReload((r) => r + 1)
  }, [])

  return (
    <section aria-labelledby="today-heading">
      <h2 id="today-heading">Heute im Spa</h2>
      <p className="shifts">{dayLabel(today)}</p>
      {state.kind === 'loading' && (
        <p className="muted" role="status">
          Lade Kennzahlen …
        </p>
      )}
      {state.kind === 'problem' && <p role="alert">{state.text}</p>}
      {state.kind === 'ready' && <Figures report={state.report} />}
      {state.kind !== 'loading' && (
        <div className="actions">
          <button type="button" className="secondary" onClick={refresh}>
            Aktualisieren
          </button>
        </div>
      )}
    </section>
  )
}

function Figures({ report }: { report: ManagementReportDto }) {
  const value = (key: string) => report.kpis.find((k) => k.key === key)?.value
  const staff = value('staffUtilization')
  const rooms = value('roomUtilization')
  const revenue = value('revenue')
  const requests = value('openRequests')
  const rows: { label: string; text: string }[] = []
  if (!report.amountsHidden && revenue !== undefined) rows.push({ label: 'Umsatz laut Kassa', text: euro.format(revenue) })
  if (staff !== undefined) rows.push({ label: 'Auslastung Personal', text: percent(staff) })
  if (rooms !== undefined) rows.push({ label: 'Auslastung Räume', text: percent(rooms) })
  rows.push({ label: 'Absagen', text: String(report.cancellations) }, { label: 'Nicht erschienen', text: String(report.noShows) })
  if (requests !== undefined) rows.push({ label: 'Offene Anfragen', text: String(requests) })

  return (
    <>
      {report.amountsHidden && <p className="muted">Beträge sehen nur Personen mit dem Recht „Berichte mit Beträgen“.</p>}
      <ul className="figures" aria-label="Kennzahlen des Tages">
        {rows.map((r) => (
          <li key={r.label}>
            <span>{r.label}</span> <strong>{r.text}</strong>
          </li>
        ))}
      </ul>
    </>
  )
}
