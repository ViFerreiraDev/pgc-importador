import { useState } from 'react'
import { Save, Loader2, CheckCircle2, AlertTriangle } from 'lucide-react'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { Input, Textarea } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select } from '@/components/ui/select'
import { Button } from '@/components/ui/button'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Separator } from '@/components/ui/separator'
import { dfdApi } from '../dfdApi'
import type { NivelPrioridade } from '../tipos'
import type { InformacoesGerais } from './tipos'

const MAX_DESCRICAO = 200
const MAX_JUSTIFICATIVA = 4000

interface Props {
  valor: InformacoesGerais
  aoMudar: (v: InformacoesGerais) => void
  uasg: number | null
  onSalvo?: () => void
}

export function EtapaInformacoesGerais({ valor, aoMudar, uasg, onSalvo }: Props) {
  const [salvando, setSalvando] = useState(false)
  const [erro, setErro] = useState<string | null>(null)
  const [sucesso, setSucesso] = useState<string | null>(null)

  function atualizar<K extends keyof InformacoesGerais>(campo: K, novo: InformacoesGerais[K]) {
    aoMudar({ ...valor, [campo]: novo })
    setSucesso(null)
  }

  const restanteDesc = MAX_DESCRICAO - valor.descricao.length
  const restantePrioridade = MAX_JUSTIFICATIVA - valor.justificativaPrioridade.length
  const exigeJustificativa = valor.nivelPrioridade === 'ALTO'
  const valido =
    !!valor.dataConclusaoContratacao &&
    valor.descricao.trim().length > 0 &&
    valor.descricao.length <= MAX_DESCRICAO &&
    (!exigeJustificativa || valor.justificativaPrioridade.trim().length > 0)

  async function aoSalvar() {
    setErro(null)
    setSucesso(null)
    if (!valido) {
      setErro(exigeJustificativa
        ? 'Preencha data, descrição e justificativa de prioridade.'
        : 'Preencha data e descrição.')
      return
    }
    setSalvando(true)
    try {
      const r = await dfdApi.atualizarInformacoesGerais({
        dataConclusaoContratacao: valor.dataConclusaoContratacao,
        objeto: valor.descricao,
        nivelPrioridade: valor.nivelPrioridade,
        justificativaPrioridade: exigeJustificativa ? valor.justificativaPrioridade : '',
      })
      setSucesso(`Informações gerais salvas. Prioridade: ${r.nivelPrioridade}.`)
      onSalvo?.()
    } catch (e) {
      setErro(e instanceof Error ? e.message : String(e))
    } finally {
      setSalvando(false)
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Informações gerais</CardTitle>
        <CardDescription>
          Data prevista da contratação, descrição sucinta do objeto e prioridade.
          UASG e área requisitante são preenchidas automaticamente a partir da sua sessão.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-5">
        <div className="grid grid-cols-12 gap-4">
          <div className="col-span-12 sm:col-span-4">
            <Label htmlFor="dataConclusao">Data da conclusão da contratação <span className="text-destructive">*</span></Label>
            <Input
              id="dataConclusao"
              type="date"
              className="mt-1.5"
              value={valor.dataConclusaoContratacao}
              onChange={(e) => atualizar('dataConclusaoContratacao', e.target.value)}
            />
          </div>
          <div className="col-span-6 sm:col-span-4">
            <Label>UASG (origem)</Label>
            <Input
              className="mt-1.5"
              value={uasg?.toString() ?? '—'}
              readOnly
              disabled
            />
          </div>
          <div className="col-span-6 sm:col-span-4">
            <Label>Área requisitante</Label>
            <Input
              className="mt-1.5"
              value="SMS (vinda da sessão)"
              readOnly
              disabled
            />
          </div>
        </div>

        <div>
          <div className="flex items-baseline justify-between">
            <Label htmlFor="descricao">Descrição sucinta do objeto <span className="text-destructive">*</span></Label>
            <span className={`text-[11px] tabular-nums ${restanteDesc < 0 ? 'text-destructive' : 'text-muted-foreground'}`}>
              {restanteDesc} caracteres restantes
            </span>
          </div>
          <Textarea
            id="descricao"
            className="mt-1.5 font-sans"
            rows={3}
            value={valor.descricao}
            onChange={(e) => atualizar('descricao', e.target.value)}
            placeholder="Resumo objetivo do que está sendo contratado"
            maxLength={MAX_DESCRICAO}
          />
        </div>

        <Separator />

        <div className="grid grid-cols-12 gap-4">
          <div className="col-span-12 sm:col-span-4">
            <Label htmlFor="prioridade">Prioridade</Label>
            <Select
              id="prioridade"
              className="mt-1.5"
              value={valor.nivelPrioridade}
              onChange={(e) => atualizar('nivelPrioridade', e.target.value as NivelPrioridade)}
            >
              <option value="BAIXO">Baixa</option>
              <option value="MEDIO">Média</option>
              <option value="ALTO">Alta</option>
            </Select>
          </div>
        </div>

        {exigeJustificativa && (
          <div>
            <div className="flex items-baseline justify-between">
              <Label htmlFor="justPrio">
                Justificativa de prioridade <span className="text-destructive">*</span>
              </Label>
              <span className={`text-[11px] tabular-nums ${restantePrioridade < 0 ? 'text-destructive' : 'text-muted-foreground'}`}>
                {restantePrioridade} caracteres restantes
              </span>
            </div>
            <Textarea
              id="justPrio"
              className="mt-1.5 font-sans"
              rows={4}
              value={valor.justificativaPrioridade}
              onChange={(e) => atualizar('justificativaPrioridade', e.target.value)}
              placeholder="Justifique por que esta contratação é de prioridade alta."
              maxLength={MAX_JUSTIFICATIVA}
            />
          </div>
        )}

        {erro && (
          <Alert variant="destructive">
            <AlertTriangle />
            <AlertDescription>{erro}</AlertDescription>
          </Alert>
        )}
        {sucesso && (
          <Alert variant="success">
            <CheckCircle2 />
            <AlertDescription>{sucesso}</AlertDescription>
          </Alert>
        )}

        <div className="flex justify-end">
          <Button onClick={aoSalvar} disabled={salvando || !valido}>
            {salvando ? <Loader2 className="animate-spin" /> : <Save />}
            {salvando ? 'Salvando...' : 'Salvar etapa'}
          </Button>
        </div>
      </CardContent>
    </Card>
  )
}
