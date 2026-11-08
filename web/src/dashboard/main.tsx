import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { DashboardApp } from './DashboardApp'
import '../tokens.css'
import './dashboard.css'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <DashboardApp />
  </StrictMode>,
)
