import { useEffect, useRef, useState } from 'react'
import { KeyRound, RefreshCw, Trash2, Loader2, BarChart3, Eraser, Database, Play, X, CheckCircle2, AlertTriangle } from 'lucide-react'
import { PageHeader } from '@/components/comum/PageHeader'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Separator } from '@/components/ui/separator'
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { DialogColarToken } from '@/features/token/DialogColarToken'
import { useToken } from '@/features/token/TokenContext'
import { useAuth } from '@/features/auth/AuthContext'
import { importacaoApi } from '@/features/importacao/importacaoApi'
import type { MetricasImportacao } from '@/features/importacao/tipos'
import { catalogoApi } from '@/features/catalogo/catalogoApi'
import type { StatusSincronizacaoCatalogo } from '@/features/catalogo/tipos'
import { cn, formatarBrl, formatarDataHora, formatarDuracao, mascararCpf } from '@/lib/utils'
import type { EstadoToken } from '@/features/token/tipos'

const VARIANT: Record<EstadoToken, 'success' | 'warning' | 'destructive' | 'muted'> = {
  Saudavel: 'success',
  ProximoExpirar: 'warning',
  Expirado: 'destructive',
  RefreshFalhou: 'destructive',
  Ausente: 'muted',
}

const ROTULO: Record<EstadoToken, string> = {
  Ausente: 'Sem sessão',
  Saudavel: 'Ativa',
  ProximoExpirar: 'Renovando',
  Expirado: 'Expirada',
  RefreshFalhou: 'Falhou',
}

export function Configuracoes() {
  const { status, refresh, limpar } = useToken()
  const { ehAdmin } = useAuth()
  const [dialogAberto, setDialogAberto] = useState(false)
  const [renovando, setRenovando] = useState(false)

  const restanteSeg = status?.expiraEm
    ? Math.max(0, Math.floor((new Date(status.expiraEm).getTime() - Date.now()) / 1000))
    : 0

  return (
    <div className="space-y-6">
      <div className="anim-rise">
        <PageHeader
          kicker="Configurações"
          titulo="Sessão e dados"
          descricao="Gerencie o token Compras.gov.br, métricas de importação acumuladas e a base local de catálogo de materiais."
        />
      </div>

      {ehAdmin && (
      <Card>
        <CardHeader>
          <div className="flex items-start justify-between gap-4">
            <div>
              <CardTitle>Sessão Compras.gov.br</CardTitle>
              <CardDescription>
                Token gerenciado em memória (e refresh persistido em SQLite local).
              </CardDescription>
            </div>
            <Badge variant={VARIANT[status?.estado ?? 'Ausente']}>
              {ROTULO[status?.estado ?? 'Ausente']}
            </Badge>
          </div>
        </CardHeader>
        <CardContent className="space-y-6">
          {!status || status.estado === 'Ausente' ? (
            <div className="rounded-lg border border-dashed border-border p-6 flex flex-col items-center text-center">
              <div className="size-12 rounded-full bg-primary/10 flex items-center justify-center mb-3">
                <KeyRound className="size-5 text-primary" />
              </div>
              <div className="font-medium">Nenhum token conectado</div>
              <p className="text-sm text-muted-foreground mt-1 max-w-sm">
                Cole o refresh token capturado no Compras.gov.br para iniciar uma sessão.
              </p>
              <Button className="mt-4" onClick={() => setDialogAberto(true)}>
                <KeyRound /> Conectar token
              </Button>
            </div>
          ) : (
            <>
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-6">
                <Item rotulo="CPF do operador" valor={mascararCpf(status.sub)} />
                <Item rotulo="UASG" valor={status.numeroUasg?.toString() ?? '—'} />
                <Item rotulo="ID da sessão" valor={status.idSessao?.toString() ?? '—'} />
                <Item rotulo="Emitido em" valor={formatarDataHora(status.emitidoEm)} />
                <Item rotulo="Expira em" valor={formatarDataHora(status.expiraEm)} />
                <Item rotulo="Tempo restante" valor={formatarDuracao(restanteSeg)} className="font-semibold" />
              </div>

              <BarraTempo restante={restanteSeg} estado={status.estado} expiraEm={status.expiraEm} emitidoEm={status.emitidoEm} />

              <Separator />

              <div>
                <div className="text-xs uppercase tracking-wide text-muted-foreground mb-2">Permissões (mnemônicos)</div>
                <div className="flex flex-wrap gap-1.5">
                  {status.mnemonicos?.map((m) => (
                    <Badge key={m} variant="outline" className="font-mono text-[10px]">{m}</Badge>
                  ))}
                </div>
              </div>

              <Separator />

              <div className="flex flex-wrap items-center gap-2">
                <Button onClick={() => setDialogAberto(true)}>
                  <KeyRound /> Substituir token
                </Button>
                <Button
                  variant="outline"
                  disabled={renovando}
                  onClick={async () => {
                    setRenovando(true)
                    try { await refresh() } finally { setRenovando(false) }
                  }}
                >
                  {renovando ? <Loader2 className="animate-spin" /> : <RefreshCw />}
                  Forçar renovação
                </Button>
                <Button variant="outline" className="text-destructive hover:text-destructive" onClick={() => void limpar()}>
                  <Trash2 /> Sair (limpar tokens)
                </Button>
              </div>

              {status.ultimoErroRefresh && (
                <div className="rounded-md border border-destructive/30 bg-destructive/5 px-3 py-2 text-sm text-destructive">
                  <span className="font-medium">Último erro:</span> {status.ultimoErroRefresh}
                </div>
              )}
            </>
          )}
        </CardContent>
      </Card>

      )}

      {ehAdmin && (
      <Card>
        <CardHeader>
          <CardTitle>Sobre tokens do Compras</CardTitle>
        </CardHeader>
        <CardContent className="text-sm text-muted-foreground space-y-3">
          <p>
            O <span className="font-medium text-foreground">access token</span> tem TTL de ~10 minutos e carrega
            seu CPF, UASG e mnemônicos. É usado em todas as chamadas de negócio.
          </p>
          <p>
            O <span className="font-medium text-foreground">refresh token</span> tem TTL de ~30 minutos e serve
            apenas para emitir novos pares via <code className="rounded bg-muted px-1 py-0.5 text-[11px] font-mono">GET /sessao/governo/retoken</code>.
            O sistema rotaciona ambos a cada renovação e persiste o refresh para sobreviver a reinícios.
          </p>
        </CardContent>
      </Card>
      )}

      <CardCatalogo />

      <CardMetricas />

      <DialogColarToken aberto={dialogAberto} onFechar={() => setDialogAberto(false)} />
    </div>
  )
}

