import { Heading } from './components'
import { useT } from './i18n'

/**
 * Erklärung zur Barrierefreiheit (BFSG § 14, Anlage 3 BFSGV). Kontaktdaten und Schlichtungsstelle setzt jeder Betrieb ein,
 * sobald es die Betriebseinstellungen gibt; bis dahin steht hier der allgemeine Teil.
 */
export function AccessibilityPage({ navigatedHere }: { navigatedHere: boolean }) {
  const { t } = useT()
  return (
    <>
      <Heading focus={navigatedHere}>{t.a11yTitle}</Heading>
      <p>{t.a11yIntro}</p>
      <h2>{t.a11yWhatTitle}</h2>
      <ul>
        {t.a11yWhat.map((item) => (
          <li key={item}>{item}</li>
        ))}
      </ul>
      <h2>{t.a11yReportTitle}</h2>
      <p>{t.a11yReport}</p>
      <p>{t.a11yEscalate}</p>
    </>
  )
}
