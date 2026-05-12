import { useEffect, useState } from 'react'
import {
  AlertTriangle, CheckCircle2, ExternalLink, Loader2, RefreshCw, XCircle,
} from 'lucide-react'
import { PageHeader } from '@/components/comum/PageHeader'
import { Card, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Alert, AlertDescription } from '@/components/ui/alert'
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'
import { EmptyState } from '@/components/comum/EmptyState'
import { importacaoApi } from '@/features/importacao/importacaoApi'
import type { HistoricoImportacao } from '@/features/importacao/tipos'
import { cn, formatarBrl, formatarBrlCompacto, formatarDataHora } from '@/lib/utils'

export function Historico() {
  const [linhas, setLinhas] = useState<HistoricoImportacao[]>([])
  const [carregando, setCarregando] = useState(true)
  const [erro, setErro] = useState<string | null>(null)
  const [erroDetalhe, setErroDetalhe] = useState<HistoricoImportacao | null>(null)

  async function carregar() {
    try {
      setErro(null)
      const dados = await importacaoApi.historico(200)
      setLinhas(dados)
    } catch (e) {
      setErro(e instanceof Error ? e.message : String(e))
    } finally {
      setCarregando(false)
    }
  }

  useEffect(() => {
    void carregar()
    const id = setInterval(carregar, 8000)
    return () => clearInterval(id)
  }, [])

  const totalSucesso = linhas.filter((l) => l.sucesso).length
  const totalFalha = linhas.length - totalSucesso

  return (
    <div className="anim-rise">
      <PageHeader
        titulo="Histórico de importações"
        descricao="Cada importação em lote registrada — sucessos e falhas. Clique no link para abrir a planilha original e no DFD para o portal."
        acoes={
          <Button variant="secondary" onClick={() => void carregar()}>
            {carregando ? <Loader2 className="animate-spin" /> : <RefreshCw />}
            Atualizar
          </Button>
        }
      />

      {erro && (
        <Alert variant="destructive" className="mb-4">
          <AlertTriangle />
          <AlertDescription>{erro}</AlertDescription>
        </Alert>
      )}

      <Card>
        <CardHeader>
          <div>
            <CardTitle>Importações registradas</CardTitle>
            <CardDescription>
              {linhas.length === 0
                ? 'Nenhuma importação registrada ainda.'
                : `${linhas.length} ${linhas.length === 1 ? 'registro' : 'registros'} · ${totalSucesso} ${totalSucesso === 1 ? 'sucesso' : 'sucessos'} · ${totalFalha} ${totalFalha === 1 ? 'falha' : 'falhas'}`}
            </CardDescription>
          </div>
        </CardHeader>

        {linhas.length === 0 ? (
          <EmptyState
            icon={ExternalLink}
            titulo="Sem histórico ainda"
            descricao="Assim que você importar uma planilha por link, ela aparece aqui — bem-sucedida ou não."
          />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-[13px]">
              <thead>
                <tr className="text-left">
                  <Th className="w-[90px]">Status</Th>
                  <Th className="w-[110px]">DFD</Th>
                  <Th>Objeto / Planilha</Th>
                  <Th className="w-[80px] text-right">Itens</Th>
                  <Th className="w-[120px] text-right">Valor</Th>
                  <Th className="w-[150px]">Importado em</Th>
                  <Th className="w-[120px]">Importado por</Th>
                  <Th className="w-[110px] text-right">Ações</Th>
                </tr>
              </thead>
              <tbody>
                {linhas.map((l) => (
                  <tr key={l.id} className="border-b border-[hsl(var(--neutral-100))] last:border-b-0 hover:bg-[hsl(var(--neutral-25))]">
                    <Td>
                      {l.sucesso ? (
                        <Badge variant="success" comDot>OK</Badge>
                      ) : (
                        <Badge variant="destructive" comDot>FALHA</Badge>
                      )}
                    </Td>
                    <Td>
                      {l.sucesso && l.numeroDfd > 0 ? (
                        <a
                          href={urlDfdCompras(l.idArtefato, l.numeroDfd, l.anoDfd)}
                          target="_blank"
                          rel="noreferrer"
                          className="font-mono text-[13px] text-[hsl(var(--brand-700))] font-semibold hover:underline inline-flex items-center gap-1"
                          title="Abrir no Compras.gov.br"
                        >
                          #{l.numeroDfd}/{l.anoDfd}
                          <ExternalLink className="size-3 opacity-60" />
                        </a>
                      ) : (
                        <span className="text-muted-foreground">—</span>
                      )}
                    </Td>
                    <Td>
                      <div className="max-w-[460px]">
                        {l.descricao && (
                          <div className="text-[13px] text-foreground font-medium truncate" title={l.descricao}>
                            {l.descricao}
                          </div>
                        )}
                        <a
                          href={l.urlOriginal}
                          target="_blank"
                          rel="noreferrer"
                          className="text-[12px] text-muted-foreground hover:text-[hsl(var(--brand-600))] inline-flex items-center gap-1 truncate max-w-full"
                          title={l.urlOriginal}
                        >
                          <ExternalLink className="size-3 shrink-0 opacity-60" />
                          <span className="truncate">{l.urlOriginal}</span>
                        </a>
                      </div>
                    </Td>
                    <Td className="text-right font-mono tabular-nums">
                      {l.totalMateriais.toLocaleString('pt-BR')}
                    </Td>
                    <Td className="text-right font-mono tabular-nums" title={formatarBrl(l.valorTotal)}>
                      {formatarBrlCompacto(l.valorTotal)}
                    </Td>
                    <Td className="text-[12px] text-muted-foreground">
                      {formatarDataHora(l.importadaEm)}
                    </Td>
                    <Td>
                      {l.usuarioLogin ? (
                        <span className="font-mono text-[12px] text-foreground">{l.usuarioLogin}</span>
                      ) : (
                        <span className="text-muted-foreground">—</span>
                      )}
                    </Td>
                    <Td className="text-right">
                      {!l.sucesso && (
                        <Button
                          variant="destructive-outline"
                          size="xs"
                          onClick={() => setErroDetalhe(l)}
                        >
                          <XCircle /> Ver erro
                        </Button>
                      )}
                      {l.sucesso && (
                        <Button asChild variant="ghost" size="xs">
                          <a
                            href={urlDfdCompras(l.idArtefato, l.numeroDfd, l.anoDfd)}
                            target="_blank"
                            rel="noreferrer"
                          >
                            Abrir <ExternalLink />
                          </a>
                        </Button>
                      )}
                    </Td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      <DialogErroDetalhe
        registro={erroDetalhe}
        onFechar={() => setErroDetalhe(null)}
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

function Td({ children, className, title }: { children: React.ReactNode; className?: string; title?: string }) {
  return <td className={cn('px-3.5 py-3 align-middle', className)} title={title}>{children}</td>
}

function DialogErroDetalhe({
  registro, onFechar,
}: {
  registro: HistoricoImportacao | null
  onFechar: () => void
}) {
  return (
    <Dialog open={registro !== null} onOpenChange={(open) => !open && onFechar()}>
      <DialogContent className="sm:max-w-[640px]">
        <DialogHeader>
          <div className="flex size-10 items-center justify-center rounded-[10px] bg-[hsl(var(--error-50))] text-[hsl(var(--error-500))] mb-1">
            <XCircle className="size-5" />
          </div>
          <DialogTitle>Detalhes da falha</DialogTitle>
          <DialogDescription>
            A importação foi interrompida. O DFD pode não ter sido criado, ou pode ter sido criado parcialmente — verifique no Compras.gov.br.
          </DialogDescription>
        </DialogHeader>

        {registro && (
          <div className="space-y-4">
            <dl className="grid grid-cols-[120px_1fr] gap-y-2.5 gap-x-4 text-[13px]">
              <Linha rotulo="Importado em" valor={formatarDataHora(registro.importadaEm)} />
              {registro.descricao && (
                <Linha rotulo="Objeto" valor={<span className="font-medium">{registro.descricao}</span>} />
              )}
              {registro.usuarioLogin && (
                <Linha rotulo="Importado por" valor={<span className="font-mono">{registro.usuarioLogin}</span>} />
              )}
              <Linha
                rotulo="Planilha"
                valor={
                  <a
                    href={registro.urlOriginal}
                    target="_blank"
                    rel="noreferrer"
                    className="text-[hsl(var(--brand-700))] hover:underline inline-flex items-center gap-1 break-all"
                  >
                    <ExternalLink className="size-3 shrink-0" />
                    <span className="truncate">{registro.urlOriginal}</span>
                  </a>
                }
              />
              <Linha rotulo="id da planilha" valor={<span className="font-mono">{registro.idPlanilha}</span>} />
              <Linha rotulo="id da execução" valor={<span className="font-mono">{registro.idExecucao}</span>} />
              {registro.numeroDfd > 0 && (
                <Linha
                  rotulo="DFD criado"
                  valor={
                    <a
                      href={urlDfdCompras(registro.idArtefato, registro.numeroDfd, registro.anoDfd)}
                      target="_blank"
                      rel="noreferrer"
                      className="font-mono text-[hsl(var(--brand-700))] font-semibold hover:underline inline-flex items-center gap-1"
                    >
                      #{registro.numeroDfd}/{registro.anoDfd}
                      <ExternalLink className="size-3" />
                    </a>
                  }
                />
              )}
              <Linha rotulo="Materiais previstos" valor={<span className="font-mono tabular-nums">{registro.totalMateriais}</span>} />
              {registro.linhaErro != null && (
                <Linha
                  rotulo="Linha do erro"
                  valor={
                    <span className="inline-flex items-center gap-2">
                      <Badge variant="destructive">linha {registro.linhaErro}</Badge>
                      <span className="text-muted-foreground text-[12px]">na aba "Materiais"</span>
                    </span>
                  }
                />
              )}
            </dl>

            <div>
              <div className="text-[11px] uppercase tracking-[0.06em] font-semibold text-muted-foreground mb-1.5">
                Mensagem de erro
              </div>
              <pre className="font-mono text-[12px] leading-relaxed bg-[hsl(var(--neutral-50))] border border-border rounded-md p-3 whitespace-pre-wrap break-words text-[hsl(var(--error-700))] max-h-[260px] overflow-auto">
{registro.mensagemErro ?? '(sem mensagem)'}
              </pre>
            </div>

            <Alert variant="info">
              <CheckCircle2 />
              <AlertDescription>
                Você pode reimportar a mesma planilha — falhas anteriores não bloqueiam nova tentativa.
              </AlertDescription>
            </Alert>
          </div>
        )}

        <DialogFooter>
          <Button variant="secondary" onClick={onFechar}>Fechar</Button>
          {registro?.urlOriginal && (
            <Button asChild>
              <a href={registro.urlOriginal} target="_blank" rel="noreferrer">
                <ExternalLink /> Abrir planilha
              </a>
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

function Linha({ rotulo, valor }: { rotulo: string; valor: React.ReactNode }) {
  return (
    <>
      <dt className="text-[10px] uppercase tracking-[0.06em] font-semibold text-muted-foreground self-center">{rotulo}</dt>
      <dd className="min-w-0 truncate">{valor}</dd>
    </>
  )
}

/** URL para abrir o DFD direto no portal Compras.gov.br */
function urlDfdCompras(idArtefato: number, numeroDfd?: number, anoDfd?: number): string {
  const base = `https://cnetmobile.estaleiro.serpro.gov.br/comprasnet-artefatos-web/artefatos/edit/${idArtefato}`
  if (numeroDfd && anoDfd) {
    const artefato = encodeURIComponent(`${numeroDfd}/${anoDfd}`)
    return `${base}?artefato=${artefato}&tipo=DFD`
  }
  return `${base}?tipo=DFD`
}
