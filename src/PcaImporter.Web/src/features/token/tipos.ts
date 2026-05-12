export type EstadoToken = 'Ausente' | 'Saudavel' | 'ProximoExpirar' | 'Expirado' | 'RefreshFalhou'

const ESTADO_POR_NUMERO: Record<number, EstadoToken> = {
  0: 'Ausente',
  1: 'Saudavel',
  2: 'ProximoExpirar',
  3: 'Expirado',
  4: 'RefreshFalhou',
}

export interface StatusToken {
  estado: EstadoToken
  emitidoEm: string | null
  expiraEm: string | null
  segundosRestantes: number | null
  sub: string | null
  idSessao: number | null
  numeroUasg: number | null
  mnemonicos: string[] | null
  ultimoRefreshEm: string | null
  ultimoErroRefresh: string | null
  temRefreshToken: boolean
}

interface StatusBruto {
  estado: number | EstadoToken
  emitidoEm: string | null
  expiraEm: string | null
  segundosRestantes: number | null
  sub: string | null
  idSessao: number | null
  numeroUasg: number | null
  mnemonicos: string[] | null
  ultimoRefreshEm: string | null
  ultimoErroRefresh: string | null
  temRefreshToken: boolean
}

export function normalizarStatus(bruto: StatusBruto): StatusToken {
  const estado = typeof bruto.estado === 'number' ? ESTADO_POR_NUMERO[bruto.estado] ?? 'Ausente' : bruto.estado
  return { ...bruto, estado }
}
