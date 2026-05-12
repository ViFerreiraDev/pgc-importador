import { useState, type FormEvent } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { Loader2, LogIn, AlertTriangle } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { useAuth } from '@/features/auth/AuthContext'

export function Login() {
  const { usuario, carregando, login } = useAuth()
  const [loginId, setLoginId] = useState('')
  const [senha, setSenha] = useState('')
  const [enviando, setEnviando] = useState(false)
  const [erro, setErro] = useState<string | null>(null)
  const location = useLocation()
  const destino = (location.state as { from?: string } | null)?.from ?? '/'

  if (carregando) {
    return (
      <div className="min-h-screen grid place-items-center bg-[hsl(220_15%_18%)]">
        <Loader2 className="size-5 animate-spin text-white/40" />
      </div>
    )
  }

  if (usuario) {
    return <Navigate to={destino} replace />
  }

  async function aoSubmeter(ev: FormEvent) {
    ev.preventDefault()
    setEnviando(true)
    setErro(null)
    try {
      await login(loginId.trim(), senha)
    } catch (e) {
      setErro(e instanceof Error ? e.message : String(e))
    } finally {
      setEnviando(false)
    }
  }

  return (
    <div className="min-h-screen grid place-items-center bg-[hsl(220_15%_18%)] px-4">
      <form
        onSubmit={aoSubmeter}
        className="w-full max-w-sm bg-surface rounded-[10px] border border-border p-7 shadow-[0_24px_48px_-12px_rgba(0,0,0,0.45)] space-y-5"
      >
        <div>
          <h1 className="text-[22px] font-semibold tracking-[-0.02em]">Entrar</h1>
          <p className="text-[13px] text-muted-foreground mt-1">
            PGC Saúde.Rio · Importador
          </p>
        </div>

        <div className="space-y-3">
          <div className="space-y-1.5">
            <label htmlFor="login" className="text-[12px] font-medium text-[hsl(var(--neutral-700))]">Login</label>
            <Input
              id="login"
              type="text"
              autoComplete="username"
              autoFocus
              value={loginId}
              onChange={(e) => setLoginId(e.target.value)}
            />
          </div>
          <div className="space-y-1.5">
            <label htmlFor="senha" className="text-[12px] font-medium text-[hsl(var(--neutral-700))]">Senha</label>
            <Input
              id="senha"
              type="password"
              autoComplete="current-password"
              value={senha}
              onChange={(e) => setSenha(e.target.value)}
            />
          </div>
        </div>

        {erro && (
          <Alert variant="destructive">
            <AlertTriangle />
            <AlertDescription>{erro}</AlertDescription>
          </Alert>
        )}

        <Button type="submit" className="w-full" size="lg" disabled={enviando || !loginId || !senha}>
          {enviando ? <Loader2 className="animate-spin" /> : <LogIn />}
          {enviando ? 'Entrando…' : 'Entrar'}
        </Button>
      </form>
    </div>
  )
}
