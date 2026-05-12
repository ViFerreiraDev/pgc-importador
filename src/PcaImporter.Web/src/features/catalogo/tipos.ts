export type EstadoSincronizacao = 'Ocioso' | 'Executando' | 'Concluida' | 'Falhou' | 'Cancelada'

const ESTADO_POR_NUMERO: Record<number, EstadoSincronizacao> = {
  0: 'Ocioso',
  1: 'Executando',
  2: 'Concluida',
  3: 'Falhou',
  4: 'Cancelada',
}

export interface StatusSincronizacaoCatalogo {
  estado: EstadoSincronizacao
  iniciadaEm: string | null
  concluidaEm: string | null
  paginaAtual: number
  totalPaginas: number
  itensProcessados: number
  totalRegistros: number
  totalArmazenado: number
  ultimaSincronizacao: string | null
  ultimoErro: string | null
}

interface StatusBruto extends Omit<StatusSincronizacaoCatalogo, 'estado'> {
  estado: EstadoSincronizacao | number
}

export function normalizarStatusCatalogo(b: StatusBruto): StatusSincronizacaoCatalogo {
  const estado = typeof b.estado === 'number' ? ESTADO_POR_NUMERO[b.estado] ?? 'Ocioso' : b.estado
  return { ...b, estado }
}

export interface ItemCatalogo {
  codigoItem: number
  codigoGrupo: number
  nomeGrupo: string
  codigoClasse: number
  nomeClasse: string
  codigoPdm: number
  nomePdm: string
  descricaoItem: string
  statusItem: boolean
  itemSustentavel: boolean
  tipo: 'MATERIAL' | 'SERVICO'
  dataHoraAtualizacao: string | null
  corpoBruto: string
}
