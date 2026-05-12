import { cn } from '@/lib/utils'

interface Props {
  className?: string
}

/**
 * Marca textual "PGC SAÚDE.RIO · Importador" — formato wordmark institucional.
 * PGC = Sistema de Planejamento e Gerenciamento de Contratações.
 */
export function MarcaPCA({ className }: Props) {
  return (
    <div className={cn('flex items-center gap-2.5', className)}>
      <div className="size-8 rounded-md bg-[hsl(var(--brand-500))] grid place-items-center text-white font-bold text-[13px] tracking-tight shadow-[inset_0_-1px_0_hsl(var(--brand-700))]">
        PGC
      </div>
      <div className="leading-tight min-w-0">
        <div className="font-bold text-[13px] tracking-[-0.01em] text-[hsl(var(--brand-700))] flex items-baseline gap-1 whitespace-nowrap">
          <span>PGC</span>
          <span className="not-italic font-semibold text-[11.5px] tracking-[0.02em] text-[hsl(var(--accent-500))]">SAÚDE.RIO</span>
        </div>
        <div className="text-[10px] uppercase tracking-[0.06em] font-semibold text-muted-foreground">Importador</div>
      </div>
    </div>
  )
}

/** Logo institucional PCRJ-Saúde (URL oficial). */
export function LogoInstitucional({ className }: { className?: string }) {
  return (
    <img
      src="https://saude.prefeitura.rio/wp-content/uploads/sites/47/2025/02/Logo_PCRJ-Saude_HorizontalSUS_UmaCor-Preto.png"
      alt="Prefeitura do Rio · Secretaria Municipal de Saúde"
      className={cn('block h-auto w-auto', className)}
      loading="lazy"
    />
  )
}

// Compatibilidade: alias antigo
export function Logo(props: { className?: string }) {
  return <MarcaPCA className={props.className} />
}
