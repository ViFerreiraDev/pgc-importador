import type { ReactNode } from 'react'
import type { LucideIcon } from 'lucide-react'
import { cn } from '@/lib/utils'

interface Props {
  rotulo: string
  valor: string | number
  /** Tooltip nativo: valor exato quando o exibido é compacto */
  valorTitulo?: string
  descricao?: string | ReactNode
  icon?: LucideIcon
  cor?: 'default' | 'success' | 'warning' | 'destructive' | 'brand'
  className?: string
}

const acentoBorda: Record<NonNullable<Props['cor']>, string> = {
  default: 'border-border',
  brand: 'border-[hsl(var(--brand-100))] bg-gradient-to-b from-[hsl(var(--brand-25))] to-surface',
  success: 'border-[hsl(var(--success-100))]',
  warning: 'border-[hsl(var(--warning-100))] bg-gradient-to-b from-[hsl(var(--warning-50))] to-surface',
  destructive: 'border-[hsl(var(--error-100))] bg-gradient-to-b from-[hsl(var(--error-50))] to-surface',
}

/**
 * KPI no estilo design — label uppercase + valor mono grande + sub.
 */
export function StatCard({ rotulo, valor, valorTitulo, descricao, icon: Icon, cor = 'default', className }: Props) {
  return (
    <div
      className={cn(
        'rounded-[10px] border bg-surface p-4 px-[18px] flex flex-col gap-1.5',
        'shadow-[0_1px_0_hsl(218_25%_8%/0.04)] overflow-hidden relative',
        acentoBorda[cor],
        className,
      )}
    >
      <span className="flex items-center gap-1.5 text-[11px] uppercase tracking-[0.06em] font-semibold text-muted-foreground">
        {Icon && <Icon className="size-[13px]" />}
        {rotulo}
      </span>
      <span
        className={cn(
          'font-mono text-[26px] font-medium leading-[1.1] tracking-[-0.01em] text-foreground mt-0.5',
          valorTitulo && 'cursor-help',
        )}
        title={valorTitulo}
      >
        {valor}
      </span>
      {descricao && (
        <span className="text-[12px] text-muted-foreground flex items-center gap-1">{descricao}</span>
      )}
    </div>
  )
}
