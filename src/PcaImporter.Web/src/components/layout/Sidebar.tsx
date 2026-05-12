import { NavLink } from 'react-router-dom'
import { LayoutDashboard, ScrollText, Settings, Upload, History, Users2, ListChecks } from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import { LogoInstitucional } from '@/components/comum/Logo'
import { useAuth } from '@/features/auth/AuthContext'
import { cn } from '@/lib/utils'

interface ItemNav {
  to: string
  icone: LucideIcon
  rotulo: string
  end?: boolean
  contagem?: number
  apenasAdmin?: boolean
}

const itens: ItemNav[] = [
  { to: '/', icone: LayoutDashboard, rotulo: 'Início', end: true },
  // /dfds (cadastro manual) está desativado da navegação — rota segue acessível por URL.
  { to: '/importar', icone: Upload, rotulo: 'Importar' },
  { to: '/validar-lote', icone: ListChecks, rotulo: 'Validar lote' },
  { to: '/historico', icone: History, rotulo: 'Histórico' },
  { to: '/logs', icone: ScrollText, rotulo: 'Logs' },
  { to: '/usuarios', icone: Users2, rotulo: 'Usuários' },
  { to: '/configuracoes', icone: Settings, rotulo: 'Configurações', apenasAdmin: true },
]

export function Sidebar() {
  const { ehAdmin } = useAuth()
  const itensVisiveis = itens.filter((i) => !i.apenasAdmin || ehAdmin)
  return (
    <aside className="w-[244px] shrink-0 bg-surface border-r border-border flex flex-col px-3 pt-3.5 pb-3 gap-0.5 min-h-0">
      {/* Brand institucional */}
      <div className="flex items-center px-2 pb-4 mb-2.5 border-b border-[hsl(var(--neutral-100))]">
        <LogoInstitucional className="h-8 max-w-full" />
      </div>

      {/* Nav */}
      <nav className="flex flex-col gap-0.5">
        {itensVisiveis.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            end={item.end}
            className={({ isActive }) =>
              cn(
                'flex items-center gap-2.5 h-9 px-2.5 rounded-md text-[13px] font-medium transition-colors',
                isActive
                  ? 'bg-[hsl(var(--brand-50))] text-[hsl(var(--brand-700))]'
                  : 'text-[hsl(var(--neutral-600))] hover:bg-[hsl(var(--neutral-100))] hover:text-foreground',
              )
            }
          >
            {({ isActive }) => (
              <>
                <item.icone className="size-4 shrink-0" />
                <span>{item.rotulo}</span>
                {item.contagem !== undefined && (
                  <span
                    className={cn(
                      'ml-auto font-mono text-[11px] px-1.5 py-px rounded-full tabular-nums',
                      isActive
                        ? 'bg-[hsl(var(--brand-100))] text-[hsl(var(--brand-700))]'
                        : 'bg-[hsl(var(--neutral-50))] text-muted-foreground',
                    )}
                  >
                    {item.contagem}
                  </span>
                )}
              </>
            )}
          </NavLink>
        ))}
      </nav>

      {/* Footer */}
      <div className="mt-auto pt-3 border-t border-[hsl(var(--neutral-100))]">
        <div className="px-2 py-1.5">
          <div className="text-[10px] uppercase tracking-[0.06em] font-semibold text-muted-foreground mb-0.5">
            Importador PCA
          </div>
          <div className="text-[11px] text-muted-foreground">SUBG · SMS-RJ</div>
        </div>
        <div className="flex justify-between items-center px-2 py-1 text-[11px] text-muted-foreground">
          <a
            href="/swagger"
            target="_blank"
            rel="noreferrer"
            className="hover:text-[hsl(var(--brand-600))] transition-colors no-underline"
          >
            API docs ↗
          </a>
          <span className="font-mono">v0.1 · interno</span>
        </div>
      </div>
    </aside>
  )
}
