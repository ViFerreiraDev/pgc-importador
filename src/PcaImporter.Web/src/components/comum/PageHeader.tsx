import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

interface Props {
  /** Não usado — mantido por compatibilidade com chamadas antigas */
  kicker?: string
  /** Idem */
  destaque?: string
  titulo: string
  descricao?: string
  acoes?: ReactNode
  className?: string
}

export function PageHeader({ titulo, descricao, acoes, className }: Props) {
  return (
    <header className={cn('flex items-end justify-between gap-4 mb-6 flex-wrap', className)}>
      <div>
        <h1 className="text-[26px] leading-[1.15] font-semibold tracking-[-0.02em] mb-1.5">{titulo}</h1>
        {descricao && (
          <p className="text-[14px] leading-relaxed text-muted-foreground max-w-[640px]">{descricao}</p>
        )}
      </div>
      {acoes && <div className="flex items-center gap-2 shrink-0">{acoes}</div>}
    </header>
  )
}

/** Pequena etiqueta de seção em uppercase (cinza) — usada acima de grupos de KPIs */
export function SectionTitle({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <div className={cn(
      'text-[11px] uppercase tracking-[0.08em] font-semibold text-muted-foreground mt-7 mb-3',
      className,
    )}>
      {children}
    </div>
  )
}
