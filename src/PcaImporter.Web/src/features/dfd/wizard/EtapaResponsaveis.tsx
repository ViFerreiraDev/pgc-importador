import { useState } from 'react'
import { Save, Loader2, CheckCircle2, AlertTriangle, Trash2 } from 'lucide-react'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Separator } from '@/components/ui/separator'
import { dfdApi } from '../dfdApi'
import { mascararCpf } from '@/lib/utils'
import type { ResponsavelCriado } from '../tipos'

const VAZIO = { cpf: '', nome: '', email: '', cargo: '' }

interface Props {
  responsaveis: ResponsavelCriado[]
  onResponsavelAdicionado: () => void
}

export function EtapaResponsaveis({ responsaveis, onResponsavelAdicionado }: Props) {
  const [v, setV] = useState(VAZIO)
  const [erro, setErro] = useState<string | null>(null)
  const [sucesso, setSucesso] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  const jaTem = responsaveis.length > 0

  function atualizar<K extends keyof typeof VAZIO>(campo: K, novo: typeof VAZIO[K]) {
    setV((s) => ({ ...s, [campo]: novo }))
    setSucesso(null)
  }

  function validar(): string | null {
    if (!v.cpf.trim()) return 'CPF é obrigatório.'
    if (!/^\d{11}$/.test(v.cpf.replace(/\D/g, ''))) return 'CPF inválido (esperado 11 dígitos).'
    if (!v.nome.trim()) return 'Nome é obrigatório.'
    if (!v.email.trim()) return 'Email é obrigatório.'
    if (!/.+@.+\..+/.test(v.email)) return 'Email inválido.'
    if (!v.cargo.trim()) return 'Cargo é obrigatório.'
    return null
  }

  async function aoSalvar() {
    const e = validar()
    if (e) { setErro(e); return }
    setEnviando(true)
    setErro(null)
    setSucesso(null)
    try {
      await dfdApi.adicionarResponsavel({
        cpf: v.cpf.replace(/\D/g, ''),
        nome: v.nome.trim(),
        email: v.email.trim(),
        cargo: v.cargo.trim(),
      })
      setSucesso(`Responsável ${v.nome} cadastrado.`)
      setV(VAZIO)
      onResponsavelAdicionado()
    } catch (e) {
      setErro(e instanceof Error ? e.message : String(e))
    } finally {
      setEnviando(false)
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Responsável</CardTitle>
        <CardDescription>
          Pessoa que assina o DFD. {jaTem ? 'Já cadastrado.' : 'Pelo menos um é obrigatório.'}
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-5">
        {jaTem ? (
          <div className="rounded-lg border border-success/30 bg-success/5 p-4">
            <div className="flex items-start justify-between gap-4">
              <div className="space-y-1.5 min-w-0">
                <div className="flex items-center gap-2">
                  <CheckCircle2 className="size-4 text-success" />
                  <span className="font-medium">{responsaveis[0].nome}</span>
                  <Badge variant="success" className="text-[10px]">{responsaveis[0].instrumento}</Badge>
                </div>
                <div className="grid grid-cols-1 sm:grid-cols-3 gap-x-6 gap-y-1 text-xs text-muted-foreground">
                  <div><span className="text-foreground/70">CPF:</span> <span className="font-mono">{mascararCpf(responsaveis[0].cpf)}</span></div>
                  <div><span className="text-foreground/70">Email:</span> {responsaveis[0].email}</div>
                  <div><span className="text-foreground/70">Cargo:</span> {responsaveis[0].cargo}</div>
                </div>
              </div>
              <Button variant="ghost" size="icon" disabled aria-label="Remover" title="Remoção será adicionada com endpoint DELETE">
                <Trash2 />
              </Button>
            </div>
          </div>
        ) : (
          <>
            <div className="grid grid-cols-12 gap-3">
              <div className="col-span-12 sm:col-span-5">
                <Label htmlFor="cpf">CPF <span className="text-destructive">*</span></Label>
                <Input id="cpf" className="mt-1.5" value={v.cpf} onChange={(e) => atualizar('cpf', e.target.value)} placeholder="00000000000" />
              </div>
              <div className="col-span-12 sm:col-span-7">
                <Label htmlFor="nome">Nome <span className="text-destructive">*</span></Label>
                <Input id="nome" className="mt-1.5" value={v.nome} onChange={(e) => atualizar('nome', e.target.value)} placeholder="Nome completo" />
              </div>
              <div className="col-span-12 sm:col-span-6">
                <Label htmlFor="email">Email <span className="text-destructive">*</span></Label>
                <Input id="email" type="email" className="mt-1.5" value={v.email} onChange={(e) => atualizar('email', e.target.value)} placeholder="email@dominio.gov.br" />
              </div>
              <div className="col-span-12 sm:col-span-6">
                <Label htmlFor="cargo">Cargo <span className="text-destructive">*</span></Label>
                <Input id="cargo" className="mt-1.5" value={v.cargo} onChange={(e) => atualizar('cargo', e.target.value)} placeholder="Ex: Coordenador II" />
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

            <Separator />

            <div className="flex justify-end">
              <Button onClick={aoSalvar} disabled={enviando}>
                {enviando ? <Loader2 className="animate-spin" /> : <Save />}
                {enviando ? 'Salvando...' : 'Salvar responsável'}
              </Button>
            </div>
          </>
        )}
      </CardContent>
    </Card>
  )
}
