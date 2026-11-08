import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { OperatorApp } from './OperatorApp'
import '../tokens.css'
import './operator.css'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <OperatorApp />
  </StrictMode>,
)
