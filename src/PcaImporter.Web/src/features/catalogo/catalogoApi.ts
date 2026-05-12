import { apiClient } from '../../api/client'
import { normalizarStatusCatalogo, type ItemCatalogo, type StatusSincronizacaoCatalogo } from './tipos'

export const catalogoApi = {
  consultarMaterial: (codigo: string) => apiClient.get<ItemCatalogo>(`/api/catalogo/material/${encodeURIComponent(codigo)}`),
  consultarServico: (codigo: string) => apiClient.get<ItemCatalogo>(`/api/catalogo/servico/${encodeURIComponent(codigo)}`),
  async status(): Promise<StatusSincronizacaoCatalogo> {
    const data = await apiClient.get<unknown>('/api/catalogo/status')
    return normalizarStatusCatalogo(data as never)
  },
  sincronizar: () => apiClient.post<{ iniciado: boolean }>('/api/catalogo/sincronizar'),
  cancelar: () => apiClient.post<{ cancelado: boolean }>('/api/catalogo/cancelar'),
  limpar: () => apiClient.delete<{ removidos: number }>('/api/catalogo'),
}
