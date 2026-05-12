import { useState } from 'react'
import { Search, Send, X, Loader2, CheckCircle2, AlertTriangle, Sparkles } from 'lucide-react'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select } from '@/components/ui/select'
import { Badge } from '@/components/ui/badge'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Separator } from '@/components/ui/separator'
import { dfdApi } from './dfdApi'
import { catalogoApi } from '@/features/catalogo/catalogoApi'
import type { ItemCatalogo } from '@/features/catalogo/tipos'
import type { MaterialServicoEntrada } from './tipos'
import { formatarBrl } from '@/lib/utils'

type TipoItem = 'MATERIAL' | 'SERVICO'

interface Quantitativos {
  quantidade: number
  valorUnitario: number
  siglaUnidadeFornecimento: string
  moeda: string
}

const PADRAO_QUANT: Quantitativos = {
  quantidade: 1,
  valorUnitario: 0,
  siglaUnidadeFornecimento: 'UN',
  moeda: 'Real',
}

interface Props {
  onItemAdicionado: () => void
}

export function FormMaterialServico({ onItemAdicionado }: Props) {
  const [tipo, setTipo] = useState<TipoItem>('MATERIAL')
  const [codigo, setCodigo] = useState('630788')
  const [item, setItem] = useState<ItemCatalogo | null>(null)
  const [buscando, setBuscando] = useState(false)
  const [erroBusca, setErroBusca] = useState<string | null>(null)

  const [quant, setQuant] = useState<Quantitativos>(PADRAO_QUANT)

  const [enviando, setEnviando] = useState(false)
  const [erroEnvio, setErroEnvio] = useState<string | null>(null)
  const [sucesso, setSucesso] = useState<string | null>(null)

  const total = quant.quantidade > 0 && quant.valorUnitario > 0 ? quant.quantidade * quant.valorUnitario : 0

  async function aoBuscar() {
    if (!codigo.trim()) {
      setErroBusca('Informe o código.')
      return
    }
    setBuscando(true)
    setErroBusca(null)
    setItem(null)
    setSucesso(null)
    try {
      const r = tipo === 'MATERIAL'
        ? await catalogoApi.consultarMaterial(codigo.trim())
        : await catalogoApi.consultarServico(codigo.trim())
      setItem(r)
    } catch (e) {
      setErroBusca(e instanceof Error ? e.message : String(e))
    } finally {
      setBuscando(false)
    }
  }

  function aoLimpar() {
    setItem(null)
    setErroBusca(null)
    setSucesso(null)
    setQuant(PADRAO_QUANT)
  }

  async function aoEnviar() {
    if (!item) return
    if (quant.quantidade <= 0 || quant.valorUnitario <= 0) {
      setErroEnvio('Quantidade e valor unitário devem ser maiores que zero.')
      return
    }

    const entrada: MaterialServicoEntrada = {
      tipo: item.tipo,
      codigo: item.codigoItem.toString(),
      idClasse: item.codigoClasse,
      nomeClasse: item.nomeClasse,
      idPadraoDescritivo: item.codigoPdm,
      nomePadraoDescritivo: item.nomePdm,
      descricao: item.descricaoItem,
      quantidade: quant.quantidade,
      valorUnitario: quant.valorUnitario,
      moeda: quant.moeda,
      siglaUnidadeFornecimento: quant.siglaUnidadeFornecimento,
    }

    setEnviando(true)
    setErroEnvio(null)
    setSucesso(null)
    try {
      const criado = await dfdApi.adicionarMaterial(entrada)
      setSucesso(`Item #${criado.id} adicionado · total ${formatarBrl(Number(criado.valorTotal))}`)
      onItemAdicionado()
      aoLimpar()
    } catch (e) {
      setErroEnvio(e instanceof Error ? e.message : String(e))
    } finally {
      setEnviando(false)
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Adicionar item ao DFD</CardTitle>
        <CardDescription>
          Pesquise por código CATMAT/CATSER. Os dados do catálogo são preenchidos automaticamente.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-5">
        <div className="grid grid-cols-12 gap-3">
          <div className="col-span-12 sm:col-span-3">
            <Label htmlFor="tipo">Tipo</Label>
            <Select id="tipo" value={tipo} onChange={(e) => { setTipo(e.target.value as TipoItem); setItem(null) }} disabled={buscando} className="mt-1.5">
              <option value="MATERIAL">Material</option>
              <option value="SERVICO">Serviço</option>
            </Select>
          </div>
          <div className="col-span-12 sm:col-span-6">
            <Label htmlFor="codigo">{tipo === 'MATERIAL' ? 'Código CATMAT' : 'Código CATSER'}</Label>
            <Input
              id="codigo"
              className="mt-1.5"
              value={codigo}
              onChange={(e) => setCodigo(e.target.value)}
              onKeyDown={(e) => { if (e.key === 'Enter') void aoBuscar() }}
              disabled={buscando}
              placeholder="ex: 630788"
            />
          </div>
          <div className="col-span-12 sm:col-span-3 sm:flex sm:items-end">
            <Button className="w-full" onClick={aoBuscar} disabled={buscando}>
              {buscando ? <Loader2 className="animate-spin" /> : <Search />}
              {buscando ? 'Buscando...' : 'Buscar'}
            </Button>
          </div>
        </div>

        {erroBusca && (
          <Alert variant="destructive">
            <AlertTriangle />
            <AlertDescription>{erroBusca}</AlertDescription>
          </Alert>
        )}

        {item && (
          <>
            <div className="rounded-lg border border-border bg-muted/30 p-4 space-y-3">
              <div className="flex items-start justify-between gap-3">
                <div className="space-y-1 min-w-0">
                  <div className="flex items-center gap-2">
                    <Badge>{item.tipo}</Badge>
                    {!item.statusItem && <Badge variant="warning">Inativo</Badge>}
                    {item.itemSustentavel && <Badge variant="success"><Sparkles className="size-3" />Sustentável</Badge>}
                    <span className="text-sm text-muted-foreground font-mono">#{item.codigoItem}</span>
                  </div>
                  <div className="font-medium tracking-tight">{item.nomePdm}</div>
                </div>
                <Button variant="ghost" size="icon" onClick={aoLimpar} aria-label="Limpar">
                  <X />
                </Button>
              </div>
              <p className="text-sm text-muted-foreground leading-relaxed">{item.descricaoItem}</p>
              <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 text-xs pt-2 border-t border-border">
                <Bloco rotulo="Classe" valor={`${item.codigoClasse} · ${item.nomeClasse}`} />
                <Bloco rotulo="Grupo" valor={`${item.codigoGrupo} · ${item.nomeGrupo}`} />
                <Bloco rotulo="PDM" valor={item.codigoPdm.toString()} />
                <Bloco rotulo="Atualizado" valor={item.dataHoraAtualizacao ? new Date(item.dataHoraAtualizacao).toLocaleDateString('pt-BR') : '—'} />
              </div>
            </div>

            <Separator />

            <div>
              <div className="flex items-baseline justify-between mb-3">
                <div className="text-sm font-medium">Quantitativos</div>
                {total > 0 && (
                  <div className="text-sm">
                    <span className="text-muted-foreground">Total estimado:</span>{' '}
                    <span className="font-semibold tabular-nums">{formatarBrl(total)}</span>
                  </div>
                )}
              </div>
              <div className="grid grid-cols-12 gap-3">
                <div className="col-span-6 sm:col-span-3">
                  <Label htmlFor="qtd">Quantidade</Label>
                  <Input
                    id="qtd"
                    className="mt-1.5"
                    type="number"
                    value={quant.quantidade}
                    onChange={(e) => setQuant((q) => ({ ...q, quantidade: Number(e.target.value) }))}
                  />
                </div>
                <div className="col-span-6 sm:col-span-3">
                  <Label htmlFor="vu">Valor unitário</Label>
                  <Input
                    id="vu"
                    className="mt-1.5"
                    type="number"
                    step="0.01"
                    value={quant.valorUnitario}
                    onChange={(e) => setQuant((q) => ({ ...q, valorUnitario: Number(e.target.value) }))}
                  />
                </div>
                <div className="col-span-6 sm:col-span-2">
                  <Label htmlFor="sigla">Sigla un.</Label>
                  <Input
                    id="sigla"
                    className="mt-1.5"
                    value={quant.siglaUnidadeFornecimento}
                    onChange={(e) => setQuant((q) => ({ ...q, siglaUnidadeFornecimento: e.target.value }))}
                  />
                </div>
                <div className="col-span-6 sm:col-span-4">
                  <Label htmlFor="moeda">Moeda</Label>
                  <Input
                    id="moeda"
                    className="mt-1.5"
                    value={quant.moeda}
                    onChange={(e) => setQuant((q) => ({ ...q, moeda: e.target.value }))}
                  />
                </div>
                <div className="col-span-12">
                  <Label htmlFor="desc">Descrição (do catálogo)</Label>
                  <Textarea id="desc" className="mt-1.5" value={item.descricaoItem} readOnly rows={3} />
                </div>
              </div>
            </div>

            {erroEnvio && (
              <Alert variant="destructive">
                <AlertTriangle />
                <AlertDescription>{erroEnvio}</AlertDescription>
              </Alert>
            )}
            {sucesso && (
              <Alert variant="success">
                <CheckCircle2 />
                <AlertDescription>{sucesso}</AlertDescription>
              </Alert>
            )}

            <div className="flex justify-end gap-2">
              <Button variant="outline" onClick={aoLimpar}>Cancelar</Button>
              <Button onClick={aoEnviar} disabled={enviando}>
                {enviando ? <Loader2 className="animate-spin" /> : <Send />}
                {enviando ? 'Enviando...' : 'Adicionar ao DFD'}
              </Button>
            </div>
          </>
        )}
      </CardContent>
    </Card>
  )
}

function Bloco({ rotulo, valor }: { rotulo: string; valor: string }) {
  return (
    <div className="min-w-0">
      <div className="text-[10px] uppercase tracking-wide text-muted-foreground">{rotulo}</div>
      <div className="text-xs mt-0.5 truncate">{valor}</div>
    </div>
  )
}
