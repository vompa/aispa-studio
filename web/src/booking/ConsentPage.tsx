import { useEffect, useState } from 'react'
import { api, type OnlineConsentDto } from './api'
import { Alert, Heading, Status } from './components'
import { useNotice } from './notice'
import { useT } from './i18n'
import { href } from './router'

/**
 * Link aus der Mail zur Einwilligung in Erinnerungen an die nächste Behandlung. Das Öffnen ändert nichts (Virenscanner rufen
 * Links vorab auf); bestätigt oder widerrufen wird mit dem Knopf, ohne Anmeldung.
 */
export function ConsentPage({ token, navigatedHere }: { token: string; navigatedHere: boolean }) {
  const { t } = useT()
  const [consent, setConsent] = useState<OnlineConsentDto | null | undefined>(undefined)
  const [busy, setBusy] = useState(false)
  const [failed, setFailed] = useState(false)
  const [message, setMessage] = useNotice()

  useEffect(() => {
    api.GET('/api/online/consent/{token}', { params: { path: { token } } }).then(
      ({ data }) => setConsent(data ?? null),
      () => setConsent(null),
    )
  }, [token])

  async function change(action: 'confirm' | 'withdraw') {
    if (busy) return
    setBusy(true)
    setFailed(false)
    try {
      const { data } =
        action === 'confirm'
          ? await api.POST('/api/online/consent/{token}/confirm', { params: { path: { token } } })
          : await api.POST('/api/online/consent/{token}/withdraw', { params: { path: { token } } })
      if (data) {
        setConsent(data)
        setMessage(data.state === 'Confirmed' ? t.consentConfirmed : data.state === 'Withdrawn' ? t.consentWithdrawn : t.consentPending)
      } else {
        setFailed(true)
      }
    } catch {
      setFailed(true)
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      <Heading focus={navigatedHere}>{t.consentTitle}</Heading>
      {consent === undefined && <Status>{t.loading}</Status>}
      {consent === null && <Alert>{t.linkInvalid}</Alert>}
      {failed && <Alert>{t.errorGeneric}</Alert>}
      {message && (
        <Status tone="success" focus key={message.n}>
          {message.text}
        </Status>
      )}
      {consent && (
        <>
          <p>{t.consentIntro(consent.spaName)}</p>
          <p>
            {consent.state === 'Confirmed' ? t.consentActive : consent.state === 'Pending' ? t.consentPending : t.consentInactive}
          </p>
          <div className="actions">
            {consent.state !== 'Confirmed' && (
              <button type="button" onClick={() => void change('confirm')} aria-disabled={busy}>
                {t.consentConfirm}
              </button>
            )}
            {consent.state !== 'Withdrawn' && (
              <button type="button" className="secondary" onClick={() => void change('withdraw')} aria-disabled={busy}>
                {t.consentWithdraw}
              </button>
            )}
          </div>
        </>
      )}
      <p>
        <a href={href({ page: 'book' })}>{t.navBook}</a>
      </p>
    </>
  )
}
