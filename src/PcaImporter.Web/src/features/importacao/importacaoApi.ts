import { normalizarStatusImp, type HistoricoImportacao, type MetricasImportacao, type ResultadoValidacao, type StatusImportacao } from './tipos'

export interface DuplicadoErro {
  duplicado: true
  erro: string
  anterior: HistoricoImportacao
}

export class ImportacaoDuplicadaError extends Error {
  anterior: HistoricoImportacao
  constructor(mensagem: string, anterior: HistoricoImportacao) {
    super(mensagem)
    this.anterior = anterior
  }
}

async function tratarResposta<T>(r: Response): Promise<T> {
  const data = await r.json()
  if (r.status === 409 && data?.duplicado === true) {
    throw new ImportacaoDuplicadaError(data.erro ?? 'Planilha já importada', data.anterior)
  }
  if (!r.ok) {
    throw new Error(data?.erro ?? data?.title ?? `HTTP ${r.status}`)
  }
  return data as T
}

export const importacaoApi = {
  async validar(arquivo: File): Promise<ResultadoValidacao> {
    const fd = new FormData()
    fd.append('arquivo', arquivo)
    const r = await fetch('/api/importacao/validar', { method: 'POST', body: fd })
    return tratarResposta<ResultadoValidacao>(r)
  },
  async iniciar(arquivo: File): Promise<{ id: string }> {
    const fd = new FormData()
    fd.append('arquivo', arquivo)
    const r = await fetch('/api/importacao/iniciar', { method: 'POST', body: fd })
    return tratarResposta<{ id: string }>(r)
  },
  async validarLink(url: string): Promise<ResultadoValidacao> {
    const r = await fetch('/api/importacao/validar-link', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ url }),
    })
    return tratarResposta<ResultadoValidacao>(r)
  },
  async iniciarLink(url: string, confirmarDuplicado = false): Promise<{ id: string }> {
    const r = await fetch('/api/importacao/iniciar-link', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ url, confirmarDuplicado }),
    })
    return tratarResposta<{ id: string }>(r)
  },
  async metricas(): Promise<MetricasImportacao> {
    const r = await fetch('/api/importacao/metricas')
    return tratarResposta<MetricasImportacao>(r)
  },
  async historico(limite = 200): Promise<HistoricoImportacao[]> {
    const r = await fetch(`/api/importacao/historico?limite=${limite}`)
    return tratarResposta<HistoricoImportacao[]>(r)
  },
  async resetarMetricas(): Promise<{ removidos: number }> {
    const r = await fetch('/api/importacao/metricas', { method: 'DELETE' })
    return tratarResposta<{ removidos: number }>(r)
  },
  async status(id: string): Promise<StatusImportacao> {
    const r = await fetch(`/api/importacao/${encodeURIComponent(id)}`)
    const data = await r.json()
    if (!r.ok) throw new Error((data && (data.erro || data.title)) ?? `HTTP ${r.status}`)
    return normalizarStatusImp(data as never)
  },
}
