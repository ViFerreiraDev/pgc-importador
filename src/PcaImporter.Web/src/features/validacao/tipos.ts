export type EstadoLink = 'pendente' | 'validando' | 'valido' | 'invalido' | 'erro'

export interface Revisor {
  login: string
  revisadoEm: string
}

export interface ItemValidacao {
  id: number
  tipo: 'erro' | 'divergencia'
  fingerprint: string
  local: string | null
  campo: string | null
  linha: number | null
  codigo: string | null
  deltaPct: number | null
  mensagem: string
  payloadJson: string
  criadoEm: string
  revisores: Revisor[]
}

export interface AvisoLista {
  local: string
  linha: number | null
  mensagem: string
}

export interface LinkValidacao {
  id: number
  rotulo: string | null
  url: string
  idPlanilha: string
  classe: string | null
  numeroGrupo: number | null
  estado: EstadoLink
  totalMateriais: number | null
  mensagemErro: string | null
  validadoEm: string | null
  criadoEm: string
  criadoPorLogin: string | null
  excluidoEm: string | null
  excluidoPorLogin: string | null
  descricao: string | null
  importadoEm: string | null
  ultimoIdExecucao: string | null
  ultimoErroImportacao: string | null
  erros: ItemValidacao[]
  divergencias: ItemValidacao[]
  avisos: AvisoLista[]
}

export interface GapClasse {
  classe: string
  ultimoNumero: number | null
  faltantes: number[]
}

export interface ListaValidacao {
  ativos: LinkValidacao[]
  lixeira: LinkValidacao[]
  gaps: GapClasse[]
}

/** Classes pré-definidas. UI guia, mas backend aceita string livre. */
export const CLASSES_PADRAO = ['INSUMOS', 'SANEANTES', 'MEDICAMENTOS'] as const
export type ClasseLink = (typeof CLASSES_PADRAO)[number]

export interface DuplicadoLote {
  linkExistenteId: number
  idPlanilha: string
  rotuloColado: string | null
  rotuloExistente: string | null
  url: string
}

export interface AusenteLote {
  linkId: number
  idPlanilha: string
  rotulo: string | null
  url: string
  estado: EstadoLink
}

export interface DiffLote {
  adicionados: LinkValidacao[]
  duplicados: DuplicadoLote[]
  ausentes: AusenteLote[]
}

/** Payload decodificado a partir de ItemValidacao.payloadJson, dependendo do tipo. */
export interface PayloadDivergencia {
  mensagem: string
  tipo: 'preco' | 'qtd'
  codigo: string
  valorPlanilha: number
  referenciaMin: number
  referenciaMax: number
  diferencaPct: number
  siglaReferencia: string
  totalRegistros: number
  linha: number | null
}

export interface PayloadErro {
  mensagem: string
  local: string
  campo: string
  linha: number | null
}

export function decodificarPayload<T = PayloadDivergencia | PayloadErro>(item: ItemValidacao): T | null {
  try { return JSON.parse(item.payloadJson) as T } catch { return null }
}
