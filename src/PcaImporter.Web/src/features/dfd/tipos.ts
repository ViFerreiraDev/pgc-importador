export interface SecoesDfd {
  idSecaoPrincipal: number
  idItemInformacoesGerais: number | null
  idItemJustificativaNecessidade: number | null
  idItemMateriaisServicos: number | null
  idItemResponsaveis: number | null
  idItemAcompanhamento: number | null
}

export interface DfdCriado {
  idArtefato: number
  idFormalizacaoDemanda: number
  numero: number
  ano: number
  uasg: number
  nomeUasg: string
  status: string
  anoPca: number | null
  statusPca: string | null
  criadoEm: string
  secoes: SecoesDfd
  corpoBruto: string
}

export interface JustificativaAtualizada {
  id: number
  idSecao: number
  conteudo: string
  dataHoraOperacao: string
  corpoBruto: string
}

export interface MaterialServicoCriado {
  id: number
  idFormalizacaoDemanda: number
  tipo: string
  codigo: string
  idClasse: number
  nomeClasse: string
  idPadraoDescritivo: number
  nomePadraoDescritivo: string
  quantidade: number
  valorUnitario: number
  valorTotal: number
  moeda: string
  siglaUnidadeFornecimento: string
  nomeUnidadeFornecimento: string
  dataHoraOperacao: string
  loginOperacao: number
  corpoBruto: string
}

export interface DfdAtual {
  atual: DfdCriado | null
  itens: MaterialServicoCriado[]
  responsaveis: ResponsavelCriado[]
}

export interface AdicionarResponsavel {
  cpf: string
  nome: string
  email: string
  cargo: string
}

export interface ResponsavelCriado {
  id: number | null
  idArtefato: number
  cpf: string
  nome: string
  email: string
  idCargo: number
  cargo: string
  instrumento: string
  assinaDocumento: boolean
  ordem: number
  corpoBruto: string
}

export type NivelPrioridade = 'BAIXO' | 'MEDIO' | 'ALTO'

export interface AtualizarInformacoesGerais {
  dataConclusaoContratacao: string
  objeto: string
  nivelPrioridade: NivelPrioridade
  justificativaPrioridade?: string | null
}

export interface InformacoesGeraisAtualizadas {
  id: number
  idArtefato: number
  objeto: string | null
  dataPrevista: string | null
  nivelPrioridade: NivelPrioridade
  emergencial: boolean
  justificativaEmergencial: string | null
  valorTotalEstimado: number | null
  corpoBruto: string
}

export interface MaterialServicoEntrada {
  tipo: string
  codigo: string
  idClasse: number
  nomeClasse: string
  idPadraoDescritivo: number
  nomePadraoDescritivo: string
  descricao: string
  quantidade: number
  valorUnitario: number
  moeda: string
  siglaUnidadeFornecimento: string
}
