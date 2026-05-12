import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr'
import { normalizarStatus, type StatusToken } from './tipos'

export class TokenHubClient {
  private conexao: HubConnection
  private listeners = new Set<(s: StatusToken) => void>()
  private promessaStart: Promise<void> | null = null
  private parando = false

  constructor() {
    this.conexao = new HubConnectionBuilder()
      .withUrl('/hubs/token')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    this.conexao.on('EstadoMudou', (bruto: unknown) => {
      try {
        const status = normalizarStatus(bruto as never)
        this.listeners.forEach((l) => l(status))
      } catch (e) {
        console.error('Falha ao processar EstadoMudou', e)
      }
    })
  }

  async iniciar(): Promise<void> {
    if (this.parando) return
    if (this.conexao.state === HubConnectionState.Connected) return
    if (!this.promessaStart) {
      this.promessaStart = this.conexao.start().catch((e) => {
        this.promessaStart = null
        if (this.parando) return
        throw e
      })
    }
    return this.promessaStart
  }

  async parar(): Promise<void> {
    this.parando = true
    if (this.promessaStart) {
      try {
        await this.promessaStart
      } catch {
      }
    }
    if (this.conexao.state !== HubConnectionState.Disconnected) {
      try {
        await this.conexao.stop()
      } catch {
      }
    }
  }

  onEstadoMudou(listener: (s: StatusToken) => void): () => void {
    this.listeners.add(listener)
    return () => this.listeners.delete(listener)
  }
}
