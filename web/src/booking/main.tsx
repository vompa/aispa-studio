import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BookingApp } from './BookingApp'
import '../tokens.css'
import './booking.css'
import { registerServiceWorker } from './install'
import { applySpaFromUrl } from './spa'

applySpaFromUrl()
registerServiceWorker()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BookingApp />
  </StrictMode>,
)
