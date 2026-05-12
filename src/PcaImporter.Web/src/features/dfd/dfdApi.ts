import { apiClient } from '../../api/client'
import type {
  AdicionarResponsavel,
  AtualizarInformacoesGerais,
  DfdAtual,
  DfdCriado,
  InformacoesGeraisAtualizadas,
  JustificativaAtualizada,
  MaterialServicoCriado,
  MaterialServicoEntrada,
  ResponsavelCriado,
} from './tipos'

export const dfdApi = {
  obterAtual: () => apiClient.get<DfdAtual>('/api/dfd/atual'),
  criar: () => apiClient.post<DfdCriado>('/api/dfd'),
  limpar: () => apiClient.delete<null>('/api/dfd/atual'),
  adicionarMaterial: (entrada: MaterialServicoEntrada) =>
    apiClient.post<MaterialServicoCriado>('/api/dfd/atual/material', entrada),
  atualizarInformacoesGerais: (entrada: AtualizarInformacoesGerais) =>
    apiClient.put<InformacoesGeraisAtualizadas>('/api/dfd/atual/informacoes-gerais', entrada),
  atualizarJustificativa: (texto: string) =>
    apiClient.put<JustificativaAtualizada>('/api/dfd/atual/justificativa', { texto }),
  adicionarResponsavel: (entrada: AdicionarResponsavel) =>
    apiClient.post<ResponsavelCriado>('/api/dfd/atual/responsavel', entrada),
}
