const baseUrl = ''

async function requisicao<T>(metodo: string, caminho: string, corpo?: unknown): Promise<T> {
  const resp = await fetch(`${baseUrl}${caminho}`, {
    method: metodo,
    headers: corpo !== undefined ? { 'Content-Type': 'application/json' } : undefined,
    body: corpo !== undefined ? JSON.stringify(corpo) : undefined,
  })

  const texto = await resp.text()
  const dados = texto ? JSON.parse(texto) : (null as unknown as T)

  if (!resp.ok) {
    const erro = (dados && typeof dados === 'object' && 'erro' in dados) ? (dados as { erro: string }).erro : `HTTP ${resp.status}`
    throw new Error(erro)
  }

  return dados as T
}

export const apiClient = {
  get: <T>(caminho: string) => requisicao<T>('GET', caminho),
  post: <T>(caminho: string, corpo?: unknown) => requisicao<T>('POST', caminho, corpo),
  put: <T>(caminho: string, corpo?: unknown) => requisicao<T>('PUT', caminho, corpo),
  delete: <T>(caminho: string) => requisicao<T>('DELETE', caminho),
}