function CardMetricas() {
  const [m, setM] = useState<MetricasImportacao | null>(null)
  const [confirmar, setConfirmar] = useState(false)
  const [resetando, setResetando] = useState(false)
  const [erro, setErro] = useState<string | null>(null)

  async function carregar() {
    try {
      setM(await importacaoApi.metricas())
    } catch {
    }
  }

  useEffect(() => { void carregar() }, [])

  async function aoResetar() {
    setResetando(true)
    setErro(null)
    try {
      await importacaoApi.resetarMetricas()
      await carregar()
      setConfirmar(false)
    } catch (e) {
      setErro(e instanceof Error ? e.message : String(e))
    } finally {
      setResetando(false)
    }
  }

  return (
    <>
      <Card>
        <CardHeader>
          <div className="flex items-start justify-between gap-4">
            <div>
              <CardTitle className="flex items-center gap-2"><BarChart3 className="size-4" /> Métricas de importação</CardTitle>
              <CardDescription>
                Histórico acumulado das planilhas importadas. Limpar não afeta DFDs já criados no Compras.
              </CardDescription>
            </div>
            <Button variant="outline" size="sm" className="text-destructive hover:text-destructive" onClick={() => setConfirmar(true)} disabled={!m || m.totalPlanilhas === 0}>
              <Eraser /> Resetar métricas
            </Button>
          </div>
        </CardHeader>
        <CardContent>
          {m === null ? (
            <div className="text-sm text-muted-foreground">Carregando…</div>
          ) : (
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-6 text-sm">
              <Item rotulo="Planilhas importadas" valor={m.totalPlanilhas.toLocaleString('pt-BR')} />
              <Item rotulo="Itens importados" valor={m.totalItens.toLocaleString('pt-BR')} />
              <Item rotulo="Valor total" valor={formatarBrl(m.totalValor)} />
            </div>
          )}
        </CardContent>
      </Card>

      <Dialog open={confirmar} onOpenChange={(open) => !open && !resetando && setConfirmar(false)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <div className="flex size-10 items-center justify-center rounded-full bg-destructive/10 mb-1">
              <Eraser className="size-5 text-destructive" />
            </div>
            <DialogTitle>Resetar métricas de importação?</DialogTitle>
            <DialogDescription>
              Vamos apagar o histórico local de planilhas importadas. Os contadores voltam a zero e a detecção de duplicado deixa de reconhecer importações antigas.
            </DialogDescription>
          </DialogHeader>
          <Alert variant="warning">
            <AlertDescription>
              Os DFDs já criados no Compras.gov.br <strong>não são afetados</strong> — só apagamos os registros locais.
            </AlertDescription>
          </Alert>
          {erro && (
            <Alert variant="destructive">
              <AlertDescription>{erro}</AlertDescription>
            </Alert>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setConfirmar(false)} disabled={resetando}>Cancelar</Button>
            <Button variant="destructive" onClick={() => void aoResetar()} disabled={resetando}>
              {resetando && <Loader2 className="animate-spin" />}
              {resetando ? 'Apagando…' : 'Sim, apagar histórico'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  )
}

function Item({ rotulo, valor, className }: { rotulo: string; valor: string; className?: string }) {
  return (
    <div>
      <div className="text-xs uppercase tracking-wide text-muted-foreground">{rotulo}</div>
      <div className={cn('mt-0.5 text-sm font-mono tabular-nums', className)}>{valor}</div>
    </div>
  )
}

function CardCatalogo() {
  const [s, setS] = useState<StatusSincronizacaoCatalogo | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const [confirmarLimpar, setConfirmarLimpar] = useState(false)
  const [limpando, setLimpando] = useState(false)
  const timerRef = useRef<number | null>(null)

  async function carregar() {
    try {
      setS(await catalogoApi.status())
    } catch (e) {
      setErro(e instanceof Error ? e.message : String(e))
    }
  }

  useEffect(() => {
    void carregar()
    timerRef.current = window.setInterval(carregar, 2000)
    return () => { if (timerRef.current) window.clearInterval(timerRef.current) }
  }, [])

  async function aoSincronizar() {
    setErro(null)
    try {
      await catalogoApi.sincronizar()
      await carregar()
    } catch (e) {
      setErro(e instanceof Error ? e.message : String(e))
    }
  }

  async function aoCancelar() {
    setErro(null)
    try {
      await catalogoApi.cancelar()
      await carregar()
    } catch (e) {
      setErro(e instanceof Error ? e.message : String(e))
    }
  }

  async function aoLimpar() {
    setLimpando(true)
    setErro(null)
    try {
      await catalogoApi.limpar()
      await carregar()
      setConfirmarLimpar(false)
    } catch (e) {
      setErro(e instanceof Error ? e.message : String(e))
    } finally {
      setLimpando(false)
    }
  }

  const executando = s?.estado === 'Executando'
  const pct = s && s.totalPaginas > 0 ? Math.min(100, Math.round((s.paginaAtual / s.totalPaginas) * 100)) : 0
  const etaSeg = s && executando && s.iniciadaEm && s.itensProcessados > 0 && s.totalRegistros > 0
    ? Math.max(0, Math.round(((s.totalRegistros - s.itensProcessados) /
        (s.itensProcessados / Math.max(1, (Date.now() - new Date(s.iniciadaEm).getTime()) / 1000)))))
    : 0

  return (
    <>
      <Card>
        <CardHeader>
          <div className="flex items-start justify-between gap-4">
            <div>
              <CardTitle className="flex items-center gap-2"><Database className="size-4" /> Catálogo local de materiais</CardTitle>
              <CardDescription>
                Base local sincronizada com o portal de Dados Abertos. Acelera a importação ao evitar consultas item a item.
              </CardDescription>
            </div>
            <div className="flex items-center gap-2">
              {executando ? (
                <Button variant="outline" size="sm" onClick={() => void aoCancelar()}>
                  <X /> Cancelar
                </Button>
              ) : (
                <Button size="sm" onClick={() => void aoSincronizar()}>
                  <Play /> {s && s.totalArmazenado > 0 ? 'Sincronizar novamente' : 'Sincronizar agora'}
                </Button>
              )}
              <Button
                variant="outline"
                size="sm"
                className="text-destructive hover:text-destructive"
                onClick={() => setConfirmarLimpar(true)}
                disabled={!s || s.totalArmazenado === 0 || executando}
              >
                <Eraser /> Apagar
              </Button>
            </div>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          {s === null ? (
            <div className="text-sm text-muted-foreground">Carregando…</div>
          ) : (
            <>
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-6 text-sm">
                <Item rotulo="Itens armazenados" valor={s.totalArmazenado.toLocaleString('pt-BR')} />
                <Item rotulo="Última sincronização" valor={s.ultimaSincronizacao ? formatarDataHora(s.ultimaSincronizacao) : '—'} />
                <Item rotulo="Estado" valor={s.estado} />
              </div>

              {executando && (
                <div className="space-y-1.5">
                  <div className="flex items-baseline justify-between text-xs text-muted-foreground tabular-nums">
                    <span>
                      Página {s.paginaAtual.toLocaleString('pt-BR')} de {s.totalPaginas.toLocaleString('pt-BR')} · {s.itensProcessados.toLocaleString('pt-BR')} / {s.totalRegistros.toLocaleString('pt-BR')} itens
                    </span>
                    <span>{pct}% · ETA {formatarDuracao(etaSeg)}</span>
                  </div>
                  <div className="h-2 rounded-full bg-muted overflow-hidden">
                    <div className="h-full bg-primary transition-all" style={{ width: `${pct}%` }} />
                  </div>
                </div>
              )}

              {s.estado === 'Concluida' && (
                <Alert variant="success">
                  <CheckCircle2 />
                  <AlertDescription>
                    Sincronização concluída em {s.concluidaEm ? formatarDataHora(s.concluidaEm) : '—'} ({s.itensProcessados.toLocaleString('pt-BR')} itens).
                  </AlertDescription>
                </Alert>
              )}
              {s.estado === 'Falhou' && s.ultimoErro && (
                <Alert variant="destructive">
                  <AlertTriangle />
                  <AlertDescription className="break-words">{s.ultimoErro}</AlertDescription>
                </Alert>
              )}
              {s.estado === 'Cancelada' && (
                <Alert variant="warning">
                  <AlertTriangle />
                  <AlertDescription>Sincronização cancelada manualmente.</AlertDescription>
                </Alert>
              )}
              {erro && (
                <Alert variant="destructive">
                  <AlertTriangle />
                  <AlertDescription>{erro}</AlertDescription>
                </Alert>
              )}
            </>
          )}
        </CardContent>
      </Card>

      <Dialog open={confirmarLimpar} onOpenChange={(open) => !open && !limpando && setConfirmarLimpar(false)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <div className="flex size-10 items-center justify-center rounded-full bg-destructive/10 mb-1">
              <Eraser className="size-5 text-destructive" />
            </div>
            <DialogTitle>Apagar catálogo local?</DialogTitle>
            <DialogDescription>
              Vamos remover todos os itens armazenados localmente. As importações voltarão a consultar o catálogo do Compras item a item até a próxima sincronização.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setConfirmarLimpar(false)} disabled={limpando}>Cancelar</Button>
            <Button variant="destructive" onClick={() => void aoLimpar()} disabled={limpando}>
              {limpando && <Loader2 className="animate-spin" />}
              {limpando ? 'Apagando…' : 'Sim, apagar'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  )
}

function BarraTempo({ restante, estado, expiraEm, emitidoEm }: { restante: number; estado: EstadoToken; expiraEm: string | null; emitidoEm: string | null }) {
  const total = expiraEm && emitidoEm
    ? Math.max(1, Math.floor((new Date(expiraEm).getTime() - new Date(emitidoEm).getTime()) / 1000))
    : 600
  const pct = Math.max(0, Math.min(100, (restante / total) * 100))
  const cor = estado === 'Expirado' || estado === 'RefreshFalhou'
    ? 'bg-red-500'
    : pct > 50 ? 'bg-emerald-500' : pct > 20 ? 'bg-amber-500' : 'bg-red-500'

  return (
    <div>
      <div className="h-1.5 rounded-full bg-muted overflow-hidden">
        <div className={cn('h-full transition-all', cor)} style={{ width: `${pct}%` }} />
      </div>
    </div>
  )
}
