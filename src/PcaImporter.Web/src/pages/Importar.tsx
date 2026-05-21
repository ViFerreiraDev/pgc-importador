import { useEffect, useRef, useState } from 'react'
import {
  Upload, ExternalLink, Play, FileSpreadsheet, AlertTriangle, CheckCircle2,
  Loader2, Info, Plus, Link as LinkIcon, History, List, Scale,
} from 'lucide-react'
import { PageHeader } from '@/components/comum/PageHeader'
import { LinkCodigoBr } from '@/components/comum/LinkCodigoBr'
import { formatarBanda } from '@/features/importacao/formatDivergencia'
import { Card, CardHeader, CardTitle, CardDescription, CardFooter } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'
import {
  importacaoApi, ImportacaoDuplicadaError,
} from '@/features/importacao/importacaoApi'
import type {
  EstadoImportacao, HistoricoImportacao, ResultadoValidacao, StatusImportacao,
} from '@/features/importacao/tipos'
import { cn, formatarDataHora } from '@/lib/utils'

// Termina em /copy — abre o diálogo "Fazer cópia" do Google Sheets,
// para o usuário não editar a planilha-mestre.
const URL_PLANILHA_EXEMPLO =
  'https://docs.google.com/spreadsheets/d/1_GbinihW6ueQ90TdbE0uTFk4kIpLYElJNPEAVf3kqyU/copy?usp=sharing'

const corBadgeEstado: Record<EstadoImportacao, 'success' | 'warning' | 'destructive' | 'muted' | 'brand'> = {
  Pendente: 'muted',
  Executando: 'brand',
  Concluida: 'success',
  Falhou: 'destructive',
  Cancelada: 'muted',
}

type Origem = 'arquivo' | 'link'

