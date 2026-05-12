import { apiClient } from '../../api/client'
import { normalizarStatus, type StatusToken } from './tipos'

export const tokenApi = {
  status: async (): Promise<StatusToken> => normalizarStatus(await apiClient.get('/api/token/status')),
  definir: async (refreshToken: string): Promise<StatusToken> =>
    normalizarStatus(await apiClient.post('/api/token', { refreshToken })),
  refresh: async (): Promise<StatusToken> => normalizarStatus(await apiClient.post('/api/token/refresh')),
  limpar: async (): Promise<StatusToken> => normalizarStatus(await apiClient.delete('/api/token')),
}
