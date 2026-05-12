import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react'
import { authApi } from './authApi'
import type { Usuario } from './tipos'

interface AuthValue {
  usuario: Usuario | null
  carregando: boolean
  erro: string | null
  ehAdmin: boolean
  login(loginId: string, senha: string): Promise<void>
  logout(): Promise<void>
  recarregar(): Promise<void>
}

const Ctx = createContext<AuthValue | null>(null)

export function ProvedorAuth({ children }: { children: ReactNode }) {
  const [usuario, setUsuario] = useState<Usuario | null>(null)
  const [carregando, setCarregando] = useState(true)
  const [erro, setErro] = useState<string | null>(null)

  const recarregar = useCallback(async () => {
    try {
      setErro(null)
      const u = await authApi.me()
      setUsuario(u)
    } catch (e) {
      setUsuario(null)
      setErro(e instanceof Error ? e.message : String(e))
    } finally {
      setCarregando(false)
    }
  }, [])

  useEffect(() => {
    void recarregar()
  }, [recarregar])

  const login = useCallback(async (loginId: string, senha: string) => {
    setErro(null)
    const u = await authApi.login(loginId, senha)
    setUsuario(u)
  }, [])

  const logout = useCallback(async () => {
    await authApi.logout()
    setUsuario(null)
  }, [])

  const valor: AuthValue = {
    usuario,
    carregando,
    erro,
    ehAdmin: usuario?.papel === 'Admin',
    login,
    logout,
    recarregar,
  }

  return <Ctx.Provider value={valor}>{children}</Ctx.Provider>
}

export function useAuth(): AuthValue {
  const c = useContext(Ctx)
  if (!c) throw new Error('useAuth fora do ProvedorAuth')
  return c
}
