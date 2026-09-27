import { BrowserRouter } from 'react-router-dom'
import { ErrorBoundary } from './components/common/ErrorBoundary'
import { AuthProvider } from './contexts/AuthProvider'
import { ToastProvider } from './contexts/ToastProvider'
import { AppRoutes } from './routes/AppRoutes'

export default function App() {
  return (
    <ErrorBoundary>
      <BrowserRouter>
        <AuthProvider>
          <ToastProvider>
            <AppRoutes />
          </ToastProvider>
        </AuthProvider>
      </BrowserRouter>
    </ErrorBoundary>
  )
}
