import { useEffect, useState } from 'react'
import { CheckCircle2, AlertTriangle, XCircle, KeyRound, RefreshCw, Trash2, ShieldOff } from 'lucide-react'
import { Link } from 'react-router-dom'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { useToken } from '@/features/token/TokenContext'
import { cn, formatarDuracao, mascararCpf } from '@/lib/utils'
import type { EstadoToken } from '@/features/token/tipos'

const APARENCIA: Record<EstadoToken, { chip: string; ponto: string; rotulo: string; pulse?: boolean }> = {
  Ausente: {
    chip: 'text-muted-foreground bg-surface border-border',
    ponto: 'bg-[hsl(var(--neutral-400))]',
    rotulo: 'Sem sessão',
  },
  Saudavel: {
    chip: 'text-[hsl(var(--success-700))] bg-[hsl(var(--success-50))] border-[hsl(var(--success-100))]',
    ponto: 'bg-[hsl(var(--success-500))] pulse-dot',
    rotulo: 'Sessão ativa',
    pulse: true,
  },
  ProximoExpirar: {
    chip: 'text-[hsl(var(--warning-700))] bg-[hsl(var(--warning-50))] border-[hsl(var(--warning-100))]',
    ponto: 'bg-[hsl(var(--warning-500))]',
    rotulo: 'Renovando',
  },
  Expirado: {
    chip: 'text-[hsl(var(--error-700))] bg-[hsl(var(--error-50))] border-[hsl(var(--error-100))]',
    ponto: 'bg-[hsl(var(--error-500))]',
    rotulo: 'Sessão expirada',
  },
  RefreshFalhou: {
    chip: 'text-[hsl(var(--error-700))] bg-[hsl(var(--error-50))] border-[hsl(var(--error-100))]',
    ponto: 'bg-[hsl(var(--error-500))]',
    rotulo: 'Renovação falhou',
  },
}

interface Props {
  onSubstituir: () => void
}

export function StatusToken({ onSubstituir }: Props) {
  const { status, refresh, limpar } = useToken()
  const [, setTick] = useState(0)
  const [refreshando, setRefreshando] = useState(false)

  useEffect(() => {
    const id = setInterval(() => setTick((n) => n + 1), 1000)
    return () => clearInterval(id)
  }, [])

  const estado = status?.estado ?? 'Ausente'
  const aparencia = APARENCIA[estado]
  const restante = calcular(status?.expiraEm)

  return (
    <DropdownMenu>
      <DropdownMenuTrigger className={cn(
        'inline-flex items-center gap-2 px-2.5 h-[30px] rounded-full border text-[11px] font-semibold uppercase tracking-[0.04em] transition-colors',
        aparencia.chip,
      )}>
        <span className={cn('size-[7px] rounded-full shrink-0', aparencia.ponto)} />
        <span>{aparencia.rotulo}</span>
        {restante !== null && estado !== 'Ausente' && estado !== 'Expirado' && estado !== 'RefreshFalhou' && (
          <span className="font-mono text-[11px] tabular-nums normal-case tracking-normal opacity-70 border-l border-current/20 pl-2 ml-0.5">renova em {formatarDuracao(restante)}</span>
        )}
      </DropdownMenuTrigger>

      <DropdownMenuContent align="end" className="min-w-72">
        <DropdownMenuLabel>Sessão Compras.gov.br</DropdownMenuLabel>
        <div className="px-2 pb-2 space-y-2">
          <Linha rotulo="Estado" valor={aparencia.rotulo} />
          {status?.sub && <Linha rotulo="CPF" valor={mascararCpf(status.sub)} />}
          {status?.numeroUasg && <Linha rotulo="UASG" valor={status.numeroUasg.toString()} />}
          {status?.idSessao && <Linha rotulo="Sessão" valor={status.idSessao.toString()} />}
          {restante !== null && <Linha rotulo="Restante" valor={formatarDuracao(restante)} />}
          {status?.ultimoErroRefresh && (
            <div className="rounded-md bg-destructive/5 border border-destructive/20 px-2 py-1.5 text-[11px] text-destructive">
              {status.ultimoErroRefresh}
            </div>
          )}
        </div>

        <DropdownMenuSeparator />

        <DropdownMenuItem onSelect={onSubstituir}>
          <KeyRound className="text-muted-foreground" />
          {status && status.estado !== 'Ausente' ? 'Substituir token' : 'Colar token'}
        </DropdownMenuItem>

        {status && status.estado !== 'Ausente' && (
          <DropdownMenuItem
            disabled={refreshando}
            onSelect={async (e) => {
              e.preventDefault()
              setRefreshando(true)
              try { await refresh() } finally { setRefreshando(false) }
            }}
          >
            <RefreshCw className={cn('text-muted-foreground', refreshando && 'animate-spin')} />
            {refreshando ? 'Renovando...' : 'Forçar renovação'}
          </DropdownMenuItem>
        )}

        <DropdownMenuItem asChild>
          <Link to="/configuracoes">
            <ShieldOff className="text-muted-foreground" />
            Configurações de sessão
          </Link>
        </DropdownMenuItem>

        {status && status.estado !== 'Ausente' && (
          <>
            <DropdownMenuSeparator />
            <DropdownMenuItem onSelect={() => void limpar()} className="text-destructive focus:text-destructive">
              <Trash2 />
              Sair (limpar tokens)
            </DropdownMenuItem>
          </>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

function Linha({ rotulo, valor }: { rotulo: string; valor: string }) {
  return (
    <div className="flex justify-between items-baseline gap-3 text-xs">
      <span className="text-muted-foreground">{rotulo}</span>
      <span className="font-mono tabular-nums">{valor}</span>
    </div>
  )
}

function calcular(expiraEm: string | null | undefined): number | null {
  if (!expiraEm) return null
  const ms = new Date(expiraEm).getTime() - Date.now()
  return Math.max(0, Math.floor(ms / 1000))
}

export function IconeEstado({ estado, className }: { estado: EstadoToken; className?: string }) {
  if (estado === 'Saudavel') return <CheckCircle2 className={cn('text-emerald-500', className)} />
  if (estado === 'ProximoExpirar') return <AlertTriangle className={cn('text-amber-500', className)} />
  if (estado === 'Expirado' || estado === 'RefreshFalhou') return <XCircle className={cn('text-red-500', className)} />
  return <ShieldOff className={cn('text-muted-foreground', className)} />
}
