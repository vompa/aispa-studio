import { api } from '../api/client'
import type { components } from '../api/schema'

// Nur die Typen des Kundenbereichs; der Client ist derselbe wie in der Oberfläche des Teams (eine kleine Datei).
export { api }

type Schemas = components['schemas']

export type OnlineLocation = Schemas['OnlineLocation']
export type OnlineImprint = Schemas['OnlineImprint']
export type OnlineTreatment = Schemas['OnlineTreatment']
export type OnlineAvailability = Schemas['OnlineAvailability']
export type OnlineAppointment = Schemas['OnlineAppointment']
export type OnlineOwnedVoucher = Schemas['OnlineOwnedVoucher']
export type OnlineBookingConfirmation = Schemas['OnlineBookingConfirmation']
export type OnlineContact = Schemas['OnlineContact']
export type OnlineAssistantAnswer = Schemas['OnlineAssistantAnswer']
export type OnlineSuggestedTime = Schemas['OnlineSuggestedTime']
export type CustomerSession = Schemas['CustomerSession']
export type AnamnesisFormDefinition = Schemas['AnamnesisFormDefinition']
export type AnamnesisAnswer = Schemas['AnamnesisAnswer']
export type AnamnesisLinkInfo = Schemas['AnamnesisLinkInfo']
export type WaitlistEntryDto = Schemas['WaitlistEntryDto']
export type WaitlistConfirmation = Schemas['WaitlistConfirmation']
export type OnlineConsentDto = Schemas['OnlineConsentDto']
export type DayPart = Schemas['DayPart']
export type OnlineVoucherOffer = Schemas['OnlineVoucherOffer']
export type VoucherOrderDto = Schemas['VoucherOrderDto']
export type VoucherKind = Schemas['VoucherKind']

/** Feldfehler aus einer ValidationProblem-Antwort (HTTP 400), Schlüssel in camelCase. */
export function fieldErrors(error: unknown): Record<string, string[]> {
  if (error && typeof error === 'object' && 'errors' in error) {
    return (error as { errors: Record<string, string[]> }).errors ?? {}
  }
  return {}
}
