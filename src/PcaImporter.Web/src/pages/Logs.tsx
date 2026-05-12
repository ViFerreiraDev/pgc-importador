import { useCallback, useEffect, useState } from 'react'
import { ChevronLeft, ChevronRight, RefreshCw, ScrollText, Loader2 } from 'lucide-react'
import { PageHeader } from '@/components/comum/PageHeader'
import { Card, CardContent } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Select } from '@/components/ui/select'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Badge } from '@/components/ui/badge'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { EmptyState } from '@/components/comum/EmptyState'
import { logsApi } from '@/features/logs/logsApi'
import type { NivelLog, PaginaLogs } from '@/features/logs/tipos'
import { cn, formatarDataHora } from '@/lib/utils'

const tamanhoPagina = 50

const corPonto: Record<NivelLog, string> = {
  Info: 'bg-muted-foreground/40',
  Sucesso: 'bg-emerald-500',
  Aviso: 'bg-amber-500',
  Erro: 'bg-red-500',
}

const variantNivel: Record<NivelLog, 'default' | 'success' | 'warning' | 'destructive' | 'muted'> = {
  Info: 'muted',
  Sucesso: 'success',
  Aviso: 'warning',
  Erro: 'destructive',
}

export function Logs() {
  const [pagina, setPagina] = useState(1)
  const [nivel, setNivel] = useState<NivelLog | ''>('')
  const [categoria, setCategoria] = useState('')
  const [dados, setDados] = useState<PaginaLogs>({ itens: [], pagina: 1, tamanhoPagina, total: 0 })
  const [carregando, setCarregando] = useState(true)
  const [autoRefresh, setAutoRefresh] = useState(true)

  const carregar = useCallback(async (paginaAlvo: number) => {
    setCarregando(true)
    try {
      const r = await logsApi.listar({
        pagina: paginaAlvo,
        tamanhoPagina,
        nivel: nivel || undefined,
        categoria: categoria || undefined,
      })
      setDados(r)
    } finally {
      setCarregando(false)
    }
  }, [nivel, categoria])

  useEffect(() => { void carregar(pagina) }, [carregar, pagina])

  useEffect(() => {
    if (!autoRefresh) return
    const id = setInterval(() => { void carregar(pagina) }, 5000)
    return () => clearInterval(id)
  }, [autoRefresh, carregar, pagina])

  const totalPaginas = Math.max(1, Math.ceil(dados.total / tamanhoPagina))

  return (
    <div className="space-y-6">
      <PageHeader
        kicker="Logs"
        titulo="Trilha de eventos"
        descricao="Eventos do sistema em tempo real para auditoria e diagnóstico."
        acoes={
          <>
            <Button
              variant={autoRefresh ? 'secondary' : 'outline'}
              size="sm"
              onClick={() => setAutoRefresh((v) => !v)}
            >
              <RefreshCw className={cn(autoRefresh && 'animate-spin')} />
              {autoRefresh ? 'Auto on' : 'Auto off'}
            </Button>
            <Button variant="outline" size="sm" onClick={() => void carregar(pagina)} disabled={carregando}>
              {carregando ? <Loader2 className="animate-spin" /> : <RefreshCw />}
              Atualizar
            </Button>
          </>
        }
      />

      <Card>
        <CardContent className="p-4 grid grid-cols-12 gap-3 items-end border-b border-border">
          <div className="col-span-12 sm:col-span-3">
            <Label htmlFor="nivel">Nível mínimo</Label>
            <Select
              id="nivel"
              className="mt-1.5"
              value={nivel}
              onChange={(e) => { setPagina(1); setNivel(e.target.value as NivelLog | '') }}
            >
              <option value="">Todos</option>
              <option value="Info">Info</option>
              <option value="Sucesso">Sucesso</option>
              <option value="Aviso">Aviso</option>
              <option value="Erro">Erro</option>
            </Select>
          </div>
          <div className="col-span-12 sm:col-span-4">
            <Label htmlFor="categoria">Categoria</Label>
            <Input
              id="categoria"
              className="mt-1.5"
              placeholder="ex: Token, DFD, Item"
              value={categoria}
              onChange={(e) => setCategoria(e.target.value)}
              onKeyDown={(e) => { if (e.key === 'Enter') { setPagina(1); void carregar(1) } }}
            />
          </div>
          <div className="col-span-12 sm:col-span-5 flex items-end justify-end gap-2 text-sm text-muted-foreground">
            <span><span className="font-medium text-foreground tabular-nums">{dados.total}</span> {dados.total === 1 ? 'evento' : 'eventos'}</span>
          </div>
        </CardContent>

        {dados.itens.length === 0 && !carregando ? (
          <EmptyState
            icon={ScrollText}
            titulo="Sem eventos"
            descricao={categoria || nivel ? 'Tente outros filtros.' : 'Conforme o sistema for usado, eventos vão aparecer aqui.'}
          />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-32">Quando</TableHead>
                <TableHead className="w-24">Nível</TableHead>
                <TableHead className="w-28">Categoria</TableHead>
                <TableHead>Mensagem</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {dados.itens.map((log) => (
                <TableRow key={log.id}>
                  <TableCell className="text-xs text-muted-foreground tabular-nums whitespace-nowrap">
                    {formatarDataHora(log.ocorridoEm)}
                  </TableCell>
                  <TableCell>
                    <span className="inline-flex items-center gap-2">
                      <span className={cn('size-2 rounded-full', corPonto[log.nivel])} />
                      <Badge variant={variantNivel[log.nivel]} className="text-[10px]">{log.nivel}</Badge>
                    </span>
                  </TableCell>
                  <TableCell><Badge variant="outline" className="text-[10px]">{log.categoria}</Badge></TableCell>
                  <TableCell>
                    <div className="text-sm">{log.mensagem}</div>
                    {log.detalhes && (
                      <div className="text-xs text-muted-foreground font-mono mt-0.5 break-all">{log.detalhes}</div>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}

        <div className="flex items-center justify-between p-4 border-t border-border">
          <div className="text-xs text-muted-foreground">
            Página <span className="tabular-nums">{dados.pagina}</span> de <span className="tabular-nums">{totalPaginas}</span>
          </div>
          <div className="flex items-center gap-1">
            <Button variant="outline" size="icon" onClick={() => setPagina((p) => Math.max(1, p - 1))} disabled={pagina <= 1 || carregando}>
              <ChevronLeft />
            </Button>
            <Button variant="outline" size="icon" onClick={() => setPagina((p) => Math.min(totalPaginas, p + 1))} disabled={pagina >= totalPaginas || carregando}>
              <ChevronRight />
            </Button>
          </div>
        </div>
      </Card>
    </div>
  )
}
