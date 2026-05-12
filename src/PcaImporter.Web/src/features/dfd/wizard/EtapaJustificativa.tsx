import { useState } from 'react'
import { Save, Loader2, CheckCircle2, AlertTriangle } from 'lucide-react'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { Textarea } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Button } from '@/components/ui/button'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { dfdApi } from '../dfdApi'

interface Props {
  valor: string
  aoMudar: (v: string) => void
  onSalvo?: () => void
}

export function EtapaJustificativa({ valor, aoMudar, onSalvo }: Props) {
  const [salvando, setSalvando] = useState(false)
  const [erro, setErro] = useState<string | null>(null)
  const [sucesso, setSucesso] = useState<string | null>(null)

  async function aoSalvar() {
    setErro(null)
    setSucesso(null)
    if (!valor.trim()) {
      setErro('Justificativa não pode ser vazia.')
      return
    }
    setSalvando(true)
    try {
      await dfdApi.atualizarJustificativa(valor)
      setSucesso('Justificativa salva.')
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
        <CardTitle>Justificativa de necessidade</CardTitle>
        <CardDescription>
          Explique a necessidade da contratação. Quebras de linha viram parágrafos automaticamente; HTML é escapado.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <div>
          <Label htmlFor="justificativa">Justificativa</Label>
          <Textarea
            id="justificativa"
            className="mt-1.5 font-sans"
            rows={14}
            value={valor}
            onChange={(e) => { aoMudar(e.target.value); setSucesso(null) }}
            placeholder="Descreva a necessidade da contratação. Inclua referências legais, técnicas e operacionais quando pertinente."
          />
          <div className="text-xs text-muted-foreground mt-2 tabular-nums">
            {valor.length} caracteres
          </div>
        </div>

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
          <Button onClick={aoSalvar} disabled={salvando || !valor.trim()}>
            {salvando ? <Loader2 className="animate-spin" /> : <Save />}
            {salvando ? 'Salvando...' : 'Salvar etapa'}
          </Button>
        </div>
      </CardContent>
    </Card>
  )
}
