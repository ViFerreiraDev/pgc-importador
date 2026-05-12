import { useState } from 'react'
import { KeyRound, Loader2, AlertTriangle, ExternalLink } from 'lucide-react'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { useToken } from './TokenContext'

interface Props {
  aberto: boolean
  onFechar: () => void
  forcado?: boolean
}

export function DialogColarToken({ aberto, onFechar, forcado = false }: Props) {
  const { definir } = useToken()
  const [refresh, setRefresh] = useState('')
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  async function aoEnviar() {
    const valor = refresh.trim()
    if (!valor) {
      setErro('Cole o refresh token antes de enviar.')
      return
    }
    if (!pareceJwt(valor)) {
      setErro('Não parece um JWT válido (esperado três partes separadas por ponto).')
      return
    }

    setEnviando(true)
    setErro(null)
    try {
      await definir(valor)
      setRefresh('')
      onFechar()
    } catch (e) {
      setErro(e instanceof Error ? e.message : String(e))
    } finally {
      setEnviando(false)
    }
  }

  return (
    <Dialog open={aberto} onOpenChange={(open) => { if (!open && !forcado) onFechar() }}>
      <DialogContent
        showCloseButton={!forcado}
        onInteractOutside={(e) => { if (forcado) e.preventDefault() }}
        onEscapeKeyDown={(e) => { if (forcado) e.preventDefault() }}
        className="sm:max-w-2xl"
      >
        <DialogHeader>
          <div className="flex size-10 items-center justify-center rounded-full bg-primary/10 mb-1">
            <KeyRound className="size-5 text-primary" />
          </div>
          <DialogTitle>Conectar ao Compras.gov.br</DialogTitle>
          <DialogDescription>
            Cole o <span className="font-medium text-foreground">refresh token</span> capturado no Compras. Vamos
            usar ele pra emitir o access token e manter a sessão viva sozinha.
          </DialogDescription>
        </DialogHeader>

        {forcado && (
          <Alert variant="warning">
            <AlertTriangle />
            <AlertDescription>
              Sessão expirou ou refresh token inválido. Cole um novo para continuar.
            </AlertDescription>
          </Alert>
        )}

        <div className="rounded-lg border border-border bg-[hsl(var(--neutral-50))] p-3 flex items-center gap-3">
          <div className="size-8 rounded-md grid place-items-center bg-[hsl(var(--brand-50))] text-[hsl(var(--brand-600))] shrink-0">
            <ExternalLink className="size-4" />
          </div>
          <div className="flex-1 min-w-0">
            <div className="text-[13px] font-semibold leading-tight">Ainda não tem o token?</div>
            <div className="text-[12px] text-muted-foreground leading-tight mt-0.5">
              Faça login no Compras numa nova aba e copie o refresh token de lá.
            </div>
          </div>
          <Button asChild variant="secondary" size="sm" className="shrink-0">
            <a
              href="https://www.comprasnet.gov.br/seguro/loginPortalUASG.asp"
              target="_blank"
              rel="noreferrer"
            >
              <ExternalLink /> Abrir Compras
            </a>
          </Button>
        </div>

        <div className="space-y-2">
          <Label htmlFor="refreshToken">Refresh token</Label>
          <Textarea
            id="refreshToken"
            placeholder="eyJhbGciOi..."
            value={refresh}
            onChange={(e) => setRefresh(e.target.value)}
            rows={6}
            autoFocus
          />
          <p className="text-xs text-muted-foreground">
            Encontre na resposta de qualquer chamada <code className="rounded bg-muted px-1 py-0.5 text-[10px]">/sessao/governo/retoken</code> ou no storage do navegador.
          </p>
        </div>

        {erro && (
          <Alert variant="destructive">
            <AlertTriangle />
            <AlertDescription>{erro}</AlertDescription>
          </Alert>
        )}

        <DialogFooter>
          {!forcado && (
            <Button variant="outline" onClick={onFechar} disabled={enviando}>
              Cancelar
            </Button>
          )}
          <Button onClick={aoEnviar} disabled={enviando || !refresh.trim()}>
            {enviando && <Loader2 className="animate-spin" />}
            {enviando ? 'Validando...' : 'Conectar'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

function pareceJwt(s: string): boolean {
  const limpo = s.startsWith('Bearer ') ? s.slice(7).trim() : s
  const partes = limpo.split('.')
  if (partes.length !== 3) return false
  if (partes[0].length < 5 || partes[1].length < 5) return false
  return true
}
