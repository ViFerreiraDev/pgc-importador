import { useEffect, useState } from 'react'
import { FilePlus2, Loader2, Trash2, FileText } from 'lucide-react'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { EmptyState } from '@/components/comum/EmptyState'
import { dfdApi } from './dfdApi'
import type { DfdAtual } from './tipos'
import { WizardDfd } from './wizard/WizardDfd'

export function PainelDfd() {
  const [atual, setAtual] = useState<DfdAtual>({ atual: null, itens: [], responsaveis: [] })
  const [criando, setCriando] = useState(false)
  const [carregando, setCarregando] = useState(true)
  const [erro, setErro] = useState<string | null>(null)

  useEffect(() => { void recarregar() }, [])

  async function recarregar() {
    try {
      const r = await dfdApi.obterAtual()
      setAtual(r)
    } catch (e) {
      setErro(e instanceof Error ? e.message : String(e))
    } finally {
      setCarregando(false)
    }
  }

  async function aoCriar() {
    setCriando(true)
    setErro(null)
    try {
      await dfdApi.criar()
      await recarregar()
    } catch (e) {
      setErro(e instanceof Error ? e.message : String(e))
    } finally {
      setCriando(false)
    }
  }

  async function aoLimpar() {
    try {
      await dfdApi.limpar()
      await recarregar()
    } catch (e) {
      setErro(e instanceof Error ? e.message : String(e))
    }
  }

  if (carregando) {
    return (
      <Card>
        <CardContent className="flex items-center gap-3 py-10 justify-center text-sm text-muted-foreground">
          <Loader2 className="size-4 animate-spin" />
          Carregando DFD atual...
        </CardContent>
      </Card>
    )
  }

  return (
    <div className="space-y-6">
      {erro && (
        <Alert variant="destructive">
          <AlertDescription>{erro}</AlertDescription>
        </Alert>
      )}

      {!atual.atual ? (
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={FileText}
              titulo="Nenhum DFD ativo"
              descricao="Crie um Documento de Formalização da Demanda em rascunho para começar."
              acao={
                <Button onClick={aoCriar} disabled={criando}>
                  {criando ? <Loader2 className="animate-spin" /> : <FilePlus2 />}
                  {criando ? 'Criando DFD...' : 'Criar novo DFD'}
                </Button>
              }
            />
          </CardContent>
        </Card>
      ) : (
        <>
          <Card>
            <CardHeader className="flex-row items-start justify-between gap-4">
              <div className="space-y-1.5">
                <div className="flex items-center gap-2">
                  <CardTitle>DFD #{atual.atual.numero}/{atual.atual.ano}</CardTitle>
                  <Badge variant={atual.atual.status === 'RASCUNHO' ? 'warning' : 'success'}>
                    {atual.atual.status}
                  </Badge>
                </div>
                <CardDescription>{atual.atual.nomeUasg}</CardDescription>
              </div>
              <div className="flex items-center gap-2">
                <Button variant="outline" size="sm" onClick={aoCriar} disabled={criando}>
                  {criando ? <Loader2 className="animate-spin" /> : <FilePlus2 />}
                  Novo DFD
                </Button>
                <Button variant="outline" size="sm" onClick={aoLimpar}>
                  <Trash2 />
                  Descartar
                </Button>
              </div>
            </CardHeader>
            <CardContent className="grid grid-cols-2 sm:grid-cols-5 gap-x-6 gap-y-3 pt-2">
              <Info rotulo="idArtefato" valor={atual.atual.idArtefato.toString()} />
              <Info rotulo="idFormalizacaoDemanda" valor={atual.atual.idFormalizacaoDemanda.toString()} destaque />
              <Info rotulo="UASG" valor={atual.atual.uasg.toString()} />
              <Info rotulo="Ano PCA" valor={atual.atual.anoPca?.toString() ?? '—'} />
              <Info rotulo="Status PCA" valor={atual.atual.statusPca ?? '—'} />
            </CardContent>
          </Card>

          <WizardDfd
            uasg={atual.atual.uasg}
            itensMaterial={atual.itens}
            responsaveis={atual.responsaveis}
            onItemAdicionado={() => void recarregar()}
            onResponsavelAdicionado={() => void recarregar()}
          />
        </>
      )}
    </div>
  )
}

function Info({ rotulo, valor, destaque = false }: { rotulo: string; valor: string; destaque?: boolean }) {
  return (
    <div className="min-w-0">
      <div className="text-[10px] uppercase tracking-wide text-muted-foreground">{rotulo}</div>
      <div className={`mt-0.5 text-sm font-mono tabular-nums truncate ${destaque ? 'text-primary font-semibold' : ''}`}>
        {valor}
      </div>
    </div>
  )
}
