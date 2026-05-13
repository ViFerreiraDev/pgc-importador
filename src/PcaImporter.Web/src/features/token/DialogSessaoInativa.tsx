import { useState } from 'react'
import { ShieldAlert, LogOut, Loader2 } from 'lucide-react'
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { useAuth } from '@/features/auth/AuthContext'
import type { EstadoToken } from './tipos'

interface Props {
  aberto: boolean
  estado: EstadoToken
}

const TEXTO_ESTADO: Record<EstadoToken, string> = {
  Ausente: 'Nenhuma sessão Compras.gov.br conectada no momento.',
  Saudavel: '',
  ProximoExpirar: '',
  Expirado: 'A sessão Compras.gov.br está expirada.',
  RefreshFalhou: 'A renovação automática da sessão falhou.',
}

/**
 * Modal travado para usuários normais quando não há sessão Compras viva.
 * Não deixa interagir com o sistema (sem campo de token — eles não têm permissão),
 * mas mantém o botão de Sair pra eles trocarem de conta se precisarem.
 */
export function DialogSessaoInativa({ aberto, estado }: Props) {
  const { usuario, logout } = useAuth()
  const [saindo, setSaindo] = useState(false)
  const subtitulo = TEXTO_ESTADO[estado] || 'A sessão Compras.gov.br não está disponível.'

  return (
    <Dialog open={aberto} onOpenChange={() => { /* travado */ }}>
      <DialogContent
        showCloseButton={false}
        onInteractOutside={(e) => e.preventDefault()}
        onEscapeKeyDown={(e) => e.preventDefault()}
        className="sm:max-w-md"
      >
        <DialogHeader>
          <div className="flex size-10 items-center justify-center rounded-[10px] bg-[hsl(var(--warning-50))] text-[hsl(var(--warning-500))] mb-1">
            <ShieldAlert className="size-5" />
          </div>
          <DialogTitle>Sessão Compras.gov.br indisponível</DialogTitle>
          <DialogDescription>
            {subtitulo}
          </DialogDescription>
        </DialogHeader>

        <div className="text-[13px] leading-relaxed space-y-2.5">
          <p>
            O sistema precisa de uma sessão ativa com o Compras.gov.br para funcionar — só um <strong className="font-semibold">administrador</strong> pode conectar ou renovar essa sessão.
          </p>
          <p className="text-muted-foreground">
            Entre em contato com o administrador do sistema. Assim que ele reconectar, esta tela some sozinha e você pode voltar a trabalhar.
          </p>
        </div>

        {usuario && (
          <div className="rounded-md border border-border bg-[hsl(var(--neutral-50))] px-3 py-2 text-[12px] flex items-center justify-between gap-3">
            <div>
              <div className="text-muted-foreground">Você está logado como</div>
              <div className="font-mono mt-0.5">{usuario.login} <span className="text-muted-foreground">· {usuario.nome}</span></div>
            </div>
          </div>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={() => { setSaindo(true); void logout() }} disabled={saindo}>
            {saindo ? <Loader2 className="animate-spin" /> : <LogOut />}
            {saindo ? 'Saindo…' : 'Sair'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
