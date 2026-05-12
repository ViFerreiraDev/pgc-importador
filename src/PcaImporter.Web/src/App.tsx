import { BrowserRouter, Navigate, Route, Routes, useLocation } from 'react-router-dom'
import { Loader2 } from 'lucide-react'
import { ProvedorAuth, useAuth } from '@/features/auth/AuthContext'
import { ProvedorToken } from '@/features/token/TokenContext'
import { AppLayout } from '@/components/layout/AppLayout'
import { Inicio } from '@/pages/Inicio'
import { Dfds } from '@/pages/Dfds'
import { Importar } from '@/pages/Importar'
import { ValidarLote } from '@/pages/ValidarLote'
import { Historico } from '@/pages/Historico'
import { Logs } from '@/pages/Logs'
import { Configuracoes } from '@/pages/Configuracoes'
import { Login } from '@/pages/Login'
import { Usuarios } from '@/pages/Usuarios'

export default function App() {
  return (
    <BrowserRouter>
      <ProvedorAuth>
        <Routes>
          <Route path="/login" element={<Login />} />
          <Route element={<ExigeAuth><ProvedorToken><AppLayout /></ProvedorToken></ExigeAuth>}>
            <Route path="/" element={<Inicio />} />
            <Route path="/dfds" element={<Dfds />} />
            <Route path="/importar" element={<Importar />} />
            <Route path="/validar-lote" element={<ValidarLote />} />
            <Route path="/historico" element={<Historico />} />
            <Route path="/logs" element={<Logs />} />
            <Route path="/usuarios" element={<Usuarios />} />
            <Route path="/configuracoes" element={<Configuracoes />} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Route>
        </Routes>
      </ProvedorAuth>
    </BrowserRouter>
  )
}

function ExigeAuth({ children }: { children: React.ReactNode }) {
  const { usuario, carregando } = useAuth()
  const location = useLocation()

  if (carregando) {
    return (
      <div className="min-h-screen grid place-items-center bg-background">
        <Loader2 className="size-5 animate-spin text-muted-foreground" />
      </div>
    )
  }

  if (!usuario) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  return <>{children}</>
}
