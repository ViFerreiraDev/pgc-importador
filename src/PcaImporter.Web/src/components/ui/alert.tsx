import * as React from 'react'
import { cva, type VariantProps } from 'class-variance-authority'
import { cn } from '@/lib/utils'

const alertVariants = cva(
  'flex gap-3 items-start rounded-lg border px-3.5 py-3 text-[13px] leading-[1.5] [&>svg]:size-4 [&>svg]:mt-0.5 [&>svg]:shrink-0',
  {
    variants: {
      variant: {
        default: 'bg-surface text-foreground border-border',
        destructive: 'bg-[hsl(var(--error-50))] text-[hsl(var(--error-700))] border-[hsl(var(--error-100))] [&>svg]:text-[hsl(var(--error-500))]',
        warning: 'bg-[hsl(var(--warning-50))] text-[hsl(var(--warning-700))] border-[hsl(var(--warning-100))] [&>svg]:text-[hsl(var(--warning-500))]',
        success: 'bg-[hsl(var(--success-50))] text-[hsl(var(--success-700))] border-[hsl(var(--success-100))] [&>svg]:text-[hsl(var(--success-500))]',
        info: 'bg-[hsl(var(--info-50))] text-[hsl(var(--info-700))] border-[hsl(var(--info-100))] [&>svg]:text-[hsl(var(--info-500))]',
      },
    },
    defaultVariants: {
      variant: 'default',
    },
  },
)

export interface AlertProps extends React.HTMLAttributes<HTMLDivElement>, VariantProps<typeof alertVariants> {}

export const Alert = React.forwardRef<HTMLDivElement, AlertProps>(({ className, variant, ...props }, ref) => (
  <div ref={ref} role="alert" className={cn(alertVariants({ variant }), className)} {...props} />
))
Alert.displayName = 'Alert'

export const AlertTitle = React.forwardRef<HTMLParagraphElement, React.HTMLAttributes<HTMLHeadingElement>>(
  ({ className, ...props }, ref) => (
    <h5 ref={ref} className={cn('font-semibold leading-tight mb-0.5', className)} {...props} />
  ),
)
AlertTitle.displayName = 'AlertTitle'

export const AlertDescription = React.forwardRef<HTMLParagraphElement, React.HTMLAttributes<HTMLParagraphElement>>(
  ({ className, ...props }, ref) => (
    <div ref={ref} className={cn('text-[13px] leading-[1.5] [&_p]:leading-relaxed', className)} {...props} />
  ),
)
AlertDescription.displayName = 'AlertDescription'
