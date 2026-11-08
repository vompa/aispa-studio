import { beforeEach, describe, expect, it } from 'vitest'
import { listUnanswered, recordUnanswered, removeUnanswered } from './unanswered'

const day = (iso: string) => new Date(`${iso}T10:00:00Z`)

beforeEach(() => localStorage.clear())

describe('Offene Fragen an die Hilfe im Browser', () => {
  it('legt die Frage mit Seite und Zeit ab und fasst gleiche Fragen zusammen', () => {
    recordUnanswered('Wie färbe ich die Wolken lila?', 'calendar', day('2027-03-01'))
    recordUnanswered('wie färbe ich die wolken lila?', 'calendar', day('2027-03-02'))
    recordUnanswered('Wie färbe ich die Wolken lila?', 'pos', day('2027-03-01'))

    expect(listUnanswered(day('2027-03-03'))).toEqual([
      { question: 'wie färbe ich die wolken lila?', page: 'calendar', at: '2027-03-02T10:00:00.000Z', count: 2 },
      { question: 'Wie färbe ich die Wolken lila?', page: 'pos', at: '2027-03-01T10:00:00.000Z', count: 1 },
    ])
  })

  it('vergisst Fragen nach 90 Tagen und Erledigt löscht den Eintrag', () => {
    recordUnanswered('Alt', null, day('2027-01-01'))
    recordUnanswered('Neu', null, day('2027-03-01'))

    // 1.1. + 90 Tage = 1.4.: am 2.4. ist „Alt“ weg, „Neu“ (1.3.) noch da.
    const list = listUnanswered(day('2027-04-02'))
    expect(list.map((q) => q.question)).toEqual(['Neu'])

    removeUnanswered(list[0])
    expect(listUnanswered(day('2027-04-02'))).toEqual([])
  })

  it('fremde Einträge im Speicher fallen weg statt zu werfen', () => {
    localStorage.setItem('aispa.help.unanswered', JSON.stringify([{ question: 3 }, null, { question: 'Gut', page: null, at: '2027-03-01T10:00:00.000Z', count: 1 }]))

    recordUnanswered('Neu', 'pos', day('2027-03-02'))
    expect(listUnanswered(day('2027-03-02')).map((q) => q.question)).toEqual(['Neu', 'Gut'])
  })

  it('ohne Speicher im Browser geht die Bedienung weiter', () => {
    localStorage.setItem('aispa.help.unanswered', 'kein JSON')

    expect(listUnanswered(day('2027-03-01'))).toEqual([])
    recordUnanswered('Frage', null, day('2027-03-01'))
    expect(listUnanswered(day('2027-03-01'))).toHaveLength(1)
  })
})
