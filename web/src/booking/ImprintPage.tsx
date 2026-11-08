import { useEffect, useState } from 'react'
import { api, type OnlineImprint } from './api'
import { Alert, Heading } from './components'
import { useT } from './i18n'

/**
 * Impressum (§ 5 ECG, § 25 MedienG, § 5 DDG) aus den Firmendaten des Spas. Fehlt dort noch etwas, steht ein Hinweis statt
 * eines unvollständigen Impressums ohne Erklärung.
 */
export function ImprintPage({ navigatedHere }: { navigatedHere: boolean }) {
  const { t } = useT()
  const [imprint, setImprint] = useState<OnlineImprint | null | undefined>(undefined)

  useEffect(() => {
    api.GET('/api/online/imprint').then(
      ({ data }) => setImprint(data ?? null),
      () => setImprint(null),
    )
  }, [])

  const rows: [string, string | null | undefined][] = imprint
    ? [
        [t.imprintPhone, imprint.phone],
        [t.imprintEmail, imprint.email],
        [t.imprintWebsite, imprint.website],
        [t.imprintVatId, imprint.vatId],
        [t.imprintRegister, [imprint.companyRegisterNumber, imprint.companyRegisterCourt].filter(Boolean).join(', ')],
        [t.imprintPurpose, imprint.businessPurpose],
        [t.imprintAuthority, imprint.supervisoryAuthority],
        [t.imprintChamber, imprint.chamberAndRules],
      ]
    : []

  return (
    <>
      <Heading focus={navigatedHere}>{t.imprintTitle}</Heading>
      {imprint === undefined && <p>{t.loading}</p>}
      {imprint === null && <Alert>{t.errorGeneric}</Alert>}
      {imprint && (
        <>
          <address>
            <strong>{imprint.legalName ?? imprint.spaName}</strong>
            {imprint.street && (
              <>
                <br />
                {imprint.street}
              </>
            )}
            {(imprint.postalCode || imprint.city) && (
              <>
                <br />
                {[imprint.postalCode, imprint.city].filter(Boolean).join(' ')}
              </>
            )}
          </address>
          <dl className="imprint">
            {rows
              .filter(([, value]) => value)
              .map(([label, value]) => (
                <div key={label}>
                  <dt>{label}</dt>
                  <dd>{value}</dd>
                </div>
              ))}
          </dl>
          {!imprint.complete && <p className="hint">{t.imprintMissing}</p>}
        </>
      )}
    </>
  )
}
