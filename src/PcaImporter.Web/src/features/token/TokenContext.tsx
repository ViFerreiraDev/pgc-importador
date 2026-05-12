import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { tokenApi } from './tokenApi'
import { TokenHubClient } from './tokenHub'
import type { StatusToken } from './tipos'

interface TokenContextValor {
  status: StatusToken | null
  carregando: boolean
  erro: string | null
  definir: (refreshToken: string) => Promise<void>
  refresh: () => Promise<void>
  limpar: () => Promise<void>
  recarregar: () => Promise<void>
}

const TokenContext = createContext<TokenContextValor | null>(null)

export function ProvedorToken({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<StatusToken | null>(null)
  const [carregando, setCarregando] = useState(true)
  const [erro, setErro] = useState<string | null>(null)
  const hubRef = useRef<TokenHubClient | null>(null)

  const recarregar = useCallback(async () => {
    try {
      const s = await tokenApi.status()
      setStatus(s)
      setErro(null)
    } catch (e) {
      setErro(e instanceof Error ? e.message : String(e))
    } finally {
      setCarregando(false)
    }
  }, [])

  useEffect(() => {
    void recarregar()
    const hub = new TokenHubClient()
    hubRef.current = hub
    const desinscrever = hub.onEstadoMudou((s) => setStatus(s))
    void hub.iniciar().catch((e) => console.error('SignalR start falhou', e))
    return () => {
      desinscrever()
      void hub.parar()
    }
  }, [recarregar])

  const definir = useCallback(async (refreshToken: string) => {
    const s = await tokenApi.definir(refreshToken)
    setStatus(s)
    setErro(null)
  }, [])

  const refresh = useCallback(async () => {
    const s = await tokenApi.refresh()
    setStatus(s)
  }, [])

  const limpar = useCallback(async () => {
    const s = await tokenApi.limpar()
    setStatus(s)
  }, [])

  const valor = useMemo<TokenContextValor>(
    () => ({ status, carregando, erro, definir, refresh, limpar, recarregar }),
    [status, carregando, erro, definir, refresh, limpar, recarregar],
  )

  return <TokenContext.Provider value={valor}>{children}</TokenContext.Provider>
}

export function useToken(): TokenContextValor {
  const ctx = useContext(TokenContext)
  if (!ctx) throw new Error('useToken precisa estar dentro de ProvedorToken')
  return ctx
}
