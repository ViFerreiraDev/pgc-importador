import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import {
  AlertTriangle, CheckCircle2, Eraser, ExternalLink, Loader2, ListChecks, Play,
  RotateCw, Scale, Search, Shield, Trash2, Upload, XCircle,
} from 'lucide-react'
import { PageHeader } from '@/components/comum/PageHeader'
import { LinkCodigoBr } from '@/components/comum/LinkCodigoBr'
import { Card, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Input, Textarea } from '@/components/ui/input'
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'
import { ProvedorListaValidacao, useListaValidacao } from '@/features/validacao/ListaValidacaoContext'
import type { AusenteLote, DiffLote, DuplicadoLote, EstadoLink, GapClasse, ItemValidacao, LinkValidacao, PayloadDivergencia } from '@/features/validacao/tipos'
import { CLASSES_PADRAO, decodificarPayload } from '@/features/validacao/tipos'
import { importacaoApi } from '@/features/importacao/importacaoApi'
import type { StatusImportacao } from '@/features/importacao/tipos'
import { listaApi, SemSessaoError } from '@/features/validacao/listaApi'
import { useAuth } from '@/features/auth/AuthContext'
import { useToken } from '@/features/token/TokenContext'
import { cn } from '@/lib/utils'

export function ValidarLote() {
  return (
    <ProvedorListaValidacao>
      <Conteudo />
    </ProvedorListaValidacao>
  )
}

