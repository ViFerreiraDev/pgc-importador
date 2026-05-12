import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  Activity, ArrowRight, BarChart3, Briefcase, CheckSquare, CheckCircle2,
  Circle, Eye, FileSpreadsheet, FileText, HeartPulse, Layers,
  Package, ShieldCheck,
} from 'lucide-react'
import { PageHeader, SectionTitle } from '@/components/comum/PageHeader'
import { StatCard } from '@/components/comum/StatCard'
import { Card, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { useToken } from '@/features/token/TokenContext'
import { dfdApi } from '@/features/dfd/dfdApi'
import { logsApi } from '@/features/logs/logsApi'
import { importacaoApi } from '@/features/importacao/importacaoApi'
import type { DfdAtual } from '@/features/dfd/tipos'
import type { LogEvento, NivelLog } from '@/features/logs/tipos'
import type { MetricasImportacao } from '@/features/importacao/tipos'
import { cn, formatarBrl, formatarBrlCompacto, formatarDuracao, formatarNumeroCompacto } from '@/lib/utils'

const corDot: Record<NivelLog, string> = {
  Info: 'bg-[hsl(var(--info-500))]',
  Sucesso: 'bg-[hsl(var(--success-500))]',
  Aviso: 'bg-[hsl(var(--warning-500))]',
  Erro: 'bg-[hsl(var(--error-500))]',
}

export function Inicio() {
  const { status } = useToken()
  const [dfd, setDfd] = useState<DfdAtual>({ atual: null, itens: [], responsaveis: [] })
  const [logsRecentes, setLogsRecentes] = useState<LogEvento[]>([])
  const [metricas, setMetricas] = useState<MetricasImportacao>({ totalPlanilhas: 0, totalItens: 0, totalValor: 0 })
  const [, setTick] = useState(0)

  useEffect(() => {
    const id = setInterval(() => setTick((n) => n + 1), 1000)
    return () => clearInterval(id)
  }, [])

  useEffect(() => {
    let ativo = true
    async function carregar() {
      try {
        const [d, l, m] = await Promise.all([
          dfdApi.obterAtual(),
          logsApi.listar({ tamanhoPagina: 6 }),
          importacaoApi.metricas(),
        ])
        if (!ativo) return
        setDfd(d)
        setLogsRecentes(l.itens)
        setMetricas(m)
      } catch {
      }
    }
    void carregar()
    const id = setInterval(carregar, 5000)
    return () => { ativo = false; clearInterval(id) }
  }, [])

  const totalDfd = dfd.itens.reduce((a, i) => a + Number(i.valorTotal), 0)
  const restanteSeg = status?.expiraEm
    ? Math.max(0, Math.floor((new Date(status.expiraEm).getTime() - Date.now()) / 1000))
    : 0
  const sessaoAtiva = status?.estado === 'Saudavel'

  const sessaoMap = {
    Saudavel: { label: 'Ativa', cor: 'success' as const, sub: `Renova em ${formatarDuracao(restanteSeg)}` },
    ProximoExpirar: { label: 'Renovando', cor: 'warning' as const, sub: 'Aguarde' },
    Expirado: { label: 'Expirada', cor: 'destructive' as const, sub: 'Reconecte' },
    RefreshFalhou: { label: 'Falhou', cor: 'destructive' as const, sub: 'Reconecte' },
    Ausente: { label: 'Ausente', cor: 'destructive' as const, sub: 'Conecte um token' },
  }
  const sessao = sessaoMap[status?.estado ?? 'Ausente']

  return (
    <div className="anim-rise">
      <PageHeader
        titulo="Visão geral"
        descricao="Importe itens do PGC no Compras.gov.br com confiabilidade. Monitore sessão, importações e atividade em um só lugar."
        acoes={
          <Button asChild variant="primary" size="lg">
            <Link to="/importar">Nova importação <ArrowRight /></Link>
          </Button>
        }
      />

      <SectionTitle>Histórico acumulado</SectionTitle>
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
        <StatCard
          icon={ShieldCheck}
          rotulo="Sessão"
          valor={sessao.label}
          cor={sessao.cor === 'success' ? 'success' : sessao.cor === 'warning' ? 'warning' : 'destructive'}
          descricao={sessao.sub}
        />
        <StatCard
          icon={FileSpreadsheet}
          rotulo="Planilhas importadas"
          valor={metricas.totalPlanilhas.toLocaleString('pt-BR')}
          descricao={metricas.totalPlanilhas > 0 ? 'Histórico acumulado' : 'Nenhuma importação ainda'}
        />
        <StatCard
          icon={Package}
          rotulo="Itens importados"
          valor={formatarNumeroCompacto(metricas.totalItens)}
          valorTitulo={metricas.totalItens.toLocaleString('pt-BR') + ' itens'}
          descricao={metricas.totalItens.toLocaleString('pt-BR') + ' itens'}
        />
        <StatCard
          icon={BarChart3}
          rotulo="Valor total"
          valor={formatarBrlCompacto(metricas.totalValor)}
          valorTitulo={formatarBrl(metricas.totalValor)}
          descricao={formatarBrl(metricas.totalValor)}
        />
      </div>

      <SectionTitle>DFD em curso</SectionTitle>
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
        <StatCard
          icon={FileText}
          rotulo="DFD atual"
          valor={dfd.atual ? `#${dfd.atual.numero}/${dfd.atual.ano}` : '—'}
          descricao={dfd.atual ? `UASG ${dfd.atual.uasg} · ${dfd.atual.status}` : 'Nenhum DFD criado nesta sessão'}
          cor={dfd.atual ? 'brand' : 'default'}
        />
        <StatCard
          icon={Layers}
          rotulo="Itens no DFD atual"
          valor={dfd.itens.length.toString()}
          descricao={dfd.itens.length > 0 ? 'Adicionados nesta sessão' : 'Aguardando primeiro item'}
        />
        <StatCard
          icon={Briefcase}
          rotulo="Valor no DFD atual"
          valor={formatarBrlCompacto(totalDfd)}
          valorTitulo={formatarBrl(totalDfd)}
          descricao={formatarBrl(totalDfd)}
        />
      </div>

      <SectionTitle>Operação</SectionTitle>
      <div className="grid grid-cols-1 lg:grid-cols-[2fr_1fr] gap-4">
        <Card>
          <CardHeader>
            <CardTitle><Activity />Atividade recente</CardTitle>
            <Link to="/logs" className="text-[12px] font-medium text-[hsl(var(--brand-600))] no-underline hover:underline">
              Ver tudo →
            </Link>
          </CardHeader>
          <div>
            {logsRecentes.length === 0 ? (
              <div className="px-[18px] py-10 text-center text-[13px] text-muted-foreground">
                Sem eventos por enquanto. Crie um DFD ou conecte sua sessão.
              </div>
            ) : (
              logsRecentes.map((log) => (
                <div key={log.id} className="flex items-start gap-3 px-[18px] py-2.5 border-b border-[hsl(var(--neutral-100))] last:border-b-0">
                  <span className={cn('size-2 rounded-full mt-1.5 shrink-0', corDot[log.nivel])} />
                  <div className="flex-1 text-[13px] leading-[1.5]">{log.mensagem}</div>
                  <div className="flex items-center gap-2 text-[11px] text-muted-foreground shrink-0">
                    <Badge variant="outline" size="upper">{log.categoria}</Badge>
                    <span className="font-mono tabular-nums">{new Date(log.ocorridoEm).toLocaleTimeString('pt-BR')}</span>
                  </div>
                </div>
              ))
            )}
          </div>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle><CheckSquare />Próximos passos</CardTitle>
          </CardHeader>
          <div className="p-2 flex flex-col gap-1">
            <CheckItem
              done={sessaoAtiva}
              label="Conectar sessão Compras.gov.br"
              acao="Conectar"
              link="/configuracoes"
            />
            <CheckItem
              done={metricas.totalItens > 0}
              label="Sincronizar catálogo local de materiais"
              acao="Sincronizar"
              link="/configuracoes"
            />
            <CheckItem
              done={metricas.totalPlanilhas > 0}
              label="Importar primeira planilha em lote"
              acao="Importar"
              link="/importar"
            />
            <CheckItem
              done={metricas.totalPlanilhas > 1}
              label="Conferir histórico de importações"
              acao="Histórico"
              link="/historico"
            />
          </div>
        </Card>
      </div>

      <SectionTitle>Sobre esta ferramenta</SectionTitle>
      <Card>
        <div className="p-[18px] grid grid-cols-1 sm:grid-cols-3 gap-6">
          <Sobre icone={ShieldCheck} titulo="Idempotência">
            Cada planilha tem um hash único. Se já foi importada, o sistema avisa antes de criar um DFD duplicado no portal.
          </Sobre>
          <Sobre icone={HeartPulse} titulo="Sessão viva">
            O refresh token roda automaticamente em background. Você cola uma vez e o sistema mantém a sessão renovada entre reinícios.
          </Sobre>
          <Sobre icone={Eye} titulo="Auditoria">
            Cada chamada ao Compras.gov.br fica registrada em logs filtráveis por nível e categoria. Ideal para depuração e prestação de contas.
          </Sobre>
        </div>
      </Card>
    </div>
  )
}

function CheckItem({
  done, label, acao, link,
}: {
  done: boolean
  label: string
  acao?: string
  link?: string
}) {
  return (
    <div className="flex items-center gap-2.5 px-3 py-2.5 rounded-md hover:bg-[hsl(var(--neutral-50))] transition-colors">
      <span
        className={cn(
          'grid place-items-center size-[22px] rounded-full shrink-0',
          done
            ? 'bg-[hsl(var(--success-500))] text-white'
            : 'bg-[hsl(var(--neutral-50))] border-[1.5px] border-[hsl(var(--neutral-300))] text-muted-foreground',
        )}
      >
        {done ? <CheckCircle2 className="size-3" /> : <Circle className="size-2" strokeWidth={2.5} />}
      </span>
      <span className={cn(
        'flex-1 text-[13px] font-medium',
        done && 'line-through decoration-[hsl(var(--neutral-300))] text-muted-foreground',
      )}>
        {label}
      </span>
      {!done && acao && link && (
        <Button asChild variant="secondary" size="xs">
          <Link to={link}>{acao} <ArrowRight /></Link>
        </Button>
      )}
    </div>
  )
}

function Sobre({
  icone: Icone, titulo, children,
}: {
  icone: React.ComponentType<{ className?: string }>
  titulo: string
  children: React.ReactNode
}) {
  return (
    <div>
      <h3 className="flex items-center gap-2 text-[13px] font-semibold mb-1.5">
        <Icone className="size-3.5 text-[hsl(var(--brand-500))]" />
        {titulo}
      </h3>
      <p className="text-[12px] text-muted-foreground leading-[1.55] m-0">{children}</p>
    </div>
  )
}
