import type { NivelPrioridade } from '../tipos'

export interface InformacoesGerais {
  dataConclusaoContratacao: string
  descricao: string
  nivelPrioridade: NivelPrioridade
  justificativaPrioridade: string
}

export const INFO_GERAIS_VAZIO: InformacoesGerais = {
  dataConclusaoContratacao: '',
  descricao: '',
  nivelPrioridade: 'BAIXO',
  justificativaPrioridade: '',
}

export type IdEtapa = 'informacoes' | 'justificativa' | 'materiais' | 'responsaveis'

export const ETAPAS: Array<{ id: IdEtapa; titulo: string; descricao: string }> = [
  { id: 'informacoes', titulo: 'Informações gerais', descricao: 'Dados básicos do DFD' },
  { id: 'justificativa', titulo: 'Justificativa', descricao: 'Motivação da contratação' },
  { id: 'materiais', titulo: 'Materiais / Serviços', descricao: 'Itens do PGC' },
  { id: 'responsaveis', titulo: 'Responsáveis', descricao: 'Quem assina o DFD' },
]