export function Importar() {
  const [origem, setOrigem] = useState<Origem>('link')
  const [arquivo, setArquivo] = useState<File | null>(null)
  const [url, setUrl] = useState('')
  const [validacao, setValidacao] = useState<ResultadoValidacao | null>(null)
  const [validando, setValidando] = useState(false)
  const [iniciando, setIniciando] = useState(false)
  const [erroGeral, setErroGeral] = useState<string | null>(null)
  const [duplicado, setDuplicado] = useState<HistoricoImportacao | null>(null)
  const [idAtivo, setIdAtivo] = useState<string | null>(null)
  const [status, setStatus] = useState<StatusImportacao | null>(null)
  const inputRef = useRef<HTMLInputElement>(null)

  function trocarOrigem(o: Origem) {
    setOrigem(o)
    setValidacao(null)
    setErroGeral(null)
  }

  async function aoValidar() {
    setValidando(true)
    setErroGeral(null)
    try {
      const r = origem === 'arquivo'
        ? (arquivo ? await importacaoApi.validar(arquivo) : null)
        : await importacaoApi.validarLink(url.trim())
      if (r) setValidacao(r)
    } catch (e) {
      setErroGeral(e instanceof Error ? e.message : String(e))
    } finally {
      setValidando(false)
    }
  }

  async function executarIniciar(confirmar = false) {
    setIniciando(true)
    setErroGeral(null)
    try {
      const { id } = origem === 'arquivo'
        ? (arquivo ? await importacaoApi.iniciar(arquivo) : { id: '' })
        : await importacaoApi.iniciarLink(url.trim(), confirmar)
      if (id) {
        setIdAtivo(id)
        setStatus(null)
        setDuplicado(null)
      }
    } catch (e) {
      if (e instanceof ImportacaoDuplicadaError) {
        setDuplicado(e.anterior)
      } else {
        setErroGeral(e instanceof Error ? e.message : String(e))
      }
    } finally {
      setIniciando(false)
    }
  }

  function aoLimpar() {
    setArquivo(null)
    setUrl('')
    setValidacao(null)
    setErroGeral(null)
    setDuplicado(null)
    setIdAtivo(null)
    setStatus(null)
    if (inputRef.current) inputRef.current.value = ''
  }

  useEffect(() => {
    if (!idAtivo) return
    let parou = false
    async function tick() {
      try {
        const s = await importacaoApi.status(idAtivo!)
        if (parou) return
        setStatus(s)
        if (s.estado === 'Concluida' || s.estado === 'Falhou' || s.estado === 'Cancelada') return
      } catch {
      }
      if (!parou) setTimeout(tick, 1000)
    }
    void tick()
    return () => { parou = true }
  }, [idAtivo])

  const temFonte = origem === 'arquivo' ? !!arquivo : url.trim().length > 0
  const podeValidar = temFonte && !validando
  const podeIniciar = temFonte && validacao?.valido === true && !iniciando

  return (
    <div className="anim-rise">
      <PageHeader
        titulo="Importar em lote"
        descricao="Envie uma planilha (Google Sheets ou Excel) com 1 DFD e seus materiais. O sistema valida, cria o DFD, salva todas as etapas e adiciona os itens automaticamente."
        acoes={
          <Button asChild variant="secondary">
            <a href={URL_PLANILHA_EXEMPLO} target="_blank" rel="noreferrer">
              <ExternalLink /> Abrir planilha de exemplo
            </a>
          </Button>
        }
      />

      {!idAtivo && (
        <Card className="anim-rise anim-rise-1">
          <CardHeader>
            <div className="min-w-0 flex-1 basis-[280px]">
              <CardTitle><Upload />1. Selecione a planilha</CardTitle>
              <CardDescription>
                Cole o link de uma planilha Google Sheets pública, ou faça upload de um arquivo .xlsx.
              </CardDescription>
            </div>
            <Segmented>
              <SegBotao ativo={origem === 'link'} onClick={() => trocarOrigem('link')}>
                <LinkIcon className="size-3.5" /> Link Google Sheets
              </SegBotao>
              <SegBotao ativo={origem === 'arquivo'} onClick={() => trocarOrigem('arquivo')}>
                <FileSpreadsheet className="size-3.5" /> Upload .xlsx
              </SegBotao>
            </Segmented>
          </CardHeader>

          <div className="p-[18px]">
            {origem === 'link' ? (
              <div className="flex flex-col gap-1.5">
                <label htmlFor="urlSheet" className="text-[12px] font-medium text-[hsl(var(--neutral-700))]">
                  URL pública da planilha
                </label>
                <Input
                  id="urlSheet"
                  type="url"
                  placeholder="https://docs.google.com/spreadsheets/d/..."
                  value={url}
                  onChange={(e) => { setUrl(e.target.value); setValidacao(null); setErroGeral(null); setDuplicado(null) }}
                  className="font-mono text-[13px]"
                />
                <span className="text-[11px] text-muted-foreground">A planilha precisa estar aberta para leitura pública.</span>

                <Alert variant="info" className="mt-3">
                  <Info />
                  <div>
                    <AlertTitle>Como deixar a planilha pública</AlertTitle>
                    <AlertDescription>
                      Compartilhar → <strong>Acesso geral</strong> → Qualquer pessoa com o link → <strong>Leitor</strong>.
                      O sistema só lê; nunca escreve na planilha.
                    </AlertDescription>
                  </div>
                </Alert>

                <Alert variant="default" className="mt-2">
                  <Info />
                  <div>
                    <AlertTitle>Aba "Materiais" — colunas esperadas</AlertTitle>
                    <AlertDescription>
                      Cabeçalhos reconhecidos:{' '}
                      <code className="font-mono text-[12px] bg-[hsl(var(--neutral-50))] px-1 py-0.5 rounded">codigo</code>,{' '}
                      <code className="font-mono text-[12px] bg-[hsl(var(--neutral-50))] px-1 py-0.5 rounded">siglaUnidadeFornecimento</code>,{' '}
                      <code className="font-mono text-[12px] bg-[hsl(var(--neutral-50))] px-1 py-0.5 rounded">quantidade</code>,{' '}
                      <code className="font-mono text-[12px] bg-[hsl(var(--neutral-50))] px-1 py-0.5 rounded">valorUnitario</code>.
                      A ordem das colunas não importa — o sistema mapeia pelo nome.
                      Colunas extras (ex.:{' '}
                      <code className="font-mono text-[12px] bg-[hsl(var(--neutral-50))] px-1 py-0.5 rounded">descritivo</code>
                      , para conferência visual) são <strong>ignoradas silenciosamente</strong>.
                    </AlertDescription>
                  </div>
                </Alert>
              </div>
            ) : (
              <>
                {arquivo ? (
                  <div className="flex items-center gap-3 px-3.5 py-3 bg-[hsl(var(--neutral-50))] border border-border rounded-lg">
                    <FileSpreadsheet className="size-[22px] text-[hsl(var(--accent-500))] shrink-0" />
                    <div className="flex-1 min-w-0">
                      <div className="font-semibold text-[13px] truncate">{arquivo.name}</div>
                      <div className="font-mono text-[11px] text-muted-foreground">{(arquivo.size / 1024).toFixed(1)} KB · enviado agora</div>
                    </div>
                    <Button variant="secondary" size="sm" onClick={() => setArquivo(null)}>Trocar arquivo</Button>
                  </div>
                ) : (
                  <div
                    className="border-[1.5px] border-dashed border-[hsl(var(--neutral-300))] rounded-[10px] py-8 px-5 text-center bg-[hsl(var(--neutral-50))] cursor-pointer transition-colors hover:bg-[hsl(var(--brand-25))] hover:border-[hsl(var(--brand-300))]"
                    onClick={() => inputRef.current?.click()}
                  >
                    <Upload className="size-7 text-muted-foreground mx-auto mb-2" />
                    <div className="text-[14px] font-semibold mb-1">Arraste o arquivo .xlsx aqui ou clique para selecionar</div>
                    <div className="text-[12px] text-muted-foreground">Limite 20 MB · uma planilha por importação</div>
                  </div>
                )}
                <input
                  ref={inputRef}
                  type="file"
                  accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                  className="hidden"
                  onChange={(e) => setArquivo(e.target.files?.[0] ?? null)}
                />
              </>
            )}

            {erroGeral && (
              <Alert variant="destructive" className="mt-3">
                <AlertTriangle />
                <AlertDescription className="whitespace-pre-line">{erroGeral}</AlertDescription>
              </Alert>
            )}
          </div>

          <CardFooter>
            <a href={URL_PLANILHA_EXEMPLO} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1.5 text-[13px] text-muted-foreground hover:text-foreground transition-colors">
              <ExternalLink className="size-3.5" /> Abrir planilha de exemplo
            </a>
            <div className="flex-1" />
            <Button variant="secondary" onClick={aoValidar} disabled={!podeValidar}>
              {validando ? <Loader2 className="animate-spin" /> : <CheckCircle2 />}
              {validando ? 'Validando…' : 'Validar'}
            </Button>
            <Button onClick={() => void executarIniciar(false)} disabled={!podeIniciar}>
              {iniciando ? <Loader2 className="animate-spin" /> : <Play />}
              {iniciando ? 'Iniciando…' : 'Iniciar importação'}
            </Button>
          </CardFooter>
        </Card>
      )}

      {!idAtivo && validacao && (
        <div className="mt-4">
          <RelatorioValidacao r={validacao} />
        </div>
      )}

      {idAtivo && status && (
        <ProgressoExecucao status={status} onResetar={aoLimpar} />
      )}
      {idAtivo && !status && (
        <Card className="mt-4 p-10">
          <div className="flex items-center gap-3 justify-center text-sm text-muted-foreground">
            <Loader2 className="size-4 animate-spin" />
            Aguardando primeiro status…
          </div>
        </Card>
      )}

      <DialogDuplicado
        anterior={duplicado}
        importando={iniciando}
        onCancelar={() => setDuplicado(null)}
        onConfirmar={() => void executarIniciar(true)}
      />
    </div>
  )
}

