export type EstadoImportacao = 'Pendente' | 'Executando' | 'Concluida' | 'Falhou' | 'Cancelada'

const ESTADO_POR_NUMERO: Record<number, EstadoImportacao> = {
  0: 'Pendente',
  1: 'Executando',
  2: 'Concluida',
  3: 'Falhou',
  4: 'Cancelada',
}

export interface ErroValidacao {
  local: string
  campo: string
  mensagem: string
  linha: number | null
}

export interface AvisoValidacao {
  local: string
  mensagem: string
  linha: number | null
}

export interface DivergenciaValidacao {
  local: string
  linha: number | null
  codigo: string
  /** "preco" | "qtd" */
  tipo: 'preco' | 'qtd'
  valorPlanilha: number
  /** Limite inferior aceito (min × 0.5) */
  referenciaMin: number
  /** Limite superior aceito (max × 1.5) */
  referenciaMax: number
  /** Sinalizado: positivo se acima do máximo, negativo se abaixo do mínimo */
  diferencaPct: number
  siglaReferencia: string
  totalRegistros: number
  mensagem: string
}

export interface MetricasImportacao {
  totalPlanilhas: number
  totalItens: number
  totalValor: number
}

export interface HistoricoImportacao {
  id: number
  idPlanilha: string
  urlOriginal: string
  importadaEm: string
  idExecucao: string
  numeroDfd: number
  anoDfd: number
  idArtefato: number
  idFormalizacaoDemanda: number
  totalMateriais: number
  valorTotal: number
  sucesso: boolean
  mensagemErro: string | null
  linhaErro: number | null
  descricao: string | null
  usuarioLogin: string | null
}

export interface ResultadoValidacao {
  valido: boolean
  totalMateriais: number
  erros: ErroValidacao[]
  avisos: AvisoValidacao[]
  divergencias?: DivergenciaValidacao[]
  duplicado?: boolean
  anterior?: HistoricoImportacao | null
}

export interface EventoImportacao {
  ocorridoEm: string
  tipo: string
  mensagem: string
  detalhe: string | null
}

export interface StatusImportacao {
  id: string
  estado: EstadoImportacao
  iniciadaEm: string
  concluidaEm: string | null
  totalEtapas: number
  etapasConcluidas: number
  etapaAtual: string | null
  idArtefatoCriado: number | null
  idFormalizacaoCriado: number | null
  numeroDfd: number | null
  anoDfd: number | null
  totalMateriais: number
  materiaisAdicionados: number
  ultimoErro: string | null
  eventos: EventoImportacao[]
}

interface StatusBruto extends Omit<StatusImportacao, 'estado'> {
  estado: number | EstadoImportacao
}

export function normalizarStatusImp(b: StatusBruto): StatusImportacao {
  const estado = typeof b.estado === 'number' ? ESTADO_POR_NUMERO[b.estado] ?? 'Pendente' : b.estado
  return { ...b, estado }
}
