import { useEffect, useState } from 'react'
import { api, type OnlineAppointment } from './api'
import { Alert, Heading, Status } from './components'
import { useNotice } from './notice'
import { useT } from './i18n'
import { CardGuaranteeBox } from './CardGuarantee'
import { DepositBox } from './Deposit'
import { AppointmentCard } from './MyAppointmentsPage'
import { href } from './router'

/** Termin über den Link aus der Bestätigungsmail: ansehen und stornieren, ohne Konto. */
export function ManagePage({ token, navigatedHere }: { token: string; navigatedHere: boolean }) {
  const { t } = useT()
  const [appointment, setAppointment] = useState<OnlineAppointment | null | undefined>(undefined)
  const [message, setMessage] = useNotice()

  useEffect(() => {
    api.GET('/api/online/bookings/{token}', { params: { path: { token } } }).then(
      ({ data }) => setAppointment(data ?? null),
      () => setAppointment(null),
    )
  }, [token])

  return (
    <>
      <Heading focus={navigatedHere}>{t.manageBooking}</Heading>
      {appointment === undefined && <Status>{t.loading}</Status>}
      {appointment === null && <Alert>{t.linkInvalid}</Alert>}
      {message && (
        <Status tone="success" focus key={message.n}>
          {message.text}
        </Status>
      )}
      {appointment && (
        <>
          <AppointmentCard
            appointment={appointment}
            cancel={(acceptFee) => api.POST('/api/online/bookings/{token}/cancel', { params: { path: { token }, query: { acceptFee } } })}
            calendarUrl={`/api/online/bookings/${encodeURIComponent(token)}/calendar.ics`}
            onCancelled={(a) => {
              setAppointment(a)
              setMessage(t.cancelDone)
            }}
            attend={() => api.POST('/api/online/bookings/{token}/confirm', { params: { path: { token } } })}
            onAttended={(a) => {
              setAppointment(a)
              setMessage(t.attendDone)
            }}
            move={(start) => api.POST('/api/online/bookings/{token}/move', { params: { path: { token } }, body: { start } })}
            onMoved={(a) => {
              setAppointment(a)
              setMessage(t.moveDone)
            }}
          />
          <CardGuaranteeBox
            appointment={appointment}
            onChange={setAppointment}
            reload={() => api.GET('/api/online/bookings/{token}', { params: { path: { token } } })}
            test={(saved) => api.POST('/api/online/bookings/{token}/card/test', { params: { path: { token } }, body: { saved } })}
            retry={() => api.POST('/api/online/bookings/{token}/card/retry', { params: { path: { token } } })}
            accept={() => api.POST('/api/online/bookings/{token}/card/accept', { params: { path: { token } } })}
          />
          <DepositBox
            appointment={appointment}
            onChange={setAppointment}
            reload={() => api.GET('/api/online/bookings/{token}', { params: { path: { token } } })}
            test={(paid) => api.POST('/api/online/bookings/{token}/deposit/test', { params: { path: { token } }, body: { paid } })}
            retry={() => api.POST('/api/online/bookings/{token}/deposit/retry', { params: { path: { token } } })}
          />
        </>
      )}
      <p>
        <a href={href({ page: 'book' })}>{t.bookAnother}</a>
      </p>
    </>
  )
}
