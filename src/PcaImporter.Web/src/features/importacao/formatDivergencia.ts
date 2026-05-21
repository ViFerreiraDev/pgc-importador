import type { DivergenciaValidacao } from './tipos'

/**
 * Formata a faixa histórica de uma divergência:
 *   preço → "R$ 1,50 – R$ 9,80"
 *   qtd   → "120 – 1.500"
 */
export function formatarBanda(d: DivergenciaValidacao): string {
  if (d.tipo === 'preco') {
    return `${brl(d.referenciaMin)} – ${brl(d.referenciaMax)}`
  }
  return `${num(d.referenciaMin)} – ${num(d.referenciaMax)}`
}

const brl = (v: number) => v.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
const num = (v: number) => v.toLocaleString('pt-BR', { maximumFractionDigits: 2 })
