import { useEffect, useState } from 'react'
import { Outlet } from 'react-router-dom'
import { Sidebar } from './Sidebar'
import { Topbar } from './Topbar'
import { DialogColarToken } from '@/features/token/DialogColarToken'
import { useToken } from '@/features/token/TokenContext'

export function AppLayout() {
  const { status } = useToken()
  const [aberto, setAberto] = useState(false)

  const precisaToken = status?.estado === 'Ausente' || status?.estado === 'Expirado' || status?.estado === 'RefreshFalhou'

  useEffect(() => {
    if (precisaToken) setAberto(true)
  }, [precisaToken])

  return (
    <div className="flex h-screen bg-background">
      <Sidebar />
      <div className="flex-1 flex flex-col min-w-0">
        <Topbar onAbrirDialogToken={() => setAberto(true)} />
        <main className="flex-1 overflow-auto scrollbar-thin">
          <div className="px-8 py-7 pb-20 max-w-[1280px] mx-auto w-full">
            <Outlet />
          </div>
        </main>
      </div>

      <DialogColarToken
        aberto={aberto}
        onFechar={() => setAberto(false)}
        forcado={precisaToken}
      />
    </div>
  )
}
