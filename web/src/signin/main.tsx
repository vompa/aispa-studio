import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { SignInApp } from './SignInApp'
import '../tokens.css'
import './signIn.css'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <SignInApp />
  </StrictMode>,
)
