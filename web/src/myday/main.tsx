import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { MyDayApp } from './MyDayApp'
import { registerServiceWorker } from './offline'
import '../tokens.css'
import './myday.css'

registerServiceWorker()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <MyDayApp />
  </StrictMode>,
)
