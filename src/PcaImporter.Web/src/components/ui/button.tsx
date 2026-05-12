import * as React from 'react'
import { Slot } from '@radix-ui/react-slot'
import { cva, type VariantProps } from 'class-variance-authority'
import { cn } from '@/lib/utils'

const buttonVariants = cva(
  'inline-flex items-center justify-center gap-1.5 whitespace-nowrap rounded-md font-medium leading-none transition-colors duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-1 ring-offset-background disabled:opacity-50 disabled:cursor-not-allowed [&_svg]:size-3.5 [&_svg]:shrink-0',
  {
    variants: {
      variant: {
        default: 'bg-primary text-primary-foreground hover:bg-[hsl(var(--brand-600))]',
        primary: 'bg-primary text-primary-foreground hover:bg-[hsl(var(--brand-600))]',
        secondary: 'bg-surface text-foreground border border-border shadow-[0_1px_0_hsl(218_25%_8%/0.04)] hover:bg-[hsl(var(--neutral-100))]',
        ghost: 'bg-transparent text-muted-foreground hover:bg-[hsl(var(--neutral-100))] hover:text-foreground',
        outline: 'bg-surface text-foreground border border-border hover:bg-[hsl(var(--neutral-100))]',
        destructive: 'bg-destructive text-destructive-foreground hover:bg-[hsl(var(--error-600))]',
        'destructive-outline':
          'bg-surface text-[hsl(var(--error-600))] border border-[hsl(var(--error-100))] hover:bg-[hsl(var(--error-50))]',
        link: 'text-primary underline-offset-4 hover:underline',
      },
      size: {
        default: 'h-[34px] px-3 text-[13px]',
        sm: 'h-7 px-2.5 text-[12px]',
        xs: 'h-6 px-2 text-[11px] gap-1 [&_svg]:size-3',
        lg: 'h-[38px] px-4 text-[14px]',
        icon: 'h-[34px] w-[34px] p-0',
      },
    },
    defaultVariants: {
      variant: 'default',
      size: 'default',
    },
  },
)

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>,
    VariantProps<typeof buttonVariants> {
  asChild?: boolean
}

export const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant, size, asChild = false, ...props }, ref) => {
    const Comp = asChild ? Slot : 'button'
    return <Comp ref={ref} className={cn(buttonVariants({ variant, size, className }))} {...props} />
  },
)
Button.displayName = 'Button'

export { buttonVariants }
