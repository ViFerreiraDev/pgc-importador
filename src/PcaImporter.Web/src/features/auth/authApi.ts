import type { Papel, Usuario } from './tipos'

async function tratar<T>(r: Response): Promise<T> {
  const data = await r.json().catch(() => ({}))
  if (!r.ok) {
    throw new Error((data && (data.erro || data.title)) ?? `HTTP ${r.status}`)
  }
  return data as T
}

export const authApi = {
  async me(): Promise<Usuario | null> {
    const r = await fetch('/api/auth/me', { credentials: 'include' })
    if (r.status === 401) return null
    return tratar<Usuario>(r)
  },
  async login(login: string, senha: string): Promise<Usuario> {
    const r = await fetch('/api/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify({ login, senha }),
    })
    return tratar<Usuario>(r)
  },
  async logout(): Promise<void> {
    await fetch('/api/auth/logout', { method: 'POST', credentials: 'include' })
  },
  async listarUsuarios(): Promise<Usuario[]> {
    const r = await fetch('/api/usuarios', { credentials: 'include' })
    return tratar<Usuario[]>(r)
  },
  async criarUsuario(input: { login: string; nome: string; senha: string; papel: Papel }): Promise<Usuario> {
    const r = await fetch('/api/usuarios', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify(input),
    })
    return tratar<Usuario>(r)
  },
  async removerUsuario(id: number): Promise<void> {
    const r = await fetch(`/api/usuarios/${id}`, { method: 'DELETE', credentials: 'include' })
    if (!r.ok && r.status !== 204) {
      const data = await r.json().catch(() => ({}))
      throw new Error((data && (data.erro || data.title)) ?? `HTTP ${r.status}`)
    }
  },
  async alterarSenha(id: number, novaSenha: string): Promise<void> {
    const r = await fetch(`/api/usuarios/${id}/senha`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify({ senhaAtual: '', novaSenha }),
    })
    if (!r.ok && r.status !== 204) {
      const data = await r.json().catch(() => ({}))
      throw new Error((data && (data.erro || data.title)) ?? `HTTP ${r.status}`)
    }
  },
}
