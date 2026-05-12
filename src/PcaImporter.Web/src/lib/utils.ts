import { type ClassValue, clsx } from 'clsx'
import { twMerge } from 'tailwind-merge'

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

export function formatarBrl(valor: number): string {
  return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(valor)
}

export function formatarBrlCompacto(valor: number): string {
  const abs = Math.abs(valor)
  const sinal = valor < 0 ? '-' : ''
  const fmt = (n: number, dig: number) =>
    n.toLocaleString('pt-BR', { minimumFractionDigits: 0, maximumFractionDigits: dig })
  if (abs >= 1_000_000_000_000) return `${sinal}R$ ${fmt(abs / 1_000_000_000_000, 2)} tri`
  if (abs >= 1_000_000_000) return `${sinal}R$ ${fmt(abs / 1_000_000_000, 2)} bi`
  if (abs >= 1_000_000) return `${sinal}R$ ${fmt(abs / 1_000_000, 2)} mi`
  if (abs >= 1_000) return `${sinal}R$ ${fmt(abs / 1_000, 1)} mil`
  return formatarBrl(valor)
}

export function formatarNumeroCompacto(valor: number): string {
  const abs = Math.abs(valor)
  const sinal = valor < 0 ? '-' : ''
  const fmt = (n: number, dig: number) =>
    n.toLocaleString('pt-BR', { minimumFractionDigits: 0, maximumFractionDigits: dig })
  if (abs >= 1_000_000_000) return `${sinal}${fmt(abs / 1_000_000_000, 2)} bi`
  if (abs >= 1_000_000) return `${sinal}${fmt(abs / 1_000_000, 2)} mi`
  if (abs >= 1_000) return `${sinal}${fmt(abs / 1_000, 1)} mil`
  return valor.toLocaleString('pt-BR')
}

export function formatarDataHora(iso: string | null | undefined): string {
  if (!iso) return '-'
  return new Date(iso).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'medium' })
}

export function formatarDuracao(segundos: number): string {
  if (segundos <= 0) return 'expirado'
  const m = Math.floor(segundos / 60)
  const s = segundos % 60
  return `${m}m ${s.toString().padStart(2, '0')}s`
}

export function mascararCpf(cpf: string | null | undefined): string {
  if (!cpf) return '-'
  if (cpf.length !== 11) return cpf
  return `${cpf.slice(0, 3)}.***.***-${cpf.slice(-2)}`
}
