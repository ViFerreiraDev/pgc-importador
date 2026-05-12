import { apiClient } from '../../api/client'
import { normalizarPaginaLogs, type PaginaLogs, type NivelLog } from './tipos'

interface FiltroLogs {
  pagina?: number
  tamanhoPagina?: number
  nivel?: NivelLog
  categoria?: string
}

const NIVEL_PARA_INT: Record<NivelLog, number> = {
  Info: 0,
  Sucesso: 1,
  Aviso: 2,
  Erro: 3,
}

export const logsApi = {
  listar: async (filtro: FiltroLogs = {}): Promise<PaginaLogs> => {
    const params = new URLSearchParams()
    params.set('pagina', String(filtro.pagina ?? 1))
    params.set('tamanhoPagina', String(filtro.tamanhoPagina ?? 50))
    if (filtro.nivel) params.set('nivel', String(NIVEL_PARA_INT[filtro.nivel]))
    if (filtro.categoria) params.set('categoria', filtro.categoria)
    const data = await apiClient.get<never>(`/api/logs?${params.toString()}`)
    return normalizarPaginaLogs(data as never)
  },
}
