import { useEffect, useState } from 'react'
import { Outlet } from 'react-router-dom'
import { Sidebar } from './Sidebar'
import { Topbar } from './Topbar'
import { DialogColarToken } from '@/features/token/DialogColarToken'
import { DialogSessaoInativa } from '@/features/token/DialogSessaoInativa'
import { useToken } from '@/features/token/TokenContext'
import { useAuth } from '@/features/auth/AuthContext'

export function AppLayout() {
  const { status } = useToken()
  const { ehAdmin } = useAuth()
  const [abertoAdmin, setAbertoAdmin] = useState(false)

  const precisaToken = status?.estado === 'Ausente' || status?.estado === 'Expirado' || status?.estado === 'RefreshFalhou'

  // Admin: abre o diálogo de colar token automaticamente.
  // Usuário normal: vê um modal travado que só permite sair.
  useEffect(() => {
    if (precisaToken && ehAdmin) setAbertoAdmin(true)
  }, [precisaToken, ehAdmin])

  return (
    <div className="flex h-screen bg-background">
      <Sidebar />
      <div className="flex-1 flex flex-col min-w-0">
        <Topbar onAbrirDialogToken={() => setAbertoAdmin(true)} />
        <main className="flex-1 overflow-auto scrollbar-thin">
          <div className="px-8 py-7 pb-20 max-w-[1280px] mx-auto w-full">
            <Outlet />
          </div>
        </main>
      </div>

      {ehAdmin && (
        <DialogColarToken
          aberto={abertoAdmin}
          onFechar={() => setAbertoAdmin(false)}
          forcado={precisaToken}
        />
      )}

      {!ehAdmin && (
        <DialogSessaoInativa
          aberto={precisaToken}
          estado={status?.estado ?? 'Ausente'}
        />
      )}
    </div>
  )
}
