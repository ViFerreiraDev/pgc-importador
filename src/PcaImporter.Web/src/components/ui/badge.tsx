import * as React from 'react'
import { cva, type VariantProps } from 'class-variance-authority'
import { cn } from '@/lib/utils'

const badgeVariants = cva(
  'inline-flex items-center gap-1 rounded-full px-2 py-[2px] text-[11px] font-semibold leading-[18px] tracking-[0.01em] whitespace-nowrap [&_svg]:size-3',
  {
    variants: {
      variant: {
        default: 'bg-[hsl(var(--brand-50))] text-[hsl(var(--brand-700))]',
        success: 'bg-[hsl(var(--success-50))] text-[hsl(var(--success-700))]',
        warning: 'bg-[hsl(var(--warning-50))] text-[hsl(var(--warning-700))]',
        destructive: 'bg-[hsl(var(--error-50))] text-[hsl(var(--error-700))]',
        info: 'bg-[hsl(var(--info-50))] text-[hsl(var(--info-500))]',
        brand: 'bg-[hsl(var(--brand-50))] text-[hsl(var(--brand-700))]',
        secondary: 'bg-[hsl(var(--neutral-50))] text-[hsl(var(--neutral-700))] border border-[hsl(var(--neutral-100))]',
        outline: 'bg-transparent text-muted-foreground border border-border font-medium',
        muted: 'bg-[hsl(var(--neutral-50))] text-[hsl(var(--neutral-700))] border border-[hsl(var(--neutral-100))]',
      },
      size: {
        default: '',
        upper: 'uppercase tracking-[0.04em] text-[10px] px-[7px]',
      },
    },
    defaultVariants: {
      variant: 'default',
      size: 'default',
    },
  },
)

export interface BadgeProps
  extends React.HTMLAttributes<HTMLSpanElement>,
    VariantProps<typeof badgeVariants> {
  /** Mostra um pontinho colorido antes do conteúdo */
  comDot?: boolean
}

const corDotPorVariant: Record<string, string> = {
  default: 'bg-[hsl(var(--brand-500))]',
  brand: 'bg-[hsl(var(--brand-500))]',
  success: 'bg-[hsl(var(--success-500))]',
  warning: 'bg-[hsl(var(--warning-500))]',
  destructive: 'bg-[hsl(var(--error-500))]',
  info: 'bg-[hsl(var(--info-500))]',
  secondary: 'bg-[hsl(var(--neutral-400))]',
  outline: 'bg-[hsl(var(--neutral-400))]',
  muted: 'bg-[hsl(var(--neutral-400))]',
}

export function Badge({ className, variant = 'default', size, comDot, children, ...props }: BadgeProps) {
  return (
    <span className={cn(badgeVariants({ variant, size, className }))} {...props}>
      {comDot && <span className={cn('size-1.5 rounded-full', corDotPorVariant[variant ?? 'default'])} />}
      {children}
    </span>
  )
}

export { badgeVariants }
