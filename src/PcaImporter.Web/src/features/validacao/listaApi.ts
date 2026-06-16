import type { DiffLote, LinkValidacao, ListaValidacao, Revisor } from './tipos'

async function tratar<T>(r: Response): Promise<T> {
  const data = await r.json().catch(() => ({}))
  if (!r.ok) {
    throw new Error((data && (data.erro || data.title)) ?? `HTTP ${r.status}`)
  }
  return data as T
}

async function tratarSemBody(r: Response): Promise<void> {
  if (r.ok || r.status === 204) return
  const data = await r.json().catch(() => ({}))
  throw new Error((data && (data.erro || data.title)) ?? `HTTP ${r.status}`)
}

export const listaApi = {
  async obter(): Promise<ListaValidacao> {
    const r = await fetch('/api/lista-validacao', { credentials: 'include' })
    return tratar<ListaValidacao>(r)
  },
  async adicionarLink(url: string, opts?: { rotulo?: string; classe?: string; numeroGrupo?: number }): Promise<LinkValidacao> {
    const r = await fetch('/api/lista-validacao/links', {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        url,
        rotulo: opts?.rotulo ?? null,
        classe: opts?.classe ?? null,
        numeroGrupo: opts?.numeroGrupo ?? null,
      }),
    })
    return tratar<LinkValidacao>(r)
  },
  async extrairLinks(texto: string, classe?: string): Promise<LinkValidacao[]> {
    const r = await fetch('/api/lista-validacao/links/extrair', {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ texto, classe: classe ?? null, detectarNumeroGrupoNoTexto: true }),
    })
    return tratar<LinkValidacao[]>(r)
  },
  async compararLote(texto: string, classe?: string): Promise<DiffLote> {
    const r = await fetch('/api/lista-validacao/links/comparar', {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ texto, classe: classe ?? null, detectarNumeroGrupoNoTexto: true }),
    })
    return tratar<DiffLote>(r)
  },
  async validar(linkId: number): Promise<LinkValidacao> {
    const r = await fetch(`/api/lista-validacao/links/${linkId}/validar`, {
      method: 'POST',
      credentials: 'include',
    })
    return tratar<LinkValidacao>(r)
  },
  async excluir(linkId: number): Promise<void> {
    const r = await fetch(`/api/lista-validacao/links/${linkId}`, {
      method: 'DELETE',
      credentials: 'include',
    })
    return tratarSemBody(r)
  },
  async restaurar(linkId: number): Promise<void> {
    const r = await fetch(`/api/lista-validacao/links/${linkId}/restaurar`, {
      method: 'POST',
      credentials: 'include',
    })
    return tratarSemBody(r)
  },
  async apagarDefinitivamente(linkId: number): Promise<void> {
    const r = await fetch(`/api/lista-validacao/lixeira/${linkId}`, {
      method: 'DELETE',
      credentials: 'include',
    })
    return tratarSemBody(r)
  },
  async revisar(itemId: number): Promise<Revisor[]> {
    const r = await fetch(`/api/lista-validacao/itens/${itemId}/revisar`, {
      method: 'POST',
      credentials: 'include',
    })
    return tratar<Revisor[]>(r)
  },
  async desrevisar(itemId: number): Promise<Revisor[]> {
    const r = await fetch(`/api/lista-validacao/itens/${itemId}/revisar`, {
      method: 'DELETE',
      credentials: 'include',
    })
    return tratar<Revisor[]>(r)
  },
  async importar(linkId: number, confirmarDuplicado = false): Promise<{ id: string }> {
    const r = await fetch(`/api/lista-validacao/links/${linkId}/importar?confirmarDuplicado=${confirmarDuplicado}`, {
      method: 'POST',
      credentials: 'include',
    })
    return tratar<{ id: string }>(r)
  },
  async registrarResultadoImportacao(linkId: number, idExecucao: string, sucesso: boolean, mensagemErro?: string): Promise<LinkValidacao> {
    const r = await fetch(`/api/lista-validacao/links/${linkId}/registrar-resultado-importacao`, {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ idExecucao, sucesso, mensagemErro: mensagemErro ?? null }),
    })
    return tratar<LinkValidacao>(r)
  },
}
