import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr'
import type { LinkValidacao, Revisor } from './tipos'

type Listener<T> = (payload: T) => void

export class ListaValidacaoHubClient {
  private conexao: HubConnection
  private promessaStart: Promise<void> | null = null

  private readonly listenersLinkAdicionado = new Set<Listener<LinkValidacao>>()
  private readonly listenersLinkAtualizado = new Set<Listener<LinkValidacao>>()
  private readonly listenersLinkExcluido = new Set<Listener<{ linkId: number; porLogin: string | null }>>()
  private readonly listenersLinkRestaurado = new Set<Listener<LinkValidacao>>()
  private readonly listenersLinkApagado = new Set<Listener<{ linkId: number }>>()
  private readonly listenersItemRevisado = new Set<Listener<{ itemId: number; revisores: Revisor[] }>>()

  constructor() {
    this.conexao = new HubConnectionBuilder()
      .withUrl('/hubs/lista-validacao')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    this.conexao.on('LinkAdicionado', (p: LinkValidacao) => this.listenersLinkAdicionado.forEach((l) => l(p)))
    this.conexao.on('LinkAtualizado', (p: LinkValidacao) => this.listenersLinkAtualizado.forEach((l) => l(p)))
    this.conexao.on('LinkExcluido', (p: { linkId: number; porLogin: string | null }) =>
      this.listenersLinkExcluido.forEach((l) => l(p)))
    this.conexao.on('LinkRestaurado', (p: LinkValidacao) => this.listenersLinkRestaurado.forEach((l) => l(p)))
    this.conexao.on('LinkApagado', (p: { linkId: number }) => this.listenersLinkApagado.forEach((l) => l(p)))
    this.conexao.on('ItemRevisado', (p: { itemId: number; revisores: Revisor[] }) =>
      this.listenersItemRevisado.forEach((l) => l(p)))
  }

  async iniciar(): Promise<void> {
    if (this.conexao.state === HubConnectionState.Connected) return
    if (!this.promessaStart) {
      this.promessaStart = this.conexao.start().catch((e) => {
        this.promessaStart = null
        throw e
      })
    }
    return this.promessaStart
  }

  async parar(): Promise<void> {
    if (this.conexao.state !== HubConnectionState.Disconnected) {
      try { await this.conexao.stop() } catch { /* ignora */ }
    }
    this.promessaStart = null
  }

  onLinkAdicionado(l: Listener<LinkValidacao>): () => void {
    this.listenersLinkAdicionado.add(l)
    return () => this.listenersLinkAdicionado.delete(l)
  }
  onLinkAtualizado(l: Listener<LinkValidacao>): () => void {
    this.listenersLinkAtualizado.add(l)
    return () => this.listenersLinkAtualizado.delete(l)
  }
  onLinkExcluido(l: Listener<{ linkId: number; porLogin: string | null }>): () => void {
    this.listenersLinkExcluido.add(l)
    return () => this.listenersLinkExcluido.delete(l)
  }
  onLinkRestaurado(l: Listener<LinkValidacao>): () => void {
    this.listenersLinkRestaurado.add(l)
    return () => this.listenersLinkRestaurado.delete(l)
  }
  onLinkApagado(l: Listener<{ linkId: number }>): () => void {
    this.listenersLinkApagado.add(l)
    return () => this.listenersLinkApagado.delete(l)
  }
  onItemRevisado(l: Listener<{ itemId: number; revisores: Revisor[] }>): () => void {
    this.listenersItemRevisado.add(l)
    return () => this.listenersItemRevisado.delete(l)
  }
}
