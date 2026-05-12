import { useMemo, useState } from 'react'
import {
  AlertTriangle, CheckCircle2, Eraser, ExternalLink, Loader2, ListChecks, RotateCw, Search, XCircle,
} from 'lucide-react'
import { PageHeader } from '@/components/comum/PageHeader'
import { Card, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Textarea } from '@/components/ui/input'
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'
import { importacaoApi } from '@/features/importacao/importacaoApi'
import type { ResultadoValidacao } from '@/features/importacao/tipos'
import { cn } from '@/lib/utils'

const PADRAO_SHEETS_URL = /(?:https?|htps):\/\/docs\.google\.com\/spreadsheets\/d\/([a-zA-Z0-9_-]+)[^\s)]*/g

interface LinkExtraido {
  rotulo: string
  url: string
  urlOriginal: string
  idPlanilha: string
}

type Estado = 'pendente' | 'validando' | 'valido' | 'invalido' | 'erro'

interface ResultadoLink {
  estado: Estado
  resultado?: ResultadoValidacao
  mensagemErro?: string
}

export function ValidarLote() {
  const [texto, setTexto] = useState('')
  const [links, setLinks] = useState<LinkExtraido[]>([])
  const [resultados, setResultados] = useState<Map<string, ResultadoLink>>(new Map())
  const [executando, setExecutando] = useState(false)
  const [progresso, setProgresso] = useState({ feitos: 0, total: 0 })
  const [detalhes, setDetalhes] = useState<{ link: LinkExtraido; r: ResultadoLink } | null>(null)

  function extrair() {
    const extraidos = extrairLinks(texto)
    setLinks(extraidos)
    const mapa = new Map<string, ResultadoLink>()
    for (const l of extraidos) mapa.set(l.idPlanilha, { estado: 'pendente' })
    setResultados(mapa)
  }

  function limpar() {
    setTexto('')
    setLinks([])
    setResultados(new Map())
    setProgresso({ feitos: 0, total: 0 })
  }

  async function rodarValidacao(alvos: LinkExtraido[]) {
    if (alvos.length === 0) return
    setExecutando(true)
    setProgresso({ feitos: 0, total: alvos.length })

    // Reseta apenas os escolhidos para 'pendente' antes de rodar.
    setResultados((prev) => {
      const novo = new Map(prev)
      for (const link of alvos) novo.set(link.idPlanilha, { estado: 'pendente' })
      return novo
    })

    let feitos = 0
    let i = 0

    const atualizar = (id: string, r: ResultadoLink) => {
      setResultados((prev) => {
        const novo = new Map(prev)
        novo.set(id, r)
        return novo
      })
    }

    async function worker() {
      while (i < alvos.length) {
        const idx = i++
        const link = alvos[idx]
        atualizar(link.idPlanilha, { estado: 'validando' })
        try {
          const r = await importacaoApi.validarLink(link.url)
          atualizar(link.idPlanilha, {
            estado: r.valido ? 'valido' : 'invalido',
            resultado: r,
          })
        } catch (e) {
          atualizar(link.idPlanilha, {
            estado: 'erro',
            mensagemErro: e instanceof Error ? e.message : String(e),
          })
        } finally {
          feitos++
          setProgresso({ feitos, total: alvos.length })
        }
      }
    }

    // Concorrência limitada — 3 em paralelo (evita estressar o Sheets/API)
    await Promise.all([worker(), worker(), worker()])
    setExecutando(false)
  }

  function validarTodos() {
    void rodarValidacao(links)
  }

  function retentarFalhas() {
    const falhas = links.filter((l) => {
      const r = resultados.get(l.idPlanilha)
      return r?.estado === 'invalido' || r?.estado === 'erro'
    })
    void rodarValidacao(falhas)
  }

  function retentarUm(link: LinkExtraido) {
    void rodarValidacao([link])
  }

  const stats = useMemo(() => {
    let validos = 0, invalidos = 0, erros = 0, pendentes = 0, totalMateriais = 0
    for (const r of resultados.values()) {
      if (r.estado === 'valido') { validos++; totalMateriais += r.resultado?.totalMateriais ?? 0 }
      else if (r.estado === 'invalido') invalidos++
      else if (r.estado === 'erro') erros++
      else pendentes++
    }
    return { validos, invalidos, erros, pendentes, totalMateriais }
  }, [resultados])

  return (
    <div className="anim-rise space-y-6">
      <PageHeader
        titulo="Validar em lote"
        descricao="Cole vários links de planilhas Google Sheets e valide todos de uma vez. Não importa nada — só verifica se cada planilha está correta antes de você iniciar as importações."
      />

      <Card>
        <CardHeader>
          <div>
            <CardTitle><ListChecks />1. Cole o texto com os links</CardTitle>
            <CardDescription>
              Aceita texto livre — qualquer URL no formato <code className="font-mono text-[11px]">docs.google.com/spreadsheets/d/...</code> é extraída.
              O rótulo é o que aparece antes da URL na mesma linha (ex.: "Grupo 01").
            </CardDescription>
          </div>
        </CardHeader>

        <div className="p-[18px] space-y-3">
          <Textarea
            placeholder={`MEDICAMENTOS\nGrupo 01: https://docs.google.com/spreadsheets/d/...\nGrupo 02: https://docs.google.com/spreadsheets/d/...\n\nSANEANTES\nGrupo 01 - https://docs.google.com/spreadsheets/d/...`}
            value={texto}
            onChange={(e) => setTexto(e.target.value)}
            className="font-mono text-[12px] min-h-[180px]"
          />
          <div className="flex flex-wrap gap-2">
            <Button onClick={extrair} disabled={!texto.trim()}>
              <Search /> Extrair links
            </Button>
            <Button variant="ghost" onClick={limpar} disabled={!texto && links.length === 0}>
              <Eraser /> Limpar
            </Button>
          </div>
        </div>
      </Card>

      {links.length > 0 && (
        <Card>
          <CardHeader>
            <div className="min-w-0 flex-1">
              <CardTitle><CheckCircle2 />2. Validar {links.length} {links.length === 1 ? 'link' : 'links'}</CardTitle>
              <CardDescription>
                {stats.validos > 0 && <>{stats.validos} válidos · </>}
                {stats.invalidos > 0 && <>{stats.invalidos} com erros · </>}
                {stats.erros > 0 && <>{stats.erros} falharam · </>}
                {stats.pendentes > 0 && <>{stats.pendentes} pendentes</>}
                {stats.totalMateriais > 0 && <> · {stats.totalMateriais.toLocaleString('pt-BR')} materiais somados</>}
              </CardDescription>
            </div>
            <div className="flex gap-2">
              {(stats.invalidos > 0 || stats.erros > 0) && !executando && (
                <Button variant="secondary" onClick={retentarFalhas}>
                  <RotateCw /> Tentar de novo {stats.invalidos + stats.erros}
                </Button>
              )}
              <Button onClick={validarTodos} disabled={executando}>
                {executando ? <Loader2 className="animate-spin" /> : <CheckCircle2 />}
                {executando ? `Validando ${progresso.feitos}/${progresso.total}…` : 'Validar todos'}
              </Button>
            </div>
          </CardHeader>

          {executando && (
            <div className="px-[18px] pb-3">
              <div className="h-1 rounded-full bg-[hsl(var(--neutral-100))] overflow-hidden">
                <div
                  className="h-full bg-[hsl(var(--brand-500))] transition-all"
                  style={{ width: `${(progresso.feitos / Math.max(1, progresso.total)) * 100}%` }}
                />
              </div>
            </div>
          )}

          <div className="overflow-x-auto">
            <table className="w-full text-[13px]">
              <thead>
                <tr className="text-left">
                  <Th className="w-[100px]">Status</Th>
                  <Th className="w-[180px]">Rótulo</Th>
                  <Th>Link</Th>
                  <Th className="w-[100px] text-right">Materiais</Th>
                  <Th className="w-[90px] text-right">Avisos</Th>
                  <Th className="w-[110px] text-right">Ações</Th>
                </tr>
              </thead>
              <tbody>
                {links.map((l) => {
                  const r = resultados.get(l.idPlanilha) ?? { estado: 'pendente' as Estado }
                  return (
                    <tr key={l.idPlanilha} className="border-b border-[hsl(var(--neutral-100))] last:border-b-0">
                      <Td><StatusBadge estado={r.estado} /></Td>
                      <Td className="font-medium">{l.rotulo}</Td>
                      <Td>
                        <a
                          href={l.urlOriginal}
                          target="_blank"
                          rel="noreferrer"
                          className="text-[12px] text-muted-foreground hover:text-[hsl(var(--brand-600))] inline-flex items-center gap-1 truncate max-w-[460px]"
                          title={l.urlOriginal}
                        >
                          <ExternalLink className="size-3 shrink-0 opacity-60" />
                          <span className="truncate">{l.urlOriginal}</span>
                        </a>
                      </Td>
                      <Td className="text-right font-mono tabular-nums">
                        {r.resultado ? r.resultado.totalMateriais.toLocaleString('pt-BR') : '—'}
                      </Td>
                      <Td className="text-right font-mono tabular-nums">
                        {r.resultado ? r.resultado.avisos.length : '—'}
                      </Td>
                      <Td className="text-right">
                        <div className="inline-flex items-center gap-1 justify-end">
                          {(r.estado === 'invalido' || r.estado === 'erro') && !executando && (
                            <Button variant="ghost" size="xs" onClick={() => retentarUm(l)} title="Tentar de novo">
                              <RotateCw />
                            </Button>
                          )}
                          {(r.estado === 'invalido' || r.estado === 'erro' || (r.estado === 'valido' && (r.resultado?.avisos.length ?? 0) > 0)) && (
                            <Button variant="ghost" size="xs" onClick={() => setDetalhes({ link: l, r })}>
                              Detalhes
                            </Button>
                          )}
                        </div>
                      </Td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        </Card>
      )}

      {links.length === 0 && texto.trim() && (
        <Alert variant="warning">
          <AlertTriangle />
          <AlertDescription>
            Nenhum link no formato Google Sheets encontrado no texto colado. Confira se as URLs estão em <code className="font-mono text-[11px]">docs.google.com/spreadsheets/d/...</code>.
          </AlertDescription>
        </Alert>
      )}

      <DialogDetalhes
        item={detalhes}
        onFechar={() => setDetalhes(null)}
      />
    </div>
  )
}

function Th({ children, className }: { children: React.ReactNode; className?: string }) {
  return (
    <th
      className={cn(
        'px-3.5 py-2.5 text-[11px] uppercase tracking-[0.05em] font-semibold text-muted-foreground bg-[hsl(var(--neutral-25))] border-b border-border',
        className,
      )}
    >
      {children}
    </th>
  )
}

function Td({ children, className }: { children: React.ReactNode; className?: string }) {
  return <td className={cn('px-3.5 py-3 align-middle', className)}>{children}</td>
}

function StatusBadge({ estado }: { estado: Estado }) {
  if (estado === 'pendente') return <Badge variant="muted">pendente</Badge>
  if (estado === 'validando') return (
    <span className="inline-flex items-center gap-1.5 text-[11px] text-[hsl(var(--brand-700))] font-semibold uppercase tracking-[0.04em]">
      <Loader2 className="size-3 animate-spin" /> validando
    </span>
  )
  if (estado === 'valido') return <Badge variant="success" comDot>válido</Badge>
  if (estado === 'invalido') return <Badge variant="destructive" comDot>com erros</Badge>
  return <Badge variant="destructive" comDot>falhou</Badge>
}

function DialogDetalhes({
  item, onFechar,
}: {
  item: { link: LinkExtraido; r: ResultadoLink } | null
  onFechar: () => void
}) {
  return (
    <Dialog open={item !== null} onOpenChange={(open) => !open && onFechar()}>
      <DialogContent className="sm:max-w-[680px] max-h-[80vh]">
        <DialogHeader>
          <div className={cn(
            'flex size-10 items-center justify-center rounded-[10px] mb-1',
            item?.r.estado === 'valido'
              ? 'bg-[hsl(var(--warning-50))] text-[hsl(var(--warning-500))]'
              : 'bg-[hsl(var(--error-50))] text-[hsl(var(--error-500))]',
          )}>
            {item?.r.estado === 'valido' ? <AlertTriangle className="size-5" /> : <XCircle className="size-5" />}
          </div>
          <DialogTitle>{item?.link.rotulo}</DialogTitle>
          <DialogDescription>
            {item?.r.estado === 'valido' && 'Planilha válida — apenas avisos abaixo.'}
            {item?.r.estado === 'invalido' && 'Planilha tem erros que bloqueiam a importação.'}
            {item?.r.estado === 'erro' && 'Falha ao buscar a planilha. Veja a mensagem abaixo.'}
          </DialogDescription>
        </DialogHeader>

        {item && (
          <div className="space-y-3 overflow-auto">
            <a
              href={item.link.urlOriginal}
              target="_blank"
              rel="noreferrer"
              className="text-[12px] text-[hsl(var(--brand-700))] hover:underline inline-flex items-center gap-1 break-all"
            >
              <ExternalLink className="size-3 shrink-0" />
              <span className="truncate">{item.link.urlOriginal}</span>
            </a>

            {item.r.estado === 'erro' && item.r.mensagemErro && (
              <pre className="font-mono text-[12px] leading-relaxed bg-[hsl(var(--neutral-50))] border border-border rounded-md p-3 whitespace-pre-wrap break-words text-[hsl(var(--error-700))]">
{item.r.mensagemErro}
              </pre>
            )}

            {item.r.resultado && item.r.resultado.erros.length > 0 && (
              <div>
                <div className="text-[11px] uppercase tracking-[0.06em] font-semibold text-muted-foreground mb-1.5">
                  {item.r.resultado.erros.length} {item.r.resultado.erros.length === 1 ? 'erro' : 'erros'}
                </div>
                <div className="border border-[hsl(var(--error-100))] rounded-md overflow-hidden">
                  <table className="w-full text-[12.5px]">
                    <thead>
                      <tr className="bg-[hsl(var(--error-50))]">
                        <th className="px-2.5 py-1.5 text-left text-[10px] uppercase font-semibold text-[hsl(var(--error-700))]">Local</th>
                        <th className="px-2.5 py-1.5 text-left text-[10px] uppercase font-semibold text-[hsl(var(--error-700))]">Linha</th>
                        <th className="px-2.5 py-1.5 text-left text-[10px] uppercase font-semibold text-[hsl(var(--error-700))]">Campo</th>
                        <th className="px-2.5 py-1.5 text-left text-[10px] uppercase font-semibold text-[hsl(var(--error-700))]">Mensagem</th>
                      </tr>
                    </thead>
                    <tbody>
                      {item.r.resultado.erros.map((e, idx) => (
                        <tr key={idx} className="border-t border-[hsl(var(--error-100))]">
                          <td className="px-2.5 py-1.5"><Badge variant="outline">{e.local}</Badge></td>
                          <td className="px-2.5 py-1.5 font-mono text-[11px] tabular-nums">{e.linha ?? '—'}</td>
                          <td className="px-2.5 py-1.5 font-mono text-[11px]">{e.campo}</td>
                          <td className="px-2.5 py-1.5">{e.mensagem}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )}

            {item.r.resultado && item.r.resultado.avisos.length > 0 && (
              <div>
                <div className="text-[11px] uppercase tracking-[0.06em] font-semibold text-muted-foreground mb-1.5">
                  {item.r.resultado.avisos.length} {item.r.resultado.avisos.length === 1 ? 'aviso' : 'avisos'}
                </div>
                <div className="border border-[hsl(var(--warning-100))] rounded-md overflow-hidden">
                  <table className="w-full text-[12.5px]">
                    <thead>
                      <tr className="bg-[hsl(var(--warning-50))]">
                        <th className="px-2.5 py-1.5 text-left text-[10px] uppercase font-semibold text-[hsl(var(--warning-700))]">Local</th>
                        <th className="px-2.5 py-1.5 text-left text-[10px] uppercase font-semibold text-[hsl(var(--warning-700))]">Linha</th>
                        <th className="px-2.5 py-1.5 text-left text-[10px] uppercase font-semibold text-[hsl(var(--warning-700))]">Mensagem</th>
                      </tr>
                    </thead>
                    <tbody>
                      {item.r.resultado.avisos.map((a, idx) => (
                        <tr key={idx} className="border-t border-[hsl(var(--warning-100))]">
                          <td className="px-2.5 py-1.5"><Badge variant="outline">{a.local}</Badge></td>
                          <td className="px-2.5 py-1.5 font-mono text-[11px] tabular-nums">{a.linha ?? '—'}</td>
                          <td className="px-2.5 py-1.5">{a.mensagem}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )}
          </div>
        )}

        <DialogFooter>
          <Button variant="secondary" onClick={onFechar}>Fechar</Button>
          {item?.link.urlOriginal && (
            <Button asChild>
              <a href={item.link.urlOriginal} target="_blank" rel="noreferrer">
                <ExternalLink /> Abrir planilha
              </a>
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

/**
 * Extrai todos os links Google Sheets do texto. Para cada um, pega o trecho da mesma linha
 * antes da URL como rótulo (limpando separadores como ":", "-", "—"). Faz dedupe por idPlanilha.
 * Tolera erros comuns de digitação ("htps://" no lugar de "https://").
 */
function extrairLinks(texto: string): LinkExtraido[] {
  const linhas = texto.split(/\r?\n/)
  const lista: LinkExtraido[] = []
  const vistos = new Set<string>()

  for (const linha of linhas) {
    PADRAO_SHEETS_URL.lastIndex = 0
    let m: RegExpExecArray | null
    while ((m = PADRAO_SHEETS_URL.exec(linha)) !== null) {
      const urlBruta = m[0]
      const idPlanilha = m[1]
      if (vistos.has(idPlanilha)) continue
      vistos.add(idPlanilha)

      const url = urlBruta.startsWith('htps://') ? 'https://' + urlBruta.slice(7) : urlBruta
      const antesUrl = linha.substring(0, m.index)
      const rotulo = antesUrl
        .trim()
        .replace(/[:\-–—|]\s*$/, '')
        .trim() || `Link ${lista.length + 1}`

      lista.push({ rotulo, url, urlOriginal: urlBruta, idPlanilha })
    }
  }

  return lista
}