function Conteudo() {
  const { ativos, lixeira, gaps, carregando, erro, adicionarLink, compararLote, validar, alternarRevisao } = useListaValidacao()
  const { usuario, ehAdmin } = useAuth()
  const { status: tokenStatus } = useToken()
  const sessaoCompras = tokenStatus?.estado === 'Saudavel'
  const meuLogin = usuario?.login

  const [aba, setAba] = useState<'ativos' | 'lixeira'>('ativos')
  const [texto, setTexto] = useState('')
  const [classeLote, setClasseLote] = useState<string>('INSUMOS')
  const [extraindo, setExtraindo] = useState(false)
  const [erroExtracao, setErroExtracao] = useState<string | null>(null)
  const [diffRecente, setDiffRecente] = useState<DiffLote | null>(null)
  const [validandoTodos, setValidandoTodos] = useState(false)
  const [progresso, setProgresso] = useState({ feitos: 0, total: 0 })
  const [detalhes, setDetalhes] = useState<LinkValidacao | null>(null)
  const [modalGlobal, setModalGlobal] = useState<'erros' | 'divergencias' | null>(null)
  const [unitarioAberto, setUnitarioAberto] = useState(false)
  const [modoValidar, setModoValidar] = useState<'pendentes' | 'validos' | 'todos'>('pendentes')
  const [filtroClasse, setFiltroClasse] = useState<string>('todas')
  const [filtroBusca, setFiltroBusca] = useState<string>('')
  const [filtroSoErros, setFiltroSoErros] = useState(false)

  // === Importação em andamento — bloqueia toda a UI enquanto roda + cooldown 3s ===
  const { importar: chamarImportar } = useListaValidacao()
  const [imp, setImp] = useState<EstadoImportacaoUi | null>(null)
  const impLinkIdRef = useRef<number | null>(null)
  const cooldownRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  const bloqueado = imp !== null

  const iniciarImportacao = useCallback(async (link: LinkValidacao) => {
    if (impLinkIdRef.current !== null) return
    impLinkIdRef.current = link.id
    setImp({ link, fase: 'enviando', status: null, erro: null, cooldownRestante: 0 })
    try {
      const { id } = await chamarImportar(link.id)
      setImp((prev) => prev ? { ...prev, execucaoId: id, fase: 'executando' } : prev)
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e)
      // Sem sessão no Compras não é falha da planilha: mostra o aviso sem
      // registrar desfecho de importação no link.
      if (!(e instanceof SemSessaoError)) {
        // não temos idExecucao porque o POST inicial falhou — registra com placeholder
        listaApi.registrarResultadoImportacao(link.id, 'envio-falhou', false, msg).catch(() => {})
      }
      setImp((prev) => prev ? { ...prev, fase: 'erro', erro: msg } : prev)
    }
  }, [chamarImportar])

  // Poll de status enquanto execução está em andamento
  useEffect(() => {
    if (!imp || imp.fase !== 'executando' || !imp.execucaoId) return
    let cancelado = false
    const linkId = imp.link.id
    const execId = imp.execucaoId
    const tick = async () => {
      if (cancelado) return
      try {
        const s = await importacaoApi.status(execId)
        if (cancelado) return
        const terminal = s.estado === 'Concluida' || s.estado === 'Falhou' || s.estado === 'Cancelada'
        if (terminal) {
          const sucesso = s.estado === 'Concluida'
          const erroMsg = sucesso ? null : (s.ultimoErro ?? `Importação ${s.estado}`)
          // notifica o backend pra persistir o desfecho no link
          listaApi.registrarResultadoImportacao(linkId, execId, sucesso, erroMsg ?? undefined)
            .catch((e) => console.warn('Falha ao registrar resultado da importação:', e))
        }
        setImp((prev) => prev ? {
          ...prev,
          status: s,
          fase: terminal ? (s.estado === 'Concluida' ? 'cooldown' : 'erro') : 'executando',
          erro: terminal && s.estado !== 'Concluida' ? (s.ultimoErro ?? `Importação ${s.estado}`) : prev.erro,
        } : prev)
      } catch (e) {
        if (cancelado) return
        const msg = e instanceof Error ? e.message : String(e)
        listaApi.registrarResultadoImportacao(linkId, execId, false, msg).catch(() => {})
        setImp((prev) => prev ? { ...prev, fase: 'erro', erro: msg } : prev)
      }
    }
    void tick()
    const interval = setInterval(tick, 700)
    return () => { cancelado = true; clearInterval(interval) }
  }, [imp?.fase, imp?.execucaoId])

  // Cooldown 3s antes de liberar a UI
  useEffect(() => {
    if (!imp) return
    if (imp.fase !== 'cooldown' && imp.fase !== 'erro') return
    let restante = 3
    setImp((prev) => prev ? { ...prev, cooldownRestante: restante } : prev)
    if (cooldownRef.current) clearInterval(cooldownRef.current as unknown as number)
    const id = setInterval(() => {
      restante -= 1
      if (restante <= 0) {
        clearInterval(id)
        impLinkIdRef.current = null
        setImp(null)
      } else {
        setImp((prev) => prev ? { ...prev, cooldownRestante: restante } : prev)
      }
    }, 1000)
    cooldownRef.current = id
    return () => clearInterval(id)
  }, [imp?.fase])

  async function aoCompararLote() {
    setErroExtracao(null)
    setExtraindo(true)
    setDiffRecente(null)
    try {
      const diff = await compararLote(texto, classeLote || undefined)
      setDiffRecente(diff)
      setTexto('')
    } catch (e) {
      setErroExtracao(e instanceof Error ? e.message : String(e))
    } finally {
      setExtraindo(false)
    }
  }

  async function aoValidarTodos(modo: 'pendentes' | 'validos' | 'todos') {
    const fonte = linksFiltrados.length > 0 && linksFiltrados.length < ativos.length ? linksFiltrados : ativos
    const alvos = modo === 'todos'
      ? fonte.slice()
      : modo === 'validos'
        ? fonte.filter((l) => l.estado === 'valido')
        : fonte.filter((l) => l.estado === 'pendente' || l.estado === 'erro' || l.estado === 'invalido')
    if (alvos.length === 0) return
    setValidandoTodos(true)
    setProgresso({ feitos: 0, total: alvos.length })
    let i = 0
    const concorrencia = 3
    let feitos = 0
    async function worker() {
      while (i < alvos.length) {
        const idx = i++
        try { await validar(alvos[idx].id) } catch { /* segue */ }
        feitos++
        setProgresso({ feitos, total: alvos.length })
      }
    }
    await Promise.all(Array.from({ length: concorrencia }, () => worker()))
    setValidandoTodos(false)
  }

  const stats = useMemo(() => {
    let validos = 0, invalidos = 0, erros = 0, pendentes = 0, totalMateriais = 0
    for (const l of ativos) {
      if (l.estado === 'valido') { validos++; totalMateriais += l.totalMateriais ?? 0 }
      else if (l.estado === 'invalido') invalidos++
      else if (l.estado === 'erro') erros++
      else pendentes++
    }
    return { validos, invalidos, erros, pendentes, totalMateriais }
  }, [ativos])

  const todosErros = useMemo<{ link: LinkValidacao; item: ItemValidacao }[]>(() => {
    const lista: { link: LinkValidacao; item: ItemValidacao }[] = []
    for (const link of ativos) for (const e of link.erros) lista.push({ link, item: e })
    return lista
  }, [ativos])

  const todasDivergencias = useMemo<{ link: LinkValidacao; item: ItemValidacao }[]>(() => {
    const lista: { link: LinkValidacao; item: ItemValidacao }[] = []
    for (const link of ativos) for (const d of link.divergencias) lista.push({ link, item: d })
    return lista
  }, [ativos])

  const linksBase = aba === 'ativos' ? ativos : lixeira

  const linksFiltrados = useMemo(() => {
    const busca = filtroBusca.trim().toLowerCase()
    // detecta "INSUMOS 3", "saneantes grupo 5", "med 2", "grupo 7", "3"
    const tokens = busca.split(/\s+/).filter(Boolean)
    const numeroBusca = tokens.map((t) => parseInt(t, 10)).find((n) => !isNaN(n))
    const textoBusca = tokens.filter((t) => isNaN(parseInt(t, 10)) && t !== 'grupo').join(' ')

    return linksBase.filter((l) => {
      if (filtroClasse !== 'todas') {
        if ((l.classe ?? '').toUpperCase() !== filtroClasse) return false
      }
      if (filtroSoErros) {
        const temErro = l.erros.length > 0 || l.divergencias.length > 0
          || l.estado === 'erro' || l.estado === 'invalido'
        if (!temErro) return false
      }
      if (busca) {
        const classeOk = textoBusca ? (l.classe ?? '').toLowerCase().includes(textoBusca) : true
        const numeroOk = numeroBusca != null ? l.numeroGrupo === numeroBusca : true
        const rotuloOk = (l.rotulo ?? '').toLowerCase().includes(busca)
        const urlOk = l.url.toLowerCase().includes(busca)
        // se digitou texto + número, exige os dois baterem; senão, casa por qualquer campo
        if (textoBusca && numeroBusca != null) {
          if (!(classeOk && numeroOk)) {
            // fallback: bate rótulo inteiro
            if (!rotuloOk) return false
          }
        } else if (!(classeOk && numeroOk) && !rotuloOk && !urlOk) {
          return false
        }
      }
      return true
    })
  }, [linksBase, filtroClasse, filtroBusca, filtroSoErros])

  const linksMostrados = linksFiltrados

  return (
    <div className="anim-rise space-y-6">
      <PageHeader
        titulo="Validar em lote"
        descricao="Lista persistente de planilhas em revisão. Adicione, valide, marque itens como conferidos. Em tempo real entre os operadores."
      />

      {/* Adicionar links — colagem em lote (com diff) OU unitário */}
      {aba === 'ativos' && (
        <Card>
          <CardHeader>
            <div className="min-w-0 flex-1">
              <CardTitle><ListChecks />Atualizar a lista</CardTitle>
              <CardDescription>
                Cole um texto com múltiplos links — vamos comparar com a lista atual: adiciona os novos, indica os duplicados e mostra o que está faltando no texto colado.
              </CardDescription>
            </div>
            <Button variant="outline" onClick={() => setUnitarioAberto(true)} disabled={bloqueado}>
              <ListChecks /> Adicionar 1 link
            </Button>
          </CardHeader>
          <div className="p-[18px] space-y-3">
            <div className="flex items-center gap-2 flex-wrap">
              <label className="text-[12px] font-medium text-[hsl(var(--neutral-700))]">Classe deste lote:</label>
              <select
                value={classeLote}
                onChange={(e) => setClasseLote(e.target.value)}
                disabled={extraindo}
                className="h-9 px-2 text-[13px] border border-input rounded-md bg-surface"
              >
                {CLASSES_PADRAO.map((c) => <option key={c} value={c}>{c}</option>)}
              </select>
              <span className="text-[11px] text-muted-foreground">
                A numeração do grupo é detectada do texto ("Grupo X") e validada contra os já cadastrados desta classe.
              </span>
            </div>
            <Textarea
              placeholder={`Grupo 01: https://docs.google.com/spreadsheets/d/...\nGrupo 02: https://docs.google.com/spreadsheets/d/...`}
              value={texto}
              onChange={(e) => setTexto(e.target.value)}
              className="font-mono text-[12px] min-h-[140px]"
              disabled={extraindo}
            />
            {erroExtracao && (
              <Alert variant="destructive">
                <AlertTriangle />
                <AlertDescription>{erroExtracao}</AlertDescription>
              </Alert>
            )}
            <div className="flex gap-2">
              <Button onClick={() => void aoCompararLote()} disabled={extraindo || !texto.trim() || bloqueado}>
                {extraindo ? <Loader2 className="animate-spin" /> : <Search />}
                {extraindo ? 'Comparando…' : 'Comparar lote com a lista'}
              </Button>
              <Button variant="ghost" onClick={() => { setTexto(''); setDiffRecente(null) }} disabled={extraindo || (!texto && !diffRecente) || bloqueado}>
                <Eraser /> Limpar
              </Button>
            </div>
          </div>
        </Card>
      )}

      {/* Resumo do último diff */}
      {diffRecente && (
        <ResumoDiff diff={diffRecente} onFechar={() => setDiffRecente(null)} />
      )}

      <DialogAdicionarUnitario
        aberto={unitarioAberto}
        onFechar={() => setUnitarioAberto(false)}
        onAdicionar={async (url, opts) => {
          await adicionarLink(url, opts)
        }}
      />

      {/* Banner de gaps de numeração por classe */}
      {aba === 'ativos' && gaps.length > 0 && gaps.some((g) => g.faltantes.length > 0) && (
        <Alert variant="warning">
          <AlertTriangle />
          <AlertDescription>
            <div className="space-y-1">
              <div className="font-semibold">Gaps na numeração de grupo:</div>
              {gaps.filter((g) => g.faltantes.length > 0).map((g) => (
                <div key={g.classe} className="text-[12px]">
                  <strong>{g.classe}</strong>: faltam {g.faltantes.map((n) => `Grupo ${n}`).join(', ')}
                  {g.ultimoNumero != null && <> (último cadastrado: Grupo {g.ultimoNumero})</>}
                </div>
              ))}
            </div>
          </AlertDescription>
        </Alert>
      )}

      {/* Abas + Toolbar */}
      <Card>
        <CardHeader>
          <div className="min-w-0 flex-1">
            <CardTitle>
              <CheckCircle2 />
              {aba === 'ativos' ? <>Ativos · {ativos.length}</> : <>Lixeira · {lixeira.length}</>}
            </CardTitle>
            <CardDescription>
              {aba === 'ativos' ? (
                <>
                  {stats.validos > 0 && <>{stats.validos} válidos · </>}
                  {stats.invalidos > 0 && <>{stats.invalidos} com erros · </>}
                  {stats.erros > 0 && <>{stats.erros} falharam · </>}
                  {stats.pendentes > 0 && <>{stats.pendentes} pendentes</>}
                  {stats.totalMateriais > 0 && <> · {stats.totalMateriais.toLocaleString('pt-BR')} materiais somados</>}
                </>
              ) : <>Links excluídos. {ehAdmin ? 'Você é admin: pode restaurar ou apagar definitivo.' : 'Só admin pode restaurar ou apagar.'}</>}
            </CardDescription>
          </div>
          <div className="flex gap-2 flex-wrap justify-end items-center">
            <div className="inline-flex bg-[hsl(var(--neutral-50))] border border-border rounded-md p-0.5 text-[12px]">
              <button
                onClick={() => setAba('ativos')}
                className={cn('px-3 py-1 rounded-sm', aba === 'ativos' ? 'bg-surface shadow-sm' : 'text-muted-foreground')}
              >
                Ativos ({ativos.length})
              </button>
              <button
                onClick={() => setAba('lixeira')}
                className={cn('px-3 py-1 rounded-sm', aba === 'lixeira' ? 'bg-surface shadow-sm' : 'text-muted-foreground')}
              >
                Lixeira ({lixeira.length})
              </button>
            </div>
            {aba === 'ativos' && (
              <>
                {todosErros.length > 0 && !validandoTodos && (
                  <Button variant="outline" onClick={() => setModalGlobal('erros')} disabled={bloqueado}>
                    <XCircle className="text-[hsl(var(--error-500))]" /> Erros ({todosErros.length})
                  </Button>
                )}
                {todasDivergencias.length > 0 && !validandoTodos && (
                  <Button variant="outline" onClick={() => setModalGlobal('divergencias')} disabled={bloqueado}>
                    <Scale className="text-[hsl(20_85%_45%)]" /> Divergências ({todasDivergencias.length})
                  </Button>
                )}
                {ativos.length > 0 && (
                  <>
                    <select
                      value={modoValidar}
                      onChange={(e) => setModoValidar(e.target.value as 'pendentes' | 'validos' | 'todos')}
                      disabled={validandoTodos || bloqueado}
                      className="h-9 px-2 text-[13px] border border-input rounded-md bg-surface"
                    >
                      <option value="pendentes">Só pendentes/erros</option>
                      <option value="validos">Só os já validados</option>
                      <option value="todos">Re-validar todos</option>
                    </select>
                    <Button onClick={() => void aoValidarTodos(modoValidar)} disabled={validandoTodos || bloqueado}>
                      {validandoTodos ? <Loader2 className="animate-spin" /> : <CheckCircle2 />}
                      {validandoTodos
                        ? `Validando ${progresso.feitos}/${progresso.total}…`
                        : modoValidar === 'todos' ? 'Re-validar todos'
                          : modoValidar === 'validos' ? 'Re-validar válidos'
                          : 'Validar pendentes'}
                    </Button>
                  </>
                )}
              </>
            )}
          </div>
        </CardHeader>

        {erro && (
          <div className="px-[18px] pb-3">
            <Alert variant="destructive">
              <AlertTriangle />
              <AlertDescription>{erro}</AlertDescription>
            </Alert>
          </div>
        )}

        {validandoTodos && (
          <div className="px-[18px] pb-3">
            <div className="h-1 rounded-full bg-[hsl(var(--neutral-100))] overflow-hidden">
              <div className="h-full bg-[hsl(var(--brand-500))] transition-all"
                style={{ width: `${(progresso.feitos / Math.max(1, progresso.total)) * 100}%` }} />
            </div>
          </div>
        )}

        {/* Filtros */}
        {linksBase.length > 0 && (
          <div className="px-[18px] pb-3 flex flex-wrap gap-2 items-center">
            <select
              value={filtroClasse}
              onChange={(e) => setFiltroClasse(e.target.value)}
              className="h-9 px-2 text-[13px] border border-input rounded-md bg-surface"
            >
              <option value="todas">Todas as classes</option>
              {CLASSES_PADRAO.map((c) => <option key={c} value={c}>{c}</option>)}
            </select>
            <div className="relative flex-1 min-w-[200px] max-w-[360px]">
              <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 size-3.5 text-muted-foreground" />
              <Input
                placeholder='Buscar por classe + número (ex: "INSUMOS 3", "Grupo 5")'
                value={filtroBusca}
                onChange={(e) => setFiltroBusca(e.target.value)}
                className="pl-8"
              />
            </div>
            <label className="inline-flex items-center gap-1.5 text-[12px] text-muted-foreground select-none">
              <input
                type="checkbox"
                checked={filtroSoErros}
                onChange={(e) => setFiltroSoErros(e.target.checked)}
              />
              Só com erros / divergências
            </label>
            {(filtroClasse !== 'todas' || filtroBusca || filtroSoErros) && (
              <Button
                variant="ghost"
                size="sm"
                onClick={() => { setFiltroClasse('todas'); setFiltroBusca(''); setFiltroSoErros(false) }}
              >
                <Eraser /> Limpar filtros
              </Button>
            )}
            <span className="text-[11.5px] text-muted-foreground ml-auto">
              Mostrando <strong className="text-foreground">{linksFiltrados.length}</strong> de {linksBase.length}
            </span>
          </div>
        )}

        {carregando ? (
          <div className="p-12 text-center text-muted-foreground text-[13px]">
            <Loader2 className="size-5 animate-spin mx-auto mb-2" />
            Carregando lista…
          </div>
        ) : linksMostrados.length === 0 ? (
          <div className="p-12 text-center text-muted-foreground text-[13px]">
            {linksBase.length === 0
              ? (aba === 'ativos' ? 'Nenhum link na lista. Cole URLs acima.' : 'Lixeira vazia.')
              : 'Nenhum link bate com os filtros atuais.'}
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-[12.5px]">
              <thead>
                <tr className="text-left">
                  <Th className="w-[90px]">Status</Th>
                  <Th className="w-[150px]">Rótulo</Th>
                  <Th className="w-[180px]">Descrição</Th>
                  <Th className="w-[240px]">Link</Th>
                  <Th className="w-[80px] text-right">Materiais</Th>
                  <Th className="w-[90px] text-right">Diverg.</Th>
                  <Th className="w-[60px] text-right">Erros</Th>
                  <Th className="w-[260px] text-right">Ações</Th>
                </tr>
              </thead>
              <tbody>
                {agruparPorClasse(linksMostrados).map(([classe, links]) => (
                  <FragmentoClasse
                    key={classe ?? '__sem__'}
                    classe={classe}
                    links={links}
                    aba={aba}
                    ehAdmin={ehAdmin}
                    sessaoCompras={sessaoCompras}
                    onDetalhes={setDetalhes}
                    gaps={gaps}
                    bloqueado={bloqueado}
                    onImportar={(l) => void iniciarImportacao(l)}
                    linkSendoImportado={imp?.link.id ?? null}
                  />
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      <DialogDetalhes
        link={detalhes}
        onFechar={() => setDetalhes(null)}
        meuLogin={meuLogin}
        alternarRevisao={alternarRevisao}
      />

      <ModalErrosGlobal
        aberto={modalGlobal === 'erros'}
        itens={todosErros}
        meuLogin={meuLogin}
        alternarRevisao={alternarRevisao}
        onFechar={() => setModalGlobal(null)}
      />

      <ModalDivergenciasGlobal
        aberto={modalGlobal === 'divergencias'}
        itens={todasDivergencias}
        meuLogin={meuLogin}
        alternarRevisao={alternarRevisao}
        onFechar={() => setModalGlobal(null)}
      />

      <DialogProgressoImportacao imp={imp} />
    </div>
  )
}

function DialogProgressoImportacao({ imp }: { imp: EstadoImportacaoUi | null }) {
  if (!imp) return null
  const s = imp.status
  const totalEtapas = s?.totalEtapas ?? 0
  const feitas = s?.etapasConcluidas ?? 0
  const pct = totalEtapas > 0 ? Math.min(100, Math.round((feitas / totalEtapas) * 100)) : (
    imp.fase === 'enviando' ? 5 : imp.fase === 'cooldown' ? 100 : 0
  )

  const titulo =
    imp.fase === 'enviando' ? 'Enviando para o Compras…'
    : imp.fase === 'executando' ? 'Importando para o PGC…'
    : imp.fase === 'cooldown' ? 'Importação concluída'
    : 'Importação falhou'

  const eventosVisiveis = s?.eventos ?? []

  return (
    <Dialog open={true} onOpenChange={() => { /* não fecha clicando fora */ }}>
      <DialogContent className="sm:max-w-[640px] gap-3">
        <DialogHeader>
          <div className={cn(
            'flex size-10 items-center justify-center rounded-[10px] mb-1',
            imp.fase === 'cooldown' ? 'bg-[hsl(var(--success-50))] text-[hsl(var(--success-600))]'
            : imp.fase === 'erro' ? 'bg-[hsl(var(--error-50))] text-[hsl(var(--error-500))]'
            : 'bg-[hsl(var(--brand-50))] text-[hsl(var(--brand-600))]')}>
            {imp.fase === 'cooldown' ? <CheckCircle2 className="size-5" />
              : imp.fase === 'erro' ? <XCircle className="size-5" />
              : <Loader2 className="size-5 animate-spin" />}
          </div>
          <DialogTitle>{titulo}</DialogTitle>
          <DialogDescription>
            <span className="font-medium text-foreground">{imp.link.rotulo ?? imp.link.idPlanilha}</span>
            {imp.link.classe && <> · {imp.link.classe}</>}
            {imp.link.numeroGrupo != null && <> · Grupo {imp.link.numeroGrupo}</>}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-2">
          <div className="h-2 rounded-full bg-[hsl(var(--neutral-100))] overflow-hidden">
            <div
              className={cn('h-full transition-all',
                imp.fase === 'cooldown' ? 'bg-[hsl(var(--success-500))]'
                : imp.fase === 'erro' ? 'bg-[hsl(var(--error-500))]'
                : 'bg-[hsl(var(--brand-500))]')}
              style={{ width: `${pct}%` }}
            />
          </div>
          <div className="flex justify-between text-[11.5px] text-muted-foreground tabular-nums">
            <span>
              {s?.etapaAtual ?? (imp.fase === 'enviando' ? 'Preparando envio…' : '—')}
            </span>
            <span>
              {totalEtapas > 0 ? `${feitas}/${totalEtapas} etapas` : ''}
              {totalEtapas > 0 ? ' · ' : ''}{pct}%
            </span>
          </div>
        </div>

        {s && (s.totalMateriais > 0 || s.materiaisAdicionados > 0) && (
          <div className="text-[12px] text-muted-foreground">
            Materiais: <strong className="text-foreground tabular-nums">{s.materiaisAdicionados}</strong>
            {s.totalMateriais > 0 && <> / {s.totalMateriais}</>}
            {s.numeroDfd != null && <> · DFD {s.numeroDfd}/{s.anoDfd}</>}
          </div>
        )}

        {eventosVisiveis.length > 0 && (
          <div className="bg-[hsl(var(--neutral-25))] border border-border rounded-md max-h-[260px] overflow-y-auto">
            <div className="sticky top-0 bg-[hsl(var(--neutral-50))] border-b border-border px-2.5 py-1 text-[10.5px] uppercase tracking-[0.05em] font-semibold text-muted-foreground flex justify-between">
              <span>Eventos da execução</span>
              <span className="tabular-nums">{eventosVisiveis.length}</span>
            </div>
            <ul className="divide-y divide-[hsl(var(--neutral-100))]">
              {eventosVisiveis.map((e, i) => {
                const eErro = (e.tipo ?? '').toLowerCase().includes('erro') || (e.tipo ?? '').toLowerCase() === 'falha'
                return (
                  <li key={i} className="text-[11.5px] flex gap-2 px-2.5 py-1.5">
                    <span className="font-mono text-muted-foreground shrink-0 w-[64px] tabular-nums">
                      {new Date(e.ocorridoEm).toLocaleTimeString('pt-BR')}
                    </span>
                    {e.tipo && (
                      <span className={cn(
                        'inline-flex items-center px-1.5 rounded-sm shrink-0 text-[9.5px] uppercase tracking-[0.04em] font-semibold',
                        eErro
                          ? 'bg-[hsl(var(--error-50))] text-[hsl(var(--error-700))]'
                          : 'bg-[hsl(var(--neutral-100))] text-[hsl(var(--neutral-700))]'
                      )}>
                        {e.tipo}
                      </span>
                    )}
                    <div className="flex-1 min-w-0">
                      <div className={cn(eErro && 'text-[hsl(var(--error-700))] font-medium')}>{e.mensagem}</div>
                      {e.detalhe && (
                        <div className="text-[10.5px] text-muted-foreground font-mono break-all mt-0.5">{e.detalhe}</div>
                      )}
                    </div>
                  </li>
                )
              })}
            </ul>
          </div>
        )}

        {imp.erro && (
          <Alert variant="destructive">
            <AlertTriangle />
            <AlertDescription>{imp.erro}</AlertDescription>
          </Alert>
        )}

        <div className="text-[12px] text-muted-foreground text-center">
          {imp.fase === 'enviando' && 'Enviando requisição ao backend…'}
          {imp.fase === 'executando' && 'Processando no PGC. Não saia desta página.'}
          {(imp.fase === 'cooldown' || imp.fase === 'erro') && imp.cooldownRestante > 0 && (
            <>Aguardando <strong className="text-foreground tabular-nums">{imp.cooldownRestante}s</strong> antes de liberar (evitar sobrecarga no Compras)…</>
          )}
        </div>
      </DialogContent>
    </Dialog>
  )
}

// ============================================================================
//  Tipos auxiliares
// ============================================================================

interface EstadoImportacaoUi {
  link: LinkValidacao
  execucaoId?: string
  status: StatusImportacao | null
  fase: 'enviando' | 'executando' | 'cooldown' | 'erro'
  erro: string | null
  cooldownRestante: number
}

// ============================================================================
//  Agrupamento por classe
// ============================================================================

function agruparPorClasse(links: LinkValidacao[]): Array<[string | null, LinkValidacao[]]> {
  const mapa = new Map<string | null, LinkValidacao[]>()
  for (const l of links) {
    const k = l.classe ? l.classe.toUpperCase() : null
    if (!mapa.has(k)) mapa.set(k, [])
    mapa.get(k)!.push(l)
  }
  // ordem definida: CLASSES_PADRAO primeiro, depois outras alfabéticas, sem-classe por último
  const todas = Array.from(mapa.keys())
  todas.sort((a, b) => {
    if (a === null) return 1
    if (b === null) return -1
    const ia = (CLASSES_PADRAO as readonly string[]).indexOf(a)
    const ib = (CLASSES_PADRAO as readonly string[]).indexOf(b)
    if (ia !== -1 || ib !== -1) {
      if (ia === -1) return 1
      if (ib === -1) return -1
      return ia - ib
    }
    return a.localeCompare(b, 'pt-BR')
  })
  return todas.map((k) => [k, mapa.get(k)!.slice().sort((a, b) => {
    const na = a.numeroGrupo ?? Number.MAX_SAFE_INTEGER
    const nb = b.numeroGrupo ?? Number.MAX_SAFE_INTEGER
    if (na !== nb) return na - nb
    return a.id - b.id
  })])
}

function FragmentoClasse({
  classe, links, aba, ehAdmin, sessaoCompras, onDetalhes, gaps, bloqueado, onImportar, linkSendoImportado,
}: {
  classe: string | null
  links: LinkValidacao[]
  aba: 'ativos' | 'lixeira'
  ehAdmin: boolean
  sessaoCompras: boolean
  onDetalhes: (l: LinkValidacao) => void
  gaps: GapClasse[]
  bloqueado: boolean
  onImportar: (l: LinkValidacao) => void
  linkSendoImportado: number | null
}) {
  const gap = classe ? gaps.find((g) => g.classe.toUpperCase() === classe) : null
  return (
    <>
      <tr>
        <td colSpan={8} className="bg-[hsl(var(--neutral-50))] border-y border-border px-3.5 py-1.5">
          <div className="flex items-center gap-2 flex-wrap">
            <span className="text-[11px] uppercase tracking-[0.06em] font-semibold text-[hsl(var(--neutral-700))]">
              {classe ?? 'Sem classe'}
            </span>
            <Badge variant="muted">{links.length}</Badge>
            {gap && gap.faltantes.length > 0 && (
              <span className="text-[11px] text-[hsl(var(--warning-700))]">
                · faltam {gap.faltantes.map((n) => `Grupo ${n}`).join(', ')}
              </span>
            )}
          </div>
        </td>
      </tr>
      {links.map((l) => (
        <LinhaLink
          key={l.id}
          link={l}
          aba={aba}
          ehAdmin={ehAdmin}
          sessaoCompras={sessaoCompras}
          onDetalhes={() => onDetalhes(l)}
          bloqueado={bloqueado}
          onImportar={() => onImportar(l)}
          sendoImportado={linkSendoImportado === l.id}
        />
      ))}
    </>
  )
}

// ============================================================================
//  Linha da tabela
// ============================================================================

function LinhaLink({
  link, aba, ehAdmin, sessaoCompras, onDetalhes, bloqueado, onImportar, sendoImportado,
}: {
  link: LinkValidacao
  aba: 'ativos' | 'lixeira'
  ehAdmin: boolean
  sessaoCompras: boolean
  onDetalhes: () => void
  bloqueado: boolean
  onImportar: () => void
  sendoImportado: boolean
}) {
  const { validar, excluir, restaurar, apagarDefinitivamente } = useListaValidacao()
  const [agindo, setAgindo] = useState<string | null>(null)
  const [erroAcao, setErroAcao] = useState<string | null>(null)

  async function correr<T>(rotulo: string, fn: () => Promise<T>) {
    setAgindo(rotulo)
    setErroAcao(null)
    try { await fn() } catch (e) { setErroAcao(e instanceof Error ? e.message : String(e)) }
    finally { setAgindo(null) }
  }

  // Sessão do Compras NÃO bloqueia o clique: se estiver deslogado, o backend
  // recusa com mensagem clara ("não está logado no Compras.gov") exibida na UI.
  const podeImportar = link.estado === 'valido' && !bloqueado
  const totalRevisaveis = link.erros.length + link.divergencias.length
  const totalRevisados = link.erros.filter((i) => i.revisores.length > 0).length
    + link.divergencias.filter((i) => i.revisores.length > 0).length
  const todoRevisado = totalRevisaveis > 0 && totalRevisados === totalRevisaveis

  return (
    <tr className="border-b border-[hsl(var(--neutral-100))] last:border-b-0">
      <Td>
        <StatusBadge estado={link.estado} />
        {link.importadoEm && (
          <div className="mt-1">
            <Badge variant="success" className="text-[10px]" comDot title={`Importado em ${new Date(link.importadoEm).toLocaleString('pt-BR')}`}>
              importado
            </Badge>
          </div>
        )}
        {!link.importadoEm && link.ultimoErroImportacao && (
          <div className="mt-1">
            <Badge variant="destructive" className="text-[10px]" comDot title={link.ultimoErroImportacao}>
              falhou
            </Badge>
          </div>
        )}
      </Td>
      <Td>
        <div className="font-medium truncate max-w-[140px]" title={link.rotulo ?? undefined}>
          {link.rotulo ?? <span className="text-muted-foreground">—</span>}
        </div>
        {link.criadoPorLogin && (
          <div className="font-mono text-[10px] text-muted-foreground">por {link.criadoPorLogin}</div>
        )}
      </Td>
      <Td>
        <DescricaoCelula descricao={link.descricao} />
      </Td>
      <Td>
        <a
          href={link.url}
          target="_blank"
          rel="noreferrer"
          className="text-[11.5px] text-muted-foreground hover:text-[hsl(var(--brand-600))] inline-flex items-center gap-1 max-w-[240px]"
          title={link.url}
        >
          <ExternalLink className="size-3 shrink-0 opacity-60" />
          <span className="truncate">{link.url}</span>
        </a>
        {erroAcao && (
          <div className="text-[11px] text-[hsl(var(--error-700))] mt-0.5">{erroAcao}</div>
        )}
        {!link.importadoEm && link.ultimoErroImportacao && (
          <div className="text-[10.5px] text-[hsl(var(--error-600))] mt-0.5 truncate max-w-[240px]" title={link.ultimoErroImportacao}>
            <strong>último erro de importação:</strong> {link.ultimoErroImportacao}
          </div>
        )}
      </Td>
      <Td className="text-right font-mono tabular-nums">
        {link.totalMateriais != null ? link.totalMateriais.toLocaleString('pt-BR') : '—'}
      </Td>
      <Td className="text-right font-mono tabular-nums">
        {link.divergencias.length > 0
          ? <span className="font-semibold text-[hsl(20_85%_35%)]">{link.divergencias.length}</span>
          : '0'}
      </Td>
      <Td className="text-right font-mono tabular-nums">
        {link.erros.length > 0 ? <span className="text-[hsl(var(--error-600))] font-semibold">{link.erros.length}</span> : '0'}
      </Td>
      <Td className="text-right">
        <div className="inline-flex items-center gap-1 justify-end flex-wrap">
          {todoRevisado && (
            <Badge variant="success" className="mr-1">{totalRevisados} revisados</Badge>
          )}
          {aba === 'ativos' ? (
            <>
              <Button variant="ghost" size="xs" onClick={() => void correr('validar', () => validar(link.id))} disabled={agindo !== null || bloqueado} title={bloqueado ? 'Aguarde a importação em andamento' : 'Re-validar'}>
                {agindo === 'validar' ? <Loader2 className="animate-spin" /> : <RotateCw />}
              </Button>
              <Button variant="ghost" size="xs" onClick={onDetalhes} disabled={(totalRevisaveis === 0 && link.estado !== 'erro') || bloqueado}>
                Detalhes
              </Button>
              <Button
                variant={podeImportar ? 'primary' : 'ghost'}
                size="xs"
                onClick={onImportar}
                disabled={!podeImportar || agindo !== null}
                title={bloqueado
                  ? (sendoImportado ? 'Importação deste link em andamento' : 'Aguarde a importação em andamento')
                  : link.estado !== 'valido' ? 'Valide com sucesso antes de importar'
                  : !sessaoCompras ? 'Atenção: sem sessão no Compras.gov — a importação será recusada'
                  : 'Importar agora'}
              >
                {sendoImportado ? <Loader2 className="animate-spin" /> : <Play />}
                Importar
              </Button>
              <Button variant="ghost" size="xs" onClick={() => void correr('excluir', () => excluir(link.id))} disabled={agindo !== null || bloqueado} title={bloqueado ? 'Aguarde a importação em andamento' : 'Excluir (vai pra lixeira)'}>
                {agindo === 'excluir' ? <Loader2 className="animate-spin" /> : <Trash2 className="text-[hsl(var(--error-500))]" />}
              </Button>
            </>
          ) : (
            <>
              {ehAdmin ? (
                <>
                  <Button variant="ghost" size="xs" onClick={() => void correr('restaurar', () => restaurar(link.id))} disabled={agindo !== null || bloqueado}>
                    {agindo === 'restaurar' ? <Loader2 className="animate-spin" /> : <Upload className="rotate-180" />} Restaurar
                  </Button>
                  <Button variant="ghost" size="xs" onClick={() => void correr('apagar', () => apagarDefinitivamente(link.id))} disabled={agindo !== null || bloqueado}>
                    {agindo === 'apagar' ? <Loader2 className="animate-spin" /> : <Eraser className="text-[hsl(var(--error-500))]" />} Apagar
                  </Button>
                </>
              ) : (
                <span className="text-[11px] text-muted-foreground inline-flex items-center gap-1">
                  <Shield className="size-3" /> só admin
                </span>
              )}
            </>
          )}
        </div>
      </Td>
    </tr>
  )
}

// ============================================================================
//  Detalhes (por planilha)
// ============================================================================

function DialogDetalhes({
  link, onFechar, meuLogin, alternarRevisao,
}: {
  link: LinkValidacao | null
  onFechar: () => void
  meuLogin: string | undefined
  alternarRevisao: (itemId: number, jaRevisado: boolean) => Promise<void>
}) {
  if (!link) {
    return (
      <Dialog open={false} onOpenChange={onFechar}>
        <DialogContent />
      </Dialog>
    )
  }

  return (
    <Dialog open={true} onOpenChange={(open) => { if (!open) onFechar() }}>
      <DialogContent className="sm:max-w-[860px] max-h-[88vh] flex flex-col gap-3">
        <DialogHeader>
          <DialogTitle className="text-[18px]">{link.rotulo ?? 'Sem rótulo'}</DialogTitle>
          <DialogDescription>
            <a href={link.url} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1 hover:text-[hsl(var(--brand-600))]">
              <ExternalLink className="size-3" />
              <span className="truncate">{link.url}</span>
            </a>
          </DialogDescription>
        </DialogHeader>

        <div className="flex-1 min-h-0 overflow-y-auto space-y-4 -mx-6 px-6">
          {link.erros.length > 0 && (
            <SecaoItens
              titulo={`${link.erros.length} ${link.erros.length === 1 ? 'erro' : 'erros'}`}
              cor="destructive"
              itens={link.erros}
              meuLogin={meuLogin}
              alternarRevisao={alternarRevisao}
            />
          )}
          {link.divergencias.length > 0 && (
            <SecaoItens
              titulo={`${link.divergencias.length} divergência${link.divergencias.length === 1 ? '' : 's'} c/ histórico`}
              cor="warning"
              itens={link.divergencias}
              meuLogin={meuLogin}
              alternarRevisao={alternarRevisao}
            />
          )}
          {link.erros.length === 0 && link.divergencias.length === 0 && (
            <p className="text-[13px] text-muted-foreground text-center py-8">
              Nenhum erro ou divergência. {link.estado === 'valido' ? 'Pronto para importar.' : ''}
            </p>
          )}
        </div>

        <DialogFooter>
          <Button variant="secondary" onClick={onFechar}>Fechar</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

function SecaoItens({
  titulo, cor, itens, meuLogin, alternarRevisao,
}: {
  titulo: string
  cor: 'destructive' | 'warning'
  itens: ItemValidacao[]
  meuLogin: string | undefined
  alternarRevisao: (itemId: number, jaRevisado: boolean) => Promise<void>
}) {
  const borderClass = cor === 'destructive'
    ? 'border-[hsl(var(--error-100))]'
    : 'border-[hsl(20_85%_55%/0.3)]'
  const bgClass = cor === 'destructive'
    ? 'bg-[hsl(var(--error-50))]'
    : 'bg-[hsl(20_85%_55%/0.08)]'
  const textClass = cor === 'destructive'
    ? 'text-[hsl(var(--error-700))]'
    : 'text-[hsl(20_85%_30%)]'

  return (
    <div>
      <div className={cn('text-[11px] uppercase tracking-[0.06em] font-semibold mb-1.5', textClass)}>
        {titulo}
      </div>
      <div className={cn('border rounded-md overflow-hidden', borderClass)}>
        <table className="w-full text-[12.5px]">
          <thead>
            <tr className={bgClass}>
              <th className={cn('px-2.5 py-1.5 text-left text-[10px] uppercase font-semibold w-[40px]', textClass)}>✓</th>
              <th className={cn('px-2.5 py-1.5 text-left text-[10px] uppercase font-semibold w-[60px]', textClass)}>Linha</th>
              <th className={cn('px-2.5 py-1.5 text-left text-[10px] uppercase font-semibold', textClass)}>Detalhe</th>
              <th className={cn('px-2.5 py-1.5 text-left text-[10px] uppercase font-semibold w-[160px]', textClass)}>Revisores</th>
            </tr>
          </thead>
          <tbody>
            {itens.map((it) => (
              <LinhaItem key={it.id} item={it} meuLogin={meuLogin} alternarRevisao={alternarRevisao} />
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}

function LinhaItem({
  item, meuLogin, alternarRevisao,
}: {
  item: ItemValidacao
  meuLogin: string | undefined
  alternarRevisao: (itemId: number, jaRevisado: boolean) => Promise<void>
}) {
  const [pendente, setPendente] = useState(false)
  const jaRevisei = meuLogin ? item.revisores.some((r) => r.login === meuLogin) : false
  const totalRevisores = item.revisores.length
  const revisado = totalRevisores > 0

  async function toggle() {
    if (!meuLogin) return
    setPendente(true)
    try { await alternarRevisao(item.id, jaRevisei) } finally { setPendente(false) }
  }

  return (
    <tr className={cn('border-t', revisado && 'opacity-60')}>
      <td className="px-2.5 py-1.5">
        <button
          onClick={() => void toggle()}
          disabled={pendente}
          className={cn(
            'size-4 rounded border grid place-items-center transition-colors',
            jaRevisei
              ? 'bg-[hsl(var(--success-500))] text-white border-[hsl(var(--success-500))]'
              : 'border-border bg-surface hover:border-foreground/40',
          )}
          title={jaRevisei ? 'Desticar (meu nome)' : 'Marcar como revisado'}
        >
          {pendente ? <Loader2 className="size-2.5 animate-spin" /> : jaRevisei ? <CheckCircle2 className="size-2.5" /> : null}
        </button>
      </td>
      <td className="px-2.5 py-1.5 font-mono text-[11px] tabular-nums text-muted-foreground">
        {item.linha ?? '—'}
      </td>
      <td className={cn('px-2.5 py-1.5 text-[12.5px]', revisado && 'line-through')}>
        <DetalheItem item={item} />
      </td>
      <td className="px-2.5 py-1.5">
        <ChipsRevisores revisores={item.revisores} />
      </td>
    </tr>
  )
}

function DetalheItem({ item }: { item: ItemValidacao }) {
  if (item.tipo === 'erro') {
    return (
      <div>
        {item.codigo && <LinkCodigoBr codigo={item.codigo} className="mr-2" />}
        {item.local && <Badge variant="outline" className="mr-1.5">{item.local}</Badge>}
        {item.campo && <span className="font-mono text-[10.5px] text-muted-foreground mr-1.5">{item.campo}</span>}
        <span>{item.mensagem}</span>
      </div>
    )
  }

  const payload = decodificarPayload<PayloadDivergencia>(item)
  return (
    <div className="flex items-start gap-3 flex-wrap">
      <div className="flex-1 min-w-0">
        {item.codigo && <LinkCodigoBr codigo={item.codigo} className="mr-2" />}
        <span className="text-[10.5px] uppercase tracking-[0.04em] font-semibold text-[hsl(20_85%_35%)] mr-1.5">
          {item.campo === 'preco' ? 'preço' : 'qtd'}
        </span>
        <span>{item.mensagem}</span>
      </div>
      {payload && (
        <div className="text-right shrink-0">
          <div className="font-mono text-[11px] tabular-nums">
            {payload.tipo === 'preco'
              ? payload.valorPlanilha.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
              : payload.valorPlanilha.toLocaleString('pt-BR', { maximumFractionDigits: 2 })}
          </div>
          <div className="font-mono text-[10px] text-muted-foreground">
            faixa {payload.tipo === 'preco'
              ? `${brl(payload.referenciaMin)} – ${brl(payload.referenciaMax)}`
              : `${num(payload.referenciaMin)} – ${num(payload.referenciaMax)}`}
          </div>
          <div className={cn(
            'font-mono text-[11px] tabular-nums font-semibold',
            payload.diferencaPct >= 0 ? 'text-[hsl(20_85%_35%)]' : 'text-[hsl(20_45%_35%)]',
          )}>
            {payload.diferencaPct >= 0 ? '+' : '−'}{(Math.abs(payload.diferencaPct) * 100).toFixed(0)}%
          </div>
        </div>
      )}
    </div>
  )
}

function ChipsRevisores({ revisores }: { revisores: { login: string; revisadoEm: string }[] }) {
  if (revisores.length === 0) {
    return <span className="text-[11px] text-muted-foreground">—</span>
  }
  return (
    <div className="flex flex-wrap gap-1">
      {revisores.map((r) => (
        <span
          key={r.login}
          className="inline-flex items-center gap-1 text-[10.5px] bg-[hsl(var(--success-50))] text-[hsl(var(--success-700))] px-1.5 py-0.5 rounded border border-[hsl(var(--success-100))]"
          title={`Revisado em ${new Date(r.revisadoEm).toLocaleString('pt-BR')}`}
        >
          <CheckCircle2 className="size-2.5" />
          {r.login}
        </span>
      ))}
    </div>
  )
}

// ============================================================================
//  Modais globais
// ============================================================================

interface AgregadoItem { link: LinkValidacao; item: ItemValidacao }

function ModalErrosGlobal({
  aberto, itens, meuLogin, alternarRevisao, onFechar,
}: {
  aberto: boolean
  itens: AgregadoItem[]
  meuLogin: string | undefined
  alternarRevisao: (itemId: number, jaRevisado: boolean) => Promise<void>
  onFechar: () => void
}) {
  const [filtroLocal, setFiltroLocal] = useState('todos')
  const [filtroCampo, setFiltroCampo] = useState('todos')
  const [busca, setBusca] = useState('')
  const [ocultarRevisados, setOcultarRevisados] = useState(false)

  const camposDisp = useMemo(() => {
    const s = new Set<string>()
    itens.forEach((e) => e.item.campo && s.add(e.item.campo))
    return Array.from(s).sort()
  }, [itens])

  const locaisDisp = useMemo(() => {
    const s = new Set<string>()
    itens.forEach((e) => e.item.local && s.add(e.item.local))
    return Array.from(s).sort()
  }, [itens])

  const filtrados = useMemo(() => {
    const lc = busca.trim().toLowerCase()
    return itens.filter((e) => {
      if (filtroLocal !== 'todos' && e.item.local !== filtroLocal) return false
      if (filtroCampo !== 'todos' && e.item.campo !== filtroCampo) return false
      if (ocultarRevisados && e.item.revisores.length > 0) return false
      if (lc) {
        const hay = `${e.item.mensagem} ${e.link.rotulo ?? ''}`.toLowerCase()
        if (!hay.includes(lc)) return false
      }
      return true
    })
  }, [itens, filtroLocal, filtroCampo, busca, ocultarRevisados])

  return (
    <Dialog open={aberto} onOpenChange={(open) => { if (!open) onFechar() }}>
      <DialogContent className="sm:max-w-[1100px] max-h-[90vh] flex flex-col gap-3">
        <DialogHeader>
          <div className="flex size-10 items-center justify-center rounded-[10px] bg-[hsl(var(--error-50))] text-[hsl(var(--error-500))] mb-1">
            <XCircle className="size-5" />
          </div>
          <DialogTitle>Todos os erros · {itens.length}</DialogTitle>
          <DialogDescription>
            Erros agregados de todas as planilhas ativas. Tique os que já foram conferidos — risca e marca seu nome.
          </DialogDescription>
        </DialogHeader>

        <div className="grid grid-cols-1 sm:grid-cols-[150px_220px_180px_1fr] gap-2 items-center">
          <select value={filtroLocal} onChange={(e) => setFiltroLocal(e.target.value)}
            className="h-9 px-2 text-[13px] border border-input rounded-md bg-surface">
            <option value="todos">Todos os locais</option>
            {locaisDisp.map((l) => <option key={l} value={l}>{l}</option>)}
          </select>
          <select value={filtroCampo} onChange={(e) => setFiltroCampo(e.target.value)}
            className="h-9 px-2 text-[13px] border border-input rounded-md bg-surface font-mono">
            <option value="todos">Todos os campos</option>
            {camposDisp.map((c) => <option key={c} value={c}>{c}</option>)}
          </select>
          <label className="inline-flex items-center gap-1.5 text-[12px] text-muted-foreground select-none">
            <input type="checkbox" checked={ocultarRevisados} onChange={(e) => setOcultarRevisados(e.target.checked)} />
            Ocultar revisados
          </label>
          <div className="relative">
            <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 size-3.5 text-muted-foreground" />
            <Input placeholder="Buscar mensagem ou rótulo…" value={busca} onChange={(e) => setBusca(e.target.value)} className="pl-8" />
          </div>
        </div>

        <div className="text-[12px] text-muted-foreground">
          Mostrando <strong className="text-foreground">{filtrados.length}</strong> de {itens.length}
        </div>

        <div className="flex-1 min-h-0 overflow-y-auto border border-border rounded-md">
          <table className="w-full text-[13px]">
            <thead className="sticky top-0 bg-[hsl(var(--neutral-25))]">
              <tr className="text-left">
                <ThSmall className="w-[36px]">✓</ThSmall>
                <ThSmall className="w-[180px]">Planilha</ThSmall>
                <ThSmall className="w-[100px]">Local</ThSmall>
                <ThSmall className="w-[60px]">Linha</ThSmall>
                <ThSmall className="w-[160px]">Campo</ThSmall>
                <ThSmall>Mensagem</ThSmall>
                <ThSmall className="w-[180px]">Revisores</ThSmall>
              </tr>
            </thead>
            <tbody>
              {filtrados.map((e) => (
                <LinhaItemGlobal key={e.item.id} item={e.item} link={e.link} meuLogin={meuLogin} alternarRevisao={alternarRevisao} />
              ))}
              {filtrados.length === 0 && (
                <tr><td colSpan={7} className="px-3 py-8 text-center text-muted-foreground text-[12.5px]">Nada com esses filtros.</td></tr>
              )}
            </tbody>
          </table>
        </div>

        <DialogFooter>
          <Button variant="secondary" onClick={onFechar}>Fechar</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

function ModalDivergenciasGlobal({
  aberto, itens, meuLogin, alternarRevisao, onFechar,
}: {
  aberto: boolean
  itens: AgregadoItem[]
  meuLogin: string | undefined
  alternarRevisao: (itemId: number, jaRevisado: boolean) => Promise<void>
  onFechar: () => void
}) {
  const [filtroTipo, setFiltroTipo] = useState<'todos' | 'preco' | 'qtd'>('todos')
  const [filtroMinPct, setFiltroMinPct] = useState<number>(0)
  const [busca, setBusca] = useState('')
  const [ocultarRevisados, setOcultarRevisados] = useState(false)
  const [ordem, setOrdem] = useState<'pct-desc' | 'pct-asc' | 'natural'>('pct-desc')

  const filtrados = useMemo(() => {
    const lc = busca.trim().toLowerCase()
    const arr = itens.filter((e) => {
      const tipo = e.item.campo
      if (filtroTipo !== 'todos' && tipo !== filtroTipo) return false
      const delta = Math.abs(e.item.deltaPct ?? 0)
      if (delta * 100 < filtroMinPct) return false
      if (ocultarRevisados && e.item.revisores.length > 0) return false
      if (lc) {
        const hay = `${e.item.codigo ?? ''} ${e.item.mensagem} ${e.link.rotulo ?? ''}`.toLowerCase()
        if (!hay.includes(lc)) return false
      }
      return true
    })
    if (ordem === 'pct-desc') arr.sort((a, b) => Math.abs(b.item.deltaPct ?? 0) - Math.abs(a.item.deltaPct ?? 0))
    else if (ordem === 'pct-asc') arr.sort((a, b) => Math.abs(a.item.deltaPct ?? 0) - Math.abs(b.item.deltaPct ?? 0))
    return arr
  }, [itens, filtroTipo, filtroMinPct, busca, ordem, ocultarRevisados])

  return (
    <Dialog open={aberto} onOpenChange={(open) => { if (!open) onFechar() }}>
      <DialogContent className="sm:max-w-[1100px] max-h-[90vh] flex flex-col gap-3">
        <DialogHeader>
          <div className="flex size-10 items-center justify-center rounded-[10px] bg-[hsl(20_85%_55%/0.15)] text-[hsl(20_85%_45%)] mb-1">
            <Scale className="size-5" />
          </div>
          <DialogTitle>Divergências c/ histórico · {itens.length}</DialogTitle>
          <DialogDescription>
            Valores fora da banda histórica (min/max + 50% margem). Tique os já conferidos pra esconder do dia-a-dia.
          </DialogDescription>
        </DialogHeader>

        <div className="grid grid-cols-1 sm:grid-cols-[140px_160px_140px_140px_1fr] gap-2 items-center">
          <select value={filtroTipo} onChange={(e) => setFiltroTipo(e.target.value as 'todos' | 'preco' | 'qtd')}
            className="h-9 px-2 text-[13px] border border-input rounded-md bg-surface">
            <option value="todos">Preço e qtd</option>
            <option value="preco">Só preço</option>
            <option value="qtd">Só quantidade</option>
          </select>
          <select value={filtroMinPct} onChange={(e) => setFiltroMinPct(Number(e.target.value))}
            className="h-9 px-2 text-[13px] border border-input rounded-md bg-surface">
            <option value={0}>Δ mínima: qualquer</option>
            <option value={100}>Δ ≥ 100%</option>
            <option value={500}>Δ ≥ 500%</option>
            <option value={1000}>Δ ≥ 1.000%</option>
            <option value={10000}>Δ ≥ 10.000%</option>
          </select>
          <select value={ordem} onChange={(e) => setOrdem(e.target.value as 'pct-desc' | 'pct-asc' | 'natural')}
            className="h-9 px-2 text-[13px] border border-input rounded-md bg-surface">
            <option value="pct-desc">Maior Δ primeiro</option>
            <option value="pct-asc">Menor Δ primeiro</option>
            <option value="natural">Ordem original</option>
          </select>
          <label className="inline-flex items-center gap-1.5 text-[12px] text-muted-foreground select-none">
            <input type="checkbox" checked={ocultarRevisados} onChange={(e) => setOcultarRevisados(e.target.checked)} />
            Ocultar revisados
          </label>
          <div className="relative">
            <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 size-3.5 text-muted-foreground" />
            <Input placeholder="Buscar código ou rótulo…" value={busca} onChange={(e) => setBusca(e.target.value)} className="pl-8" />
          </div>
        </div>

        <div className="text-[12px] text-muted-foreground">
          Mostrando <strong className="text-foreground">{filtrados.length}</strong> de {itens.length}
        </div>

        <div className="flex-1 min-h-0 overflow-y-auto border border-[hsl(20_85%_55%/0.3)] rounded-md">
          <table className="w-full text-[13px]">
            <thead className="sticky top-0 bg-[hsl(20_85%_55%/0.08)]">
              <tr className="text-left">
                <ThSmallLaranja className="w-[36px]">✓</ThSmallLaranja>
                <ThSmallLaranja className="w-[160px]">Planilha</ThSmallLaranja>
                <ThSmallLaranja className="w-[60px]">Linha</ThSmallLaranja>
                <ThSmallLaranja className="w-[90px]">Código</ThSmallLaranja>
                <ThSmallLaranja className="w-[60px]">Campo</ThSmallLaranja>
                <ThSmallLaranja className="w-[120px] text-right">Planilha</ThSmallLaranja>
                <ThSmallLaranja className="w-[160px] text-right">Faixa histórica</ThSmallLaranja>
                <ThSmallLaranja className="w-[80px] text-right">Δ</ThSmallLaranja>
                <ThSmallLaranja className="w-[170px]">Revisores</ThSmallLaranja>
              </tr>
            </thead>
            <tbody>
              {filtrados.map((e) => (
                <LinhaDivGlobal key={e.item.id} item={e.item} link={e.link} meuLogin={meuLogin} alternarRevisao={alternarRevisao} />
              ))}
              {filtrados.length === 0 && (
                <tr><td colSpan={9} className="px-3 py-8 text-center text-muted-foreground text-[12.5px]">Nada com esses filtros.</td></tr>
              )}
            </tbody>
          </table>
        </div>

        <DialogFooter>
          <Button variant="secondary" onClick={onFechar}>Fechar</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

function LinhaItemGlobal({
  item, link, meuLogin, alternarRevisao,
}: {
  item: ItemValidacao
  link: LinkValidacao
  meuLogin: string | undefined
  alternarRevisao: (itemId: number, jaRevisado: boolean) => Promise<void>
}) {
  const [pendente, setPendente] = useState(false)
  const jaRevisei = meuLogin ? item.revisores.some((r) => r.login === meuLogin) : false
  const revisado = item.revisores.length > 0
  return (
    <tr className={cn('border-b border-[hsl(var(--neutral-100))] last:border-b-0 hover:bg-[hsl(var(--neutral-25))]', revisado && 'opacity-60')}>
      <td className="px-3 py-2">
        <button
          onClick={async () => { setPendente(true); try { await alternarRevisao(item.id, jaRevisei) } finally { setPendente(false) } }}
          disabled={pendente || !meuLogin}
          className={cn(
            'size-4 rounded border grid place-items-center',
            jaRevisei
              ? 'bg-[hsl(var(--success-500))] text-white border-[hsl(var(--success-500))]'
              : 'border-border bg-surface hover:border-foreground/40',
          )}
        >
          {pendente ? <Loader2 className="size-2.5 animate-spin" /> : jaRevisei ? <CheckCircle2 className="size-2.5" /> : null}
        </button>
      </td>
      <td className="px-3 py-2">
        <a href={link.url} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1.5 text-[12.5px] font-medium hover:text-[hsl(var(--brand-600))]">
          <ExternalLink className="size-3 opacity-60" />
          <span className="truncate max-w-[150px]">{link.rotulo ?? '—'}</span>
        </a>
      </td>
      <td className="px-3 py-2"><Badge variant="outline">{item.local}</Badge></td>
      <td className="px-3 py-2 font-mono text-[11.5px] tabular-nums text-muted-foreground">{item.linha ?? '—'}</td>
      <td className="px-3 py-2 font-mono text-[11.5px]">{item.campo}</td>
      <td className={cn('px-3 py-2 text-[12.5px]', revisado && 'line-through')}>{item.mensagem}</td>
      <td className="px-3 py-2"><ChipsRevisores revisores={item.revisores} /></td>
    </tr>
  )
}

function LinhaDivGlobal({
  item, link, meuLogin, alternarRevisao,
}: {
  item: ItemValidacao
  link: LinkValidacao
  meuLogin: string | undefined
  alternarRevisao: (itemId: number, jaRevisado: boolean) => Promise<void>
}) {
  const [pendente, setPendente] = useState(false)
  const jaRevisei = meuLogin ? item.revisores.some((r) => r.login === meuLogin) : false
  const revisado = item.revisores.length > 0
  const payload = decodificarPayload<PayloadDivergencia>(item)
  return (
    <tr className={cn('border-b border-[hsl(20_85%_55%/0.15)] last:border-b-0 hover:bg-[hsl(20_85%_55%/0.04)]', revisado && 'opacity-60')}>
      <td className="px-3 py-2">
        <button
          onClick={async () => { setPendente(true); try { await alternarRevisao(item.id, jaRevisei) } finally { setPendente(false) } }}
          disabled={pendente || !meuLogin}
          className={cn(
            'size-4 rounded border grid place-items-center',
            jaRevisei
              ? 'bg-[hsl(var(--success-500))] text-white border-[hsl(var(--success-500))]'
              : 'border-border bg-surface hover:border-foreground/40',
          )}
        >
          {pendente ? <Loader2 className="size-2.5 animate-spin" /> : jaRevisei ? <CheckCircle2 className="size-2.5" /> : null}
        </button>
      </td>
      <td className="px-3 py-2">
        <a href={link.url} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1.5 text-[12.5px] font-medium hover:text-[hsl(var(--brand-600))]">
          <ExternalLink className="size-3 opacity-60" />
          <span className="truncate max-w-[130px]">{link.rotulo ?? '—'}</span>
        </a>
      </td>
      <td className="px-3 py-2 font-mono text-[11.5px] tabular-nums text-muted-foreground">{item.linha ?? '—'}</td>
      <td className="px-3 py-2 font-mono text-[11.5px]">
        {item.codigo ? <LinkCodigoBr codigo={item.codigo} /> : '—'}
      </td>
      <td className="px-3 py-2 text-[10.5px] uppercase font-semibold text-[hsl(20_85%_35%)]">
        {item.campo === 'preco' ? 'preço' : 'qtd'}
      </td>
      <td className="px-3 py-2 font-mono text-[12px] tabular-nums text-right">
        {payload ? (payload.tipo === 'preco' ? brl(payload.valorPlanilha) : num(payload.valorPlanilha)) : '—'}
      </td>
      <td className="px-3 py-2 font-mono text-[11.5px] tabular-nums text-right text-muted-foreground">
        {payload ? (payload.tipo === 'preco'
          ? `${brl(payload.referenciaMin)} – ${brl(payload.referenciaMax)}`
          : `${num(payload.referenciaMin)} – ${num(payload.referenciaMax)}`)
          : '—'}
        {payload && <span className="block text-[10px]">{payload.totalRegistros} reg · {payload.siglaReferencia}</span>}
      </td>
      <td className="px-3 py-2 font-mono text-[12px] tabular-nums text-right">
        {item.deltaPct != null && (
          <span className={item.deltaPct >= 0 ? 'text-[hsl(20_85%_35%)] font-semibold' : 'text-[hsl(20_45%_35%)]'}>
            {item.deltaPct >= 0 ? '+' : '−'}{(Math.abs(item.deltaPct) * 100).toFixed(0)}%
          </span>
        )}
      </td>
      <td className="px-3 py-2"><ChipsRevisores revisores={item.revisores} /></td>
    </tr>
  )
}

// ============================================================================
//  Componentes auxiliares
// ============================================================================

/**
 * Mostra a descrição da DFD truncada — mantém os primeiros 2 "grupos" (palavras)
 * mais um pouco do contexto, com a versão integral no tooltip nativo.
 */
function DescricaoCelula({ descricao }: { descricao: string | null }) {
  if (!descricao || !descricao.trim()) {
    return <span className="text-[11px] text-muted-foreground">—</span>
  }
  const palavras = descricao.trim().split(/\s+/)
  // pelo menos 2 grupos de palavras (~4 palavras), respeitando largura
  const preview = palavras.slice(0, 4).join(' ')
  const truncado = palavras.length > 4
  return (
    <div className="max-w-[180px]" title={descricao}>
      <div className="text-[12px] leading-[1.3] line-clamp-2 break-words">
        {preview}{truncado && '…'}
      </div>
    </div>
  )
}

function StatusBadge({ estado }: { estado: EstadoLink }) {
  if (estado === 'pendente') return <Badge variant="muted">pendente</Badge>
  if (estado === 'validando') return (
    <span className="inline-flex items-center gap-1.5 text-[11px] text-[hsl(var(--brand-700))] font-semibold uppercase tracking-[0.04em]">
      <Loader2 className="size-3 animate-spin" /> validando
    </span>
  )
  if (estado === 'valido') return <Badge variant="success" comDot>válido</Badge>
  if (estado === 'invalido') return <Badge variant="destructive" comDot>com erros</Badge>
  return <Badge variant="destructive" comDot>falhou</Badge>
}

function Th({ children, className }: { children: React.ReactNode; className?: string }) {
  return <th className={cn('px-3.5 py-2.5 text-[11px] uppercase tracking-[0.05em] font-semibold text-muted-foreground bg-[hsl(var(--neutral-25))] border-b border-border', className)}>{children}</th>
}
function Td({ children, className }: { children: React.ReactNode; className?: string }) {
  return <td className={cn('px-3.5 py-1.5 align-middle', className)}>{children}</td>
}
function ThSmall({ children, className }: { children: React.ReactNode; className?: string }) {
  return <th className={cn('px-3 py-2 text-[10.5px] uppercase tracking-[0.05em] font-semibold text-muted-foreground border-b border-border', className)}>{children}</th>
}
function ThSmallLaranja({ children, className }: { children: React.ReactNode; className?: string }) {
  return <th className={cn('px-3 py-2 text-[10.5px] uppercase tracking-[0.05em] font-semibold text-[hsl(20_85%_30%)] border-b border-[hsl(20_85%_55%/0.2)]', className)}>{children}</th>
}

const brl = (v: number) => v.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
const num = (v: number) => v.toLocaleString('pt-BR', { maximumFractionDigits: 2 })

// ============================================================================
//  Resumo do diff (comparar lote)
// ============================================================================

function ResumoDiff({ diff, onFechar }: { diff: DiffLote; onFechar: () => void }) {
  const [secao, setSecao] = useState<'adicionados' | 'duplicados' | 'ausentes'>(
    diff.adicionados.length > 0 ? 'adicionados' :
    diff.duplicados.length > 0 ? 'duplicados' : 'ausentes'
  )

  const nada = diff.adicionados.length === 0 && diff.duplicados.length === 0 && diff.ausentes.length === 0

  return (
    <Card className="border-[hsl(var(--brand-200))]">
      <CardHeader>
        <div className="min-w-0 flex-1">
          <CardTitle><Search />Comparação concluída</CardTitle>
          <CardDescription>
            <span className="text-[hsl(var(--success-700))]">{diff.adicionados.length} adicionados</span>
            {' · '}
            <span className="text-[hsl(var(--warning-700))]">{diff.duplicados.length} já estavam</span>
            {' · '}
            <span className="text-[hsl(var(--info-700))]">{diff.ausentes.length} ausentes do texto colado</span>
            {nada && ' · nenhuma URL encontrada no texto'}
          </CardDescription>
        </div>
        <Button variant="ghost" size="sm" onClick={onFechar}>Fechar</Button>
      </CardHeader>

      {!nada && (
        <>
          <div className="px-[18px] flex gap-1">
            <BotaoSecao ativo={secao === 'adicionados'} onClick={() => setSecao('adicionados')}>
              Adicionados ({diff.adicionados.length})
            </BotaoSecao>
            <BotaoSecao ativo={secao === 'duplicados'} onClick={() => setSecao('duplicados')}>
              Já estavam ({diff.duplicados.length})
            </BotaoSecao>
            <BotaoSecao ativo={secao === 'ausentes'} onClick={() => setSecao('ausentes')}>
              Ausentes ({diff.ausentes.length})
            </BotaoSecao>
          </div>

          <div className="px-[18px] pb-[18px] pt-3">
            {secao === 'adicionados' && (
              diff.adicionados.length === 0 ? <VazioDiff>Nada novo no texto.</VazioDiff> : (
                <ListaSimplesLinks itens={diff.adicionados.map((l) => ({ id: l.id, rotulo: l.rotulo, url: l.url, idPlanilha: l.idPlanilha }))} />
              )
            )}
            {secao === 'duplicados' && (
              diff.duplicados.length === 0 ? <VazioDiff>Nenhuma URL do texto já estava cadastrada.</VazioDiff> : (
                <ListaDuplicados itens={diff.duplicados} />
              )
            )}
            {secao === 'ausentes' && (
              diff.ausentes.length === 0 ? <VazioDiff>Todos os links da lista atual apareceram no texto.</VazioDiff> : (
                <ListaAusentes itens={diff.ausentes} />
              )
            )}
          </div>
        </>
      )}
    </Card>
  )
}

function BotaoSecao({ ativo, onClick, children }: { ativo: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      onClick={onClick}
      className={cn(
        'px-3 py-1.5 text-[12px] font-medium rounded-md',
        ativo ? 'bg-[hsl(var(--brand-50))] text-[hsl(var(--brand-700))]' : 'text-muted-foreground hover:bg-[hsl(var(--neutral-50))]',
      )}
    >
      {children}
    </button>
  )
}

function VazioDiff({ children }: { children: React.ReactNode }) {
  return <div className="text-[13px] text-muted-foreground py-6 text-center">{children}</div>
}

function ListaSimplesLinks({ itens }: { itens: { id: number; rotulo: string | null; url: string; idPlanilha: string }[] }) {
  return (
    <ul className="divide-y divide-border border border-border rounded-md">
      {itens.map((l) => (
        <li key={l.id} className="flex items-center gap-3 px-3 py-2 text-[13px]">
          <Badge variant="success">novo</Badge>
          <span className="font-medium truncate flex-1">{l.rotulo ?? l.idPlanilha}</span>
          <a href={l.url} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1 text-[11.5px] text-muted-foreground hover:text-[hsl(var(--brand-600))] truncate max-w-[420px]">
            <ExternalLink className="size-3 opacity-60" />
            <span className="truncate">{l.url}</span>
          </a>
        </li>
      ))}
    </ul>
  )
}

function ListaDuplicados({ itens }: { itens: DuplicadoLote[] }) {
  return (
    <ul className="divide-y divide-border border border-border rounded-md">
      {itens.map((d) => (
        <li key={d.linkExistenteId} className="flex items-center gap-3 px-3 py-2 text-[13px]">
          <Badge variant="warning">já estava</Badge>
          <div className="flex-1 min-w-0">
            <div className="font-medium truncate">{d.rotuloExistente ?? d.idPlanilha}</div>
            {d.rotuloColado && d.rotuloColado !== d.rotuloExistente && (
              <div className="text-[11px] text-muted-foreground">colado como: {d.rotuloColado}</div>
            )}
          </div>
          <a href={d.url} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1 text-[11.5px] text-muted-foreground hover:text-[hsl(var(--brand-600))] truncate max-w-[380px]">
            <ExternalLink className="size-3 opacity-60" />
            <span className="truncate">{d.url}</span>
          </a>
        </li>
      ))}
    </ul>
  )
}

function ListaAusentes({ itens }: { itens: AusenteLote[] }) {
  return (
    <div className="space-y-2">
      <div className="text-[12px] text-muted-foreground">
        Estes links já estavam na lista ativa, mas não apareceram no texto que você colou. Eles continuam ativos — se quiser, exclua manualmente.
      </div>
      <ul className="divide-y divide-border border border-border rounded-md">
        {itens.map((a) => (
          <li key={a.linkId} className="flex items-center gap-3 px-3 py-2 text-[13px]">
            <Badge variant="info">ausente</Badge>
            <span className="font-medium truncate flex-1">{a.rotulo ?? a.idPlanilha}</span>
            <span className="text-[11px] text-muted-foreground">{a.estado}</span>
            <a href={a.url} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1 text-[11.5px] text-muted-foreground hover:text-[hsl(var(--brand-600))] truncate max-w-[380px]">
              <ExternalLink className="size-3 opacity-60" />
              <span className="truncate">{a.url}</span>
            </a>
          </li>
        ))}
      </ul>
    </div>
  )
}

// ============================================================================
//  Dialog: adicionar 1 link (sem checagem de duplicado/ausente)
// ============================================================================

function DialogAdicionarUnitario({
  aberto, onFechar, onAdicionar,
}: {
  aberto: boolean
  onFechar: () => void
  onAdicionar: (url: string, opts?: { rotulo?: string; classe?: string; numeroGrupo?: number }) => Promise<void>
}) {
  const [url, setUrl] = useState('')
  const [classe, setClasse] = useState<string>('INSUMOS')
  const [numero, setNumero] = useState<string>('')
  const [enviando, setEnviando] = useState(false)
  const [erroLocal, setErroLocal] = useState<string | null>(null)

  function reset() {
    setUrl('')
    setNumero('')
    setErroLocal(null)
  }

  async function aoConfirmar() {
    setEnviando(true)
    setErroLocal(null)
    try {
      const n = numero.trim() ? parseInt(numero.trim(), 10) : undefined
      if (numero.trim() && (isNaN(n!) || n! < 1)) {
        throw new Error('Número do grupo deve ser inteiro ≥ 1.')
      }
      await onAdicionar(url.trim(), {
        classe: classe || undefined,
        numeroGrupo: n,
      })
      reset()
      onFechar()
    } catch (e) {
      setErroLocal(e instanceof Error ? e.message : String(e))
    } finally {
      setEnviando(false)
    }
  }

  return (
    <Dialog open={aberto} onOpenChange={(open) => { if (!open && !enviando) { reset(); onFechar() } }}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <div className="flex size-10 items-center justify-center rounded-[10px] bg-[hsl(var(--brand-50))] text-[hsl(var(--brand-600))] mb-1">
            <ListChecks className="size-5" />
          </div>
          <DialogTitle>Adicionar 1 link</DialogTitle>
          <DialogDescription>
            Adição direta — não compara com a lista atual. O rótulo será gerado como "Grupo X" se você informar o número.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-3">
          <div className="space-y-1.5">
            <label className="text-[12px] font-medium text-[hsl(var(--neutral-700))]">URL do Google Sheets</label>
            <Input
              value={url}
              onChange={(e) => setUrl(e.target.value)}
              placeholder="https://docs.google.com/spreadsheets/d/..."
              className="font-mono text-[12px]"
              autoFocus
            />
          </div>
          <div className="grid grid-cols-2 gap-2">
            <div className="space-y-1.5">
              <label className="text-[12px] font-medium text-[hsl(var(--neutral-700))]">Classe</label>
              <select
                value={classe}
                onChange={(e) => setClasse(e.target.value)}
                className="h-9 w-full px-2 text-[13px] border border-input rounded-md bg-surface"
              >
                {CLASSES_PADRAO.map((c) => <option key={c} value={c}>{c}</option>)}
              </select>
            </div>
            <div className="space-y-1.5">
              <label className="text-[12px] font-medium text-[hsl(var(--neutral-700))]">Número do grupo</label>
              <Input
                value={numero}
                onChange={(e) => setNumero(e.target.value.replace(/\D/g, ''))}
                placeholder="Ex: 42"
                inputMode="numeric"
              />
            </div>
          </div>
          {erroLocal && (
            <Alert variant="destructive">
              <AlertTriangle />
              <AlertDescription>{erroLocal}</AlertDescription>
            </Alert>
          )}
        </div>
        <DialogFooter>
          <Button variant="secondary" onClick={onFechar} disabled={enviando}>Cancelar</Button>
          <Button onClick={() => void aoConfirmar()} disabled={enviando || !url.trim()}>
            {enviando && <Loader2 className="animate-spin" />}
            {enviando ? 'Adicionando…' : 'Adicionar'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
