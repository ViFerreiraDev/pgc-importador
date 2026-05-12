export type NivelLog = 'Info' | 'Sucesso' | 'Aviso' | 'Erro'

const NIVEL_POR_NUMERO: Record<number, NivelLog> = {
  0: 'Info',
  1: 'Sucesso',
  2: 'Aviso',
  3: 'Erro',
}

export interface LogEvento {
  id: number
  ocorridoEm: string
  nivel: NivelLog
  categoria: string
  mensagem: string
  detalhes: string | null
}

export interface PaginaLogs {
  itens: LogEvento[]
  pagina: number
  tamanhoPagina: number
  total: number
}

interface PaginaLogsBruta {
  itens: Array<Omit<LogEvento, 'nivel'> & { nivel: number | NivelLog }>
  pagina: number
  tamanhoPagina: number
  total: number
}

export function normalizarPaginaLogs(p: PaginaLogsBruta): PaginaLogs {
  return {
    pagina: p.pagina,
    tamanhoPagina: p.tamanhoPagina,
    total: p.total,
    itens: p.itens.map((i) => ({
      ...i,
      nivel: typeof i.nivel === 'number' ? NIVEL_POR_NUMERO[i.nivel] ?? 'Info' : i.nivel,
    })),
  }
}
