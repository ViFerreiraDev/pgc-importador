export type Papel = 'Admin' | 'Normal'

export interface Usuario {
  id: number
  login: string
  nome: string
  papel: Papel
  criadoEm: string
  criadoPorLogin: string | null
}
