import { LogOut, User, ChevronDown, Shield } from 'lucide-react'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { StatusToken } from '@/components/comum/StatusToken'
import { useAuth } from '@/features/auth/AuthContext'
import { Badge } from '@/components/ui/badge'

interface Props {
  onAbrirDialogToken: () => void
}

export function Topbar({ onAbrirDialogToken }: Props) {
  const { usuario, ehAdmin, logout } = useAuth()

  return (
    <header className="h-14 shrink-0 bg-surface border-b border-border sticky top-0 z-30">
      <div className="h-full px-6 flex items-center gap-3.5">
        <nav className="flex items-center gap-1.5 text-[13px] whitespace-nowrap">
          <span className="text-muted-foreground">Planejamento e Gerenciamento de Contratações</span>
          <span className="text-[hsl(var(--neutral-300))]">/</span>
          <strong className="text-foreground font-medium">Compras.gov.br</strong>
        </nav>

        <div className="flex-1" />

        {ehAdmin && <StatusToken onSubstituir={onAbrirDialogToken} />}

        {usuario && (
          <DropdownMenu>
            <DropdownMenuTrigger className="inline-flex items-center gap-2.5 h-9 px-2.5 rounded-md hover:bg-[hsl(var(--neutral-50))] transition-colors">
              <span className="size-7 rounded-full bg-[hsl(var(--brand-500))] text-white grid place-items-center text-[11px] font-bold">
                {iniciais(usuario.nome)}
              </span>
              <span className="hidden sm:flex flex-col items-start leading-none">
                <span className="text-[12px] font-medium text-foreground">{usuario.nome}</span>
                <span className="text-[10.5px] text-muted-foreground font-mono mt-0.5">{usuario.login}</span>
              </span>
              <ChevronDown className="size-3.5 text-muted-foreground" />
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="min-w-[220px]">
              <DropdownMenuLabel>
                <div className="flex flex-col gap-1">
                  <span className="text-[13px] font-medium">{usuario.nome}</span>
                  <span className="text-[11px] text-muted-foreground font-mono">{usuario.login}</span>
                  <div className="mt-1">
                    {ehAdmin ? (
                      <Badge variant="brand" comDot><Shield className="size-3" />admin</Badge>
                    ) : (
                      <Badge variant="muted"><User className="size-3" />normal</Badge>
                    )}
                  </div>
                </div>
              </DropdownMenuLabel>
              <DropdownMenuSeparator />
              <DropdownMenuItem onSelect={() => void logout()} className="text-destructive focus:text-destructive">
                <LogOut /> Sair
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        )}
      </div>
    </header>
  )
}

function iniciais(nome: string): string {
  const partes = nome.trim().split(/\s+/).filter(Boolean)
  if (partes.length === 0) return '?'
  if (partes.length === 1) return partes[0].slice(0, 2).toUpperCase()
  return (partes[0][0] + partes[partes.length - 1][0]).toUpperCase()
}
