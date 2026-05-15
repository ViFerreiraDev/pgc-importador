import { cn } from '@/lib/utils'

interface Props {
  codigo: string | number
  className?: string
}

/**
 * Link para a página de análise interna do código BR (CATMAT/CATSER).
 * URL: https://siga.subgsms.rio/analise-codigo-br?codigoBR={codigo}
 */
export function LinkCodigoBr({ codigo, className }: Props) {
  return (
    <a
      href={`https://siga.subgsms.rio/analise-codigo-br?codigoBR=${encodeURIComponent(String(codigo))}`}
      target="_blank"
      rel="noreferrer"
      className={cn(
        'text-[hsl(var(--brand-700))] underline decoration-[hsl(var(--brand-300))] decoration-1 underline-offset-2',
        'hover:decoration-[hsl(var(--brand-600))] hover:text-[hsl(var(--brand-800))]',
        className,
      )}
      title="Abrir análise do código BR no SIGA"
    >
      {codigo}
    </a>
  )
}
