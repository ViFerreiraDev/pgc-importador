import { Check } from 'lucide-react'
import { ETAPAS, type IdEtapa } from './tipos'
import { cn } from '@/lib/utils'

interface Props {
  ativa: IdEtapa
  concluidas: Set<IdEtapa>
  onIr: (id: IdEtapa) => void
}

export function Stepper({ ativa, concluidas, onIr }: Props) {
  const indiceAtiva = ETAPAS.findIndex((e) => e.id === ativa)

  return (
    <ol className="flex items-stretch gap-0 rounded-lg border border-border overflow-hidden bg-card">
      {ETAPAS.map((etapa, idx) => {
        const isAtiva = etapa.id === ativa
        const isConcluida = concluidas.has(etapa.id)
        const isAcessivel = idx <= indiceAtiva || isConcluida
        const numero = idx + 1

        return (
          <li
            key={etapa.id}
            className={cn(
              'flex-1 min-w-0 relative',
              idx < ETAPAS.length - 1 && 'border-r border-border',
            )}
          >
            <button
              type="button"
              onClick={() => isAcessivel && onIr(etapa.id)}
              disabled={!isAcessivel}
              className={cn(
                'w-full text-left px-4 py-3 flex items-center gap-3 transition-colors disabled:cursor-not-allowed',
                isAtiva && 'bg-primary/5',
                !isAtiva && isAcessivel && 'hover:bg-accent',
              )}
            >
              <span
                className={cn(
                  'flex size-8 items-center justify-center rounded-full text-sm font-semibold shrink-0 transition-colors',
                  isAtiva && 'bg-primary text-primary-foreground',
                  !isAtiva && isConcluida && 'bg-success text-success-foreground',
                  !isAtiva && !isConcluida && 'border border-border text-muted-foreground',
                )}
              >
                {isConcluida && !isAtiva ? <Check className="size-4" /> : numero}
              </span>
              <div className="min-w-0">
                <div
                  className={cn(
                    'text-sm font-medium truncate',
                    isAtiva ? 'text-foreground' : isAcessivel ? 'text-foreground' : 'text-muted-foreground',
                  )}
                >
                  {etapa.titulo}
                </div>
                <div className="text-xs text-muted-foreground truncate">{etapa.descricao}</div>
              </div>
            </button>
          </li>
        )
      })}
    </ol>
  )
}
