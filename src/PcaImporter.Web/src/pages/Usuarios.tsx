import { useEffect, useState, type FormEvent } from 'react'
import {
  AlertTriangle, Loader2, Plus, RefreshCw, Shield, Trash2, User,
} from 'lucide-react'
import { PageHeader } from '@/components/comum/PageHeader'
import { Card, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Badge } from '@/components/ui/badge'
import { Alert, AlertDescription } from '@/components/ui/alert'
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'
import { authApi } from '@/features/auth/authApi'
import type { Papel, Usuario } from '@/features/auth/tipos'
import { useAuth } from '@/features/auth/AuthContext'
import { cn, formatarDataHora } from '@/lib/utils'

export function Usuarios() {
  const { ehAdmin, usuario: meu } = useAuth()
  const [lista, setLista] = useState<Usuario[]>([])
  const [carregando, setCarregando] = useState(true)
  const [erro, setErro] = useState<string | null>(null)
  const [criando, setCriando] = useState(false)
  const [aRemover, setARemover] = useState<Usuario | null>(null)

  async function carregar() {
    try {
      setErro(null)
      setLista(await authApi.listarUsuarios())
    } catch (e) {
      setErro(e instanceof Error ? e.message : String(e))
    } finally {
      setCarregando(false)
    }
  }

  useEffect(() => { void carregar() }, [])

  return (
    <div className="anim-rise">
      <PageHeader
        titulo="Usuários"
        descricao="Quem pode entrar no sistema. Usuários normais veem tudo (métricas, histórico, logs); apenas admins gerenciam o token Compras.gov.br."
        acoes={
          <>
            <Button variant="secondary" onClick={() => void carregar()}>
              {carregando ? <Loader2 className="animate-spin" /> : <RefreshCw />}
              Atualizar
            </Button>
            <Button onClick={() => setCriando(true)}>
              <Plus /> Novo usuário
            </Button>
          </>
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
            <CardTitle>Cadastrados</CardTitle>
            <CardDescription>
              {lista.length} {lista.length === 1 ? 'usuário' : 'usuários'} ·{' '}
              {lista.filter((l) => l.papel === 'Admin').length} admins ·{' '}
              {lista.filter((l) => l.papel === 'Normal').length} normais
            </CardDescription>
          </div>
        </CardHeader>

        <div className="overflow-x-auto">
          <table className="w-full text-[13px]">
            <thead>
              <tr className="text-left">
                <Th className="w-[200px]">Login</Th>
                <Th>Nome</Th>
                <Th className="w-[110px]">Permissão</Th>
                <Th className="w-[170px]">Criado em</Th>
                <Th className="w-[140px]">Criado por</Th>
                <Th className="w-[110px] text-right">Ações</Th>
              </tr>
            </thead>
            <tbody>
              {lista.map((u) => (
                <tr
                  key={u.id}
                  className={cn(
                    'border-b border-[hsl(var(--neutral-100))] last:border-b-0 hover:bg-[hsl(var(--neutral-25))]',
                    u.id === meu?.id && 'bg-[hsl(var(--brand-25))]',
                  )}
                >
                  <Td>
                    <span className="font-mono text-[13px] text-foreground">{u.login}</span>
                    {u.id === meu?.id && (
                      <Badge variant="outline" className="ml-2 text-[9px]">você</Badge>
                    )}
                  </Td>
                  <Td>{u.nome}</Td>
                  <Td>
                    {u.papel === 'Admin' ? (
                      <Badge variant="brand" comDot><Shield className="size-3" />admin</Badge>
                    ) : (
                      <Badge variant="muted"><User className="size-3" />normal</Badge>
                    )}
                  </Td>
                  <Td className="text-[12px] text-muted-foreground">{formatarDataHora(u.criadoEm)}</Td>
                  <Td className="text-[12px] text-muted-foreground font-mono">{u.criadoPorLogin ?? '—'}</Td>
                  <Td className="text-right">
                    {ehAdmin && u.id !== meu?.id && (
                      <Button variant="destructive-outline" size="xs" onClick={() => setARemover(u)}>
                        <Trash2 /> Remover
                      </Button>
                    )}
                  </Td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>

      <DialogCriar
        aberto={criando}
        ehAdmin={ehAdmin}
        onFechar={() => setCriando(false)}
        onCriado={() => { setCriando(false); void carregar() }}
      />
      <DialogRemover
        usuario={aRemover}
        onFechar={() => setARemover(null)}
        onRemovido={() => { setARemover(null); void carregar() }}
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

function DialogCriar({
  aberto, ehAdmin, onFechar, onCriado,
}: {
  aberto: boolean
  ehAdmin: boolean
  onFechar: () => void
  onCriado: () => void
}) {
  const [login, setLogin] = useState('')
  const [nome, setNome] = useState('')
  const [senha, setSenha] = useState('')
  const [papel, setPapel] = useState<Papel>('Normal')
  const [enviando, setEnviando] = useState(false)
  const [erro, setErro] = useState<string | null>(null)

  function reset() {
    setLogin('')
    setNome('')
    setSenha('')
    setPapel('Normal')
    setErro(null)
  }

  async function aoSubmeter(ev: FormEvent) {
    ev.preventDefault()
    setEnviando(true)
    setErro(null)
    try {
      await authApi.criarUsuario({ login: login.trim(), nome: nome.trim(), senha, papel })
      reset()
      onCriado()
    } catch (e) {
      setErro(e instanceof Error ? e.message : String(e))
    } finally {
      setEnviando(false)
    }
  }

  return (
    <Dialog open={aberto} onOpenChange={(open) => { if (!open && !enviando) { reset(); onFechar() } }}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <div className="flex size-10 items-center justify-center rounded-[10px] bg-[hsl(var(--brand-50))] text-[hsl(var(--brand-600))] mb-1">
            <Plus className="size-5" />
          </div>
          <DialogTitle>Novo usuário</DialogTitle>
          <DialogDescription>
            Usuários normais podem ver tudo (importações, histórico, logs) e criar outros usuários normais. Apenas admins gerenciam o token Compras.gov.br.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={aoSubmeter} className="space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <label className="text-[12px] font-medium text-[hsl(var(--neutral-700))]">Login</label>
              <Input
                value={login}
                onChange={(e) => setLogin(e.target.value)}
                placeholder="seu.login"
                className="font-mono"
              />
            </div>
            <div className="space-y-1.5">
              <label className="text-[12px] font-medium text-[hsl(var(--neutral-700))]">Permissão</label>
              <select
                value={papel}
                onChange={(e) => setPapel(e.target.value as Papel)}
                disabled={!ehAdmin}
                className="flex h-9 w-full rounded-md border border-input bg-surface px-3 text-[13px]"
              >
                <option value="Normal">Normal</option>
                {ehAdmin && <option value="Admin">Admin</option>}
              </select>
            </div>
          </div>
          <div className="space-y-1.5">
            <label className="text-[12px] font-medium text-[hsl(var(--neutral-700))]">Nome completo</label>
            <Input value={nome} onChange={(e) => setNome(e.target.value)} placeholder="Maria da Silva" />
          </div>
          <div className="space-y-1.5">
            <label className="text-[12px] font-medium text-[hsl(var(--neutral-700))]">Senha</label>
            <Input
              type="password"
              value={senha}
              onChange={(e) => setSenha(e.target.value)}
              placeholder="Mínimo 6 caracteres"
            />
          </div>

          {erro && (
            <Alert variant="destructive">
              <AlertTriangle />
              <AlertDescription>{erro}</AlertDescription>
            </Alert>
          )}

          <DialogFooter>
            <Button type="button" variant="secondary" onClick={() => { reset(); onFechar() }} disabled={enviando}>
              Cancelar
            </Button>
            <Button type="submit" disabled={enviando || !login || !nome || senha.length < 6}>
              {enviando && <Loader2 className="animate-spin" />}
              {enviando ? 'Criando…' : 'Criar usuário'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

function DialogRemover({
  usuario, onFechar, onRemovido,
}: {
  usuario: Usuario | null
  onFechar: () => void
  onRemovido: () => void
}) {
  const [removendo, setRemovendo] = useState(false)
  const [erro, setErro] = useState<string | null>(null)

  async function aoRemover() {
    if (!usuario) return
    setRemovendo(true)
    setErro(null)
    try {
      await authApi.removerUsuario(usuario.id)
      onRemovido()
    } catch (e) {
      setErro(e instanceof Error ? e.message : String(e))
    } finally {
      setRemovendo(false)
    }
  }

  return (
    <Dialog open={usuario !== null} onOpenChange={(open) => { if (!open && !removendo) { setErro(null); onFechar() } }}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <div className="flex size-10 items-center justify-center rounded-[10px] bg-[hsl(var(--error-50))] text-[hsl(var(--error-500))] mb-1">
            <Trash2 className="size-5" />
          </div>
          <DialogTitle>Remover usuário</DialogTitle>
          <DialogDescription>
            {usuario && (
              <>
                Vai remover o acesso de <strong className="font-mono">{usuario.login}</strong> ({usuario.nome}). O histórico de importações dele permanece registrado.
              </>
            )}
          </DialogDescription>
        </DialogHeader>
        {erro && (
          <Alert variant="destructive">
            <AlertTriangle />
            <AlertDescription>{erro}</AlertDescription>
          </Alert>
        )}
        <DialogFooter>
          <Button variant="secondary" onClick={onFechar} disabled={removendo}>Cancelar</Button>
          <Button variant="destructive" onClick={() => void aoRemover()} disabled={removendo}>
            {removendo && <Loader2 className="animate-spin" />}
            {removendo ? 'Removendo…' : 'Sim, remover'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
