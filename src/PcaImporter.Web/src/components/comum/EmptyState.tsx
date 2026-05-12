import type { ReactNode } from 'react'
import type { LucideIcon } from 'lucide-react'
import { cn } from '@/lib/utils'

interface Props {
  icon?: LucideIcon
  titulo: string
  descricao?: string
  acao?: ReactNode
  className?: string
}

export function EmptyState({ icon: Icon, titulo, descricao, acao, className }: Props) {
  return (
    <div className={cn('flex flex-col items-center justify-center text-center gap-3.5 py-16 px-5', className)}>
      {Icon && (
        <div className="grid place-items-center size-[88px] rounded-full bg-[hsl(var(--brand-50))] text-[hsl(var(--brand-500))]">
          <Icon className="size-[38px]" strokeWidth={1.5} />
        </div>
      )}
      <h2 className="text-[18px] font-semibold m-0">{titulo}</h2>
      {descricao && <p className="text-[13px] text-muted-foreground max-w-[380px] leading-relaxed m-0">{descricao}</p>}
      {acao && <div className="mt-1">{acao}</div>}
    </div>
  )
}