function Segmented({ children }: { children: React.ReactNode }) {
  return (
    <div className="inline-flex p-[3px] gap-0.5 bg-[hsl(var(--neutral-50))] border border-[hsl(var(--neutral-100))] rounded-lg">
      {children}
    </div>
  )
}

function SegBotao({ ativo, onClick, children }: { ativo: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        'inline-flex items-center gap-1.5 px-3.5 py-1.5 rounded-md text-[13px] font-medium transition-colors',
        ativo
          ? 'bg-surface text-foreground shadow-[var(--shadow-sm)]'
          : 'bg-transparent text-muted-foreground hover:text-foreground',
      )}
    >
      {children}
    </button>
  )
}

function DialogDuplicado({
  anterior, importando, onCancelar, onConfirmar,
}: {
  anterior: HistoricoImportacao | null
  importando: boolean
  onCancelar: () => void
  onConfirmar: () => void
}) {
  return (
    <Dialog open={anterior !== null} onOpenChange={(open) => !open && !importando && onCancelar()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <div className="flex size-10 items-center justify-center rounded-[10px] bg-[hsl(var(--warning-50))] text-[hsl(var(--warning-500))] mb-1">
            <History className="size-5" />
          </div>
          <DialogTitle>Esta planilha já foi importada</DialogTitle>
          <DialogDescription>
            Confirme antes de criar um DFD novo no Compras com os mesmos dados.
          </DialogDescription>
        </DialogHeader>

        {anterior && (
          <dl className="grid grid-cols-[110px_1fr] gap-y-3 gap-x-4 py-2 text-[13px]">
            <dt className="text-[10px] uppercase tracking-[0.06em] font-semibold text-muted-foreground self-center">DFD anterior</dt>
            <dd className="font-mono text-[hsl(var(--brand-700))] font-semibold">#{anterior.numeroDfd}/{anterior.anoDfd}</dd>
            <dt className="text-[10px] uppercase tracking-[0.06em] font-semibold text-muted-foreground self-center">idArtefato</dt>
            <dd className="font-mono">{anterior.idArtefato}</dd>
            <dt className="text-[10px] uppercase tracking-[0.06em] font-semibold text-muted-foreground self-center">Importado em</dt>
            <dd>{formatarDataHora(anterior.importadaEm)}</dd>
            <dt className="text-[10px] uppercase tracking-[0.06em] font-semibold text-muted-foreground self-center">Materiais</dt>
            <dd className="font-mono tabular-nums">{anterior.totalMateriais}</dd>
          </dl>
        )}

        <Alert variant="warning">
          <AlertTriangle />
          <AlertDescription>
            Se confirmar, um <strong>novo DFD será criado</strong> no Compras com os mesmos dados.
          </AlertDescription>
        </Alert>

        <DialogFooter>
          <Button variant="secondary" onClick={onCancelar} disabled={importando}>Cancelar</Button>
          <Button onClick={onConfirmar} disabled={importando}>
            {importando && <Loader2 className="animate-spin" />}
            {importando ? 'Importando…' : 'Importar mesmo assim'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

function RelatorioValidacao({ r }: { r: ResultadoValidacao }) {
  if (r.valido) {
    return (
      <div className="flex flex-col gap-3 anim-rise">
        <Alert variant="success">
          <CheckCircle2 />
          <div>
            <AlertTitle>
              Arquivo válido — {r.totalMateriais} {r.totalMateriais === 1 ? 'material pronto' : 'materiais prontos'} para importar
              {r.avisos.length > 0 && ` (${r.avisos.length} aviso${r.avisos.length === 1 ? '' : 's'})`}
            </AlertTitle>
            <AlertDescription>
              Estrutura compatível: 1 DFD com {r.totalMateriais} {r.totalMateriais === 1 ? 'linha' : 'linhas'} de materiais.
            </AlertDescription>
          </div>
        </Alert>
        {r.duplicado && r.anterior && (
          <Alert variant="warning">
            <History />
            <div>
              <AlertTitle>Esta planilha já foi importada</AlertTitle>
              <AlertDescription>
                Em {formatarDataHora(r.anterior.importadaEm)} como <strong>DFD #{r.anterior.numeroDfd}/{r.anterior.anoDfd}</strong>.
                Ao iniciar, vamos perguntar se quer mesmo importar de novo.
              </AlertDescription>
            </div>
          </Alert>
        )}
        {(r.divergencias?.length ?? 0) > 0 && (
          <Card className="border-[hsl(20_85%_55%/0.45)] bg-[hsl(20_85%_55%/0.04)]">
            <CardHeader className="border-b border-[hsl(20_85%_55%/0.25)]">
              <div>
                <CardTitle className="text-[hsl(20_85%_35%)]">
                  <Scale className="!text-[hsl(20_85%_45%)]" />
                  Divergência com a base histórica
                </CardTitle>
                <CardDescription className="text-[hsl(20_45%_30%)]">
                  Não bloqueia a importação, mas vale conferir antes de subir — os valores estão fora do padrão histórico do Compras.gov.br.
                </CardDescription>
              </div>
              <Badge className="bg-[hsl(20_85%_55%/0.15)] text-[hsl(20_85%_30%)] border border-[hsl(20_85%_55%/0.35)]">
                <span className="size-1.5 rounded-full bg-[hsl(20_85%_50%)]" />
                {r.divergencias!.length} {r.divergencias!.length === 1 ? 'divergência' : 'divergências'}
              </Badge>
            </CardHeader>
            <div className="overflow-x-auto">
              <table className="w-full text-[13px]">
                <thead>
                  <tr className="text-left">
                    <th className="px-3.5 py-2.5 text-[11px] uppercase tracking-[0.05em] font-semibold text-[hsl(20_45%_35%)] bg-[hsl(20_85%_55%/0.06)] border-b border-[hsl(20_85%_55%/0.2)] w-20">Linha</th>
                    <th className="px-3.5 py-2.5 text-[11px] uppercase tracking-[0.05em] font-semibold text-[hsl(20_45%_35%)] bg-[hsl(20_85%_55%/0.06)] border-b border-[hsl(20_85%_55%/0.2)] w-28">Código</th>
                    <th className="px-3.5 py-2.5 text-[11px] uppercase tracking-[0.05em] font-semibold text-[hsl(20_45%_35%)] bg-[hsl(20_85%_55%/0.06)] border-b border-[hsl(20_85%_55%/0.2)] w-20">Campo</th>
                    <th className="px-3.5 py-2.5 text-[11px] uppercase tracking-[0.05em] font-semibold text-[hsl(20_45%_35%)] bg-[hsl(20_85%_55%/0.06)] border-b border-[hsl(20_85%_55%/0.2)] w-32 text-right">Planilha</th>
                    <th className="px-3.5 py-2.5 text-[11px] uppercase tracking-[0.05em] font-semibold text-[hsl(20_45%_35%)] bg-[hsl(20_85%_55%/0.06)] border-b border-[hsl(20_85%_55%/0.2)] w-32 text-right">Histórico</th>
                    <th className="px-3.5 py-2.5 text-[11px] uppercase tracking-[0.05em] font-semibold text-[hsl(20_45%_35%)] bg-[hsl(20_85%_55%/0.06)] border-b border-[hsl(20_85%_55%/0.2)] w-24 text-right">Δ</th>
                  </tr>
                </thead>
                <tbody>
                  {r.divergencias!.map((d, idx) => (
                    <tr key={idx} className="border-b border-[hsl(20_85%_55%/0.15)] last:border-b-0">
                      <td className="px-3.5 py-3 font-mono text-[12px] text-muted-foreground tabular-nums">{d.linha ?? '—'}</td>
                      <td className="px-3.5 py-3 font-mono text-[12px]"><LinkCodigoBr codigo={d.codigo} /></td>
                      <td className="px-3.5 py-3">
                        <span className="text-[10.5px] uppercase tracking-[0.05em] font-semibold text-[hsl(20_85%_35%)]">
                          {d.tipo === 'preco' ? 'preço' : 'qtd'}
                        </span>
                      </td>
                      <td className="px-3.5 py-3 font-mono text-[12.5px] tabular-nums text-right">
                        {d.tipo === 'preco'
                          ? d.valorPlanilha.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
                          : d.valorPlanilha.toLocaleString('pt-BR', { maximumFractionDigits: 2 })}
                      </td>
                      <td className="px-3.5 py-3 font-mono text-[12.5px] tabular-nums text-right text-muted-foreground">
                        {formatarBanda(d)}
                        <span className="block text-[10px] text-muted-foreground">{d.totalRegistros} registros · {d.siglaReferencia}</span>
                      </td>
                      <td className="px-3.5 py-3 font-mono text-[12.5px] tabular-nums text-right">
                        <span className={d.diferencaPct >= 0 ? 'text-[hsl(20_85%_35%)] font-semibold' : 'text-[hsl(20_45%_35%)]'}>
                          {d.diferencaPct >= 0 ? '+' : '−'}{(Math.abs(d.diferencaPct) * 100).toFixed(0)}%
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Card>
        )}
        {r.avisos.length > 0 && (
          <Card>
            <CardHeader>
              <CardTitle><AlertTriangle />Avisos de normalização</CardTitle>
              <Badge variant="warning"><span className="size-1.5 rounded-full bg-[hsl(var(--warning-500))]" />{r.avisos.length} avisos</Badge>
            </CardHeader>
            <div className="overflow-hidden">
              <table className="w-full text-[13px]">
                <thead>
                  <tr className="text-left">
                    <th className="px-3.5 py-2.5 text-[11px] uppercase tracking-[0.05em] font-semibold text-muted-foreground bg-[hsl(var(--neutral-25))] border-b border-border w-32">Local</th>
                    <th className="px-3.5 py-2.5 text-[11px] uppercase tracking-[0.05em] font-semibold text-muted-foreground bg-[hsl(var(--neutral-25))] border-b border-border w-20">Linha</th>
                    <th className="px-3.5 py-2.5 text-[11px] uppercase tracking-[0.05em] font-semibold text-muted-foreground bg-[hsl(var(--neutral-25))] border-b border-border">Mensagem</th>
                  </tr>
                </thead>
                <tbody>
                  {r.avisos.map((a, idx) => (
                    <tr key={idx} className="border-b border-[hsl(var(--neutral-100))] last:border-b-0">
                      <td className="px-3.5 py-3"><Badge variant="outline">{a.local}</Badge></td>
                      <td className="px-3.5 py-3 font-mono text-[12px] text-muted-foreground tabular-nums">{a.linha ?? '—'}</td>
                      <td className="px-3.5 py-3">{a.mensagem}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Card>
        )}
      </div>
    )
  }
  return (
    <Card className="anim-rise">
      <CardHeader>
        <CardTitle><AlertTriangle />Erros encontrados</CardTitle>
        <Badge variant="destructive"><span className="size-1.5 rounded-full bg-[hsl(var(--error-500))]" />{r.erros.length} {r.erros.length === 1 ? 'erro' : 'erros'}</Badge>
      </CardHeader>
      <table className="w-full text-[13px]">
        <thead>
          <tr className="text-left">
            <th className="px-3.5 py-2.5 text-[11px] uppercase tracking-[0.05em] font-semibold text-muted-foreground bg-[hsl(var(--neutral-25))] border-b border-border w-32">Local</th>
            <th className="px-3.5 py-2.5 text-[11px] uppercase tracking-[0.05em] font-semibold text-muted-foreground bg-[hsl(var(--neutral-25))] border-b border-border w-20">Linha</th>
            <th className="px-3.5 py-2.5 text-[11px] uppercase tracking-[0.05em] font-semibold text-muted-foreground bg-[hsl(var(--neutral-25))] border-b border-border w-44">Campo</th>
            <th className="px-3.5 py-2.5 text-[11px] uppercase tracking-[0.05em] font-semibold text-muted-foreground bg-[hsl(var(--neutral-25))] border-b border-border">Mensagem</th>
          </tr>
        </thead>
        <tbody>
          {r.erros.map((e, idx) => (
            <tr key={idx} className="border-b border-[hsl(var(--neutral-100))] last:border-b-0">
              <td className="px-3.5 py-3"><Badge variant="outline">{e.local}</Badge></td>
              <td className="px-3.5 py-3 font-mono text-[12px] text-muted-foreground tabular-nums">{e.linha ?? '—'}</td>
              <td className="px-3.5 py-3 font-mono text-[12px]">{e.campo}</td>
              <td className="px-3.5 py-3">{e.mensagem}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </Card>
  )
}

function ProgressoExecucao({ status, onResetar }: { status: StatusImportacao; onResetar: () => void }) {
  const pct = status.totalEtapas > 0
    ? Math.min(100, Math.round((status.etapasConcluidas / status.totalEtapas) * 100))
    : 0
  const finalizado = status.estado === 'Concluida' || status.estado === 'Falhou' || status.estado === 'Cancelada'
  const corBarra = status.estado === 'Falhou'
    ? 'bg-[hsl(var(--error-500))]'
    : status.estado === 'Concluida'
      ? 'bg-[hsl(var(--success-500))]'
      : 'bg-[hsl(var(--brand-500))] progress-striped'

  return (
    <div className="flex flex-col gap-4 anim-rise">
      <Card>
        <CardHeader>
          <div className="min-w-0 flex-1 basis-[280px]">
            <CardTitle>
              <Play />Execução
              <Badge variant={corBadgeEstado[status.estado]} size="upper" comDot>{status.estado}</Badge>
              <span className="font-mono text-[11px] text-muted-foreground font-normal ml-1">#{status.id}</span>
            </CardTitle>
            <CardDescription className="font-mono">
              {status.numeroDfd ? `DFD #${status.numeroDfd}/${status.anoDfd} · idArtefato ${status.idArtefatoCriado}` : 'Iniciando…'}
            </CardDescription>
          </div>
          {finalizado && (
            <Button variant="secondary" onClick={onResetar}>
              <Plus /> Nova importação
            </Button>
          )}
        </CardHeader>

        <div className="p-[18px] flex flex-col gap-3.5">
          <div>
            <div className="flex items-baseline justify-between mb-2">
              <div className="flex items-center gap-2">
                {!finalizado && <Loader2 className="size-3.5 animate-spin text-[hsl(var(--brand-500))]" />}
                <strong className="text-[14px]">{status.etapaAtual ?? (finalizado ? 'Concluído' : 'Aguardando')}</strong>
              </div>
              <span className="font-mono text-[12px] text-[hsl(var(--neutral-600))] tabular-nums">
                {status.etapasConcluidas}/{status.totalEtapas} etapas · {pct}%
              </span>
            </div>
            <div className="h-2.5 rounded-full bg-[hsl(var(--neutral-50))] overflow-hidden">
              <div className={cn('h-full rounded-full transition-all duration-300', corBarra)} style={{ width: `${pct}%` }} />
            </div>
          </div>

          <div className="grid grid-cols-2 sm:grid-cols-4 gap-px bg-[hsl(var(--neutral-100))] border border-[hsl(var(--neutral-100))] rounded-lg overflow-hidden">
            <MiniStat rotulo="Materiais" valor={`${status.materiaisAdicionados}/${status.totalMateriais}`} sub="CATMAT" />
            <MiniStat rotulo="Iniciada" valor={hora(status.iniciadaEm)} sub={data(status.iniciadaEm)} />
            <MiniStat rotulo="Concluída" valor={hora(status.concluidaEm)} sub={data(status.concluidaEm)} />
            <MiniStat rotulo="Etapas" valor={`${status.etapasConcluidas}/${status.totalEtapas}`} sub={status.etapaAtual ?? '—'} />
          </div>

          {status.ultimoErro && (
            <Alert variant="destructive">
              <AlertTriangle />
              <AlertDescription className="whitespace-pre-line">{status.ultimoErro}</AlertDescription>
            </Alert>
          )}
          {status.estado === 'Concluida' && (
            <Alert variant="success">
              <CheckCircle2 />
              <div>
                <AlertTitle>
                  Importação concluída · DFD <strong>#{status.numeroDfd}/{status.anoDfd}</strong> está <strong>ATIVO</strong>
                </AlertTitle>
                <AlertDescription>
                  {status.materiaisAdicionados} {status.materiaisAdicionados === 1 ? 'material adicionado' : 'materiais adicionados'}, justificativa salva e responsável vinculado.
                </AlertDescription>
              </div>
            </Alert>
          )}
        </div>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle><List />Eventos</CardTitle>
          <Badge variant="muted" className="font-mono">{status.eventos.length} eventos</Badge>
        </CardHeader>
        <div>
          {status.eventos.length === 0 ? (
            <div className="px-[18px] py-12 text-center text-[13px] text-muted-foreground">Sem eventos ainda.</div>
          ) : (
            status.eventos.slice().reverse().map((e, idx) => {
              const cor = e.tipo === 'Erro' ? 'bg-[hsl(var(--error-500))]'
                : e.tipo === 'Sucesso' || e.tipo === 'Conclusao' ? 'bg-[hsl(var(--success-500))]'
                : 'bg-[hsl(var(--info-500))]'
              return (
                <div key={idx} className="flex items-start gap-3 px-[18px] py-2.5 border-b border-[hsl(var(--neutral-100))] last:border-b-0">
                  <span className={cn('size-2 rounded-full mt-1.5 shrink-0', cor)} />
                  <div className="flex-1 text-[13px]">
                    {e.mensagem}
                    {e.detalhe && (
                      <div className="font-mono text-[11px] text-muted-foreground mt-0.5 break-all">{e.detalhe}</div>
                    )}
                  </div>
                  <span className="font-mono text-[11px] text-muted-foreground shrink-0 tabular-nums">
                    {new Date(e.ocorridoEm).toLocaleTimeString('pt-BR')}
                  </span>
                </div>
              )
            })
          )}
        </div>
      </Card>
    </div>
  )
}

function MiniStat({ rotulo, valor, sub }: { rotulo: string; valor: string; sub: string }) {
  return (
    <div className="bg-surface px-3.5 py-3">
      <div className="text-[10px] uppercase tracking-[0.06em] font-semibold text-muted-foreground">{rotulo}</div>
      <div className="font-mono text-[16px] font-medium mt-1">{valor}</div>
      <div className="font-mono text-[11px] text-muted-foreground mt-0.5">{sub}</div>
    </div>
  )
}

function hora(iso: string | null | undefined): string {
  if (!iso) return '—'
  return new Date(iso).toLocaleTimeString('pt-BR')
}
function data(iso: string | null | undefined): string {
  if (!iso) return '—'
  return new Date(iso).toLocaleDateString('pt-BR')
}
