import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { listaApi } from './listaApi'
import { ListaValidacaoHubClient } from './listaHub'
import type { DiffLote, GapClasse, LinkValidacao, ListaValidacao, Revisor } from './tipos'

interface Valor {
  ativos: LinkValidacao[]
  lixeira: LinkValidacao[]
  gaps: GapClasse[]
  carregando: boolean
  erro: string | null

  adicionarLink(url: string, opts?: { rotulo?: string; classe?: string; numeroGrupo?: number }): Promise<void>
  extrairLinks(texto: string, classe?: string): Promise<LinkValidacao[]>
  compararLote(texto: string, classe?: string): Promise<DiffLote>
  validar(linkId: number): Promise<void>
  excluir(linkId: number): Promise<void>
  restaurar(linkId: number): Promise<void>
  apagarDefinitivamente(linkId: number): Promise<void>
  alternarRevisao(itemId: number, jaRevisado: boolean): Promise<void>
  importar(linkId: number, confirmarDuplicado?: boolean): Promise<{ id: string }>
  recarregar(): Promise<void>
}

const Ctx = createContext<Valor | null>(null)

export function ProvedorListaValidacao({ children }: { children: ReactNode }) {
  const [ativos, setAtivos] = useState<LinkValidacao[]>([])
  const [lixeira, setLixeira] = useState<LinkValidacao[]>([])
  const [gaps, setGaps] = useState<GapClasse[]>([])
  const [carregando, setCarregando] = useState(true)
  const [erro, setErro] = useState<string | null>(null)
  const hubRef = useRef<ListaValidacaoHubClient | null>(null)

  const recarregar = useCallback(async () => {
    try {
      setErro(null)
      const dados: ListaValidacao = await listaApi.obter()
      setAtivos(dados.ativos)
      setLixeira(dados.lixeira)
      setGaps(dados.gaps ?? [])
    } catch (e) {
      setErro(e instanceof Error ? e.message : String(e))
    } finally {
      setCarregando(false)
    }
  }, [])

  useEffect(() => {
    void recarregar()
  }, [recarregar])

  useEffect(() => {
    const hub = new ListaValidacaoHubClient()
    hubRef.current = hub

    const off: Array<() => void> = []

    off.push(hub.onLinkAdicionado((link) => {
      setAtivos((prev) => prev.some((l) => l.id === link.id) ? prev : [...prev, link])
    }))
    off.push(hub.onLinkAtualizado((link) => {
      setAtivos((prev) => prev.map((l) => l.id === link.id ? link : l))
      setLixeira((prev) => prev.map((l) => l.id === link.id ? link : l))
    }))
    off.push(hub.onLinkExcluido(({ linkId }) => {
      setAtivos((prev) => {
        const removido = prev.find((l) => l.id === linkId)
        if (removido) {
          setLixeira((lx) => [...lx, { ...removido, excluidoEm: new Date().toISOString() }])
        }
        return prev.filter((l) => l.id !== linkId)
      })
    }))
    off.push(hub.onLinkRestaurado((link) => {
      setLixeira((prev) => prev.filter((l) => l.id !== link.id))
      setAtivos((prev) => prev.some((l) => l.id === link.id) ? prev : [...prev, link])
    }))
    off.push(hub.onLinkApagado(({ linkId }) => {
      setLixeira((prev) => prev.filter((l) => l.id !== linkId))
    }))
    off.push(hub.onItemRevisado(({ itemId, revisores }) => {
      const atualizar = (l: LinkValidacao): LinkValidacao => ({
        ...l,
        erros: l.erros.map((i) => i.id === itemId ? { ...i, revisores } : i),
        divergencias: l.divergencias.map((i) => i.id === itemId ? { ...i, revisores } : i),
      })
      setAtivos((prev) => prev.map(atualizar))
      setLixeira((prev) => prev.map(atualizar))
    }))

    void hub.iniciar().catch((e) => console.warn('Hub /lista-validacao falhou ao iniciar:', e))

    return () => {
      off.forEach((fn) => fn())
      void hub.parar()
      hubRef.current = null
    }
  }, [])

  const adicionarLink = useCallback(async (url: string, opts?: { rotulo?: string; classe?: string; numeroGrupo?: number }) => {
    const novo = await listaApi.adicionarLink(url, opts)
    setAtivos((prev) => prev.some((l) => l.id === novo.id) ? prev : [...prev, novo])
    void recarregar()
  }, [recarregar])

  const extrairLinks = useCallback(async (texto: string, classe?: string) => {
    const novos = await listaApi.extrairLinks(texto, classe)
    setAtivos((prev) => {
      const map = new Map(prev.map((l) => [l.id, l]))
      for (const n of novos) map.set(n.id, n)
      return Array.from(map.values()).sort((a, b) => a.id - b.id)
    })
    void recarregar()
    return novos
  }, [recarregar])

  const compararLote = useCallback(async (texto: string, classe?: string) => {
    const diff = await listaApi.compararLote(texto, classe)
    setAtivos((prev) => {
      const map = new Map(prev.map((l) => [l.id, l]))
      for (const n of diff.adicionados) map.set(n.id, n)
      return Array.from(map.values()).sort((a, b) => a.id - b.id)
    })
    void recarregar()
    return diff
  }, [recarregar])

  const validar = useCallback(async (linkId: number) => {
    const link = await listaApi.validar(linkId)
    setAtivos((prev) => prev.map((l) => l.id === link.id ? link : l))
  }, [])

  const excluir = useCallback(async (linkId: number) => {
    await listaApi.excluir(linkId)
  }, [])

  const restaurar = useCallback(async (linkId: number) => {
    await listaApi.restaurar(linkId)
  }, [])

  const apagarDefinitivamente = useCallback(async (linkId: number) => {
    await listaApi.apagarDefinitivamente(linkId)
  }, [])

  const alternarRevisao = useCallback(async (itemId: number, jaRevisado: boolean) => {
    // otimismo: já manda evento síncrono e deixa o broadcast confirmar
    const revisores: Revisor[] = jaRevisado
      ? await listaApi.desrevisar(itemId)
      : await listaApi.revisar(itemId)
    const aplicar = (l: LinkValidacao) => ({
      ...l,
      erros: l.erros.map((i) => i.id === itemId ? { ...i, revisores } : i),
      divergencias: l.divergencias.map((i) => i.id === itemId ? { ...i, revisores } : i),
    })
    setAtivos((prev) => prev.map(aplicar))
    setLixeira((prev) => prev.map(aplicar))
  }, [])

  const importar = useCallback(async (linkId: number, confirmarDuplicado = false) => {
    return listaApi.importar(linkId, confirmarDuplicado)
  }, [])

  const valor = useMemo<Valor>(() => ({
    ativos, lixeira, gaps, carregando, erro,
    adicionarLink, extrairLinks, compararLote, validar, excluir, restaurar, apagarDefinitivamente,
    alternarRevisao, importar, recarregar,
  }), [ativos, lixeira, gaps, carregando, erro, adicionarLink, extrairLinks, compararLote, validar, excluir, restaurar, apagarDefinitivamente, alternarRevisao, importar, recarregar])

  return <Ctx.Provider value={valor}>{children}</Ctx.Provider>
}

export function useListaValidacao(): Valor {
  const c = useContext(Ctx)
  if (!c) throw new Error('useListaValidacao fora do ProvedorListaValidacao')
  return c
}
