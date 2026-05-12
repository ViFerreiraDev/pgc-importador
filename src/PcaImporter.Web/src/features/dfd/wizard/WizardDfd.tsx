import { useMemo, useState } from 'react'
import { ChevronLeft, ChevronRight, Send } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Stepper } from './Stepper'
import { EtapaInformacoesGerais } from './EtapaInformacoesGerais'
import { EtapaJustificativa } from './EtapaJustificativa'
import { EtapaMateriais } from './EtapaMateriais'
import { EtapaResponsaveis } from './EtapaResponsaveis'
import {
  ETAPAS,
  INFO_GERAIS_VAZIO,
  type IdEtapa,
  type InformacoesGerais,
} from './tipos'
import type { MaterialServicoCriado, ResponsavelCriado } from '../tipos'

interface Props {
  uasg: number | null
  itensMaterial: MaterialServicoCriado[]
  responsaveis: ResponsavelCriado[]
  onItemAdicionado: () => void
  onResponsavelAdicionado: () => void
}

export function WizardDfd({
  uasg,
  itensMaterial,
  responsaveis,
  onItemAdicionado,
  onResponsavelAdicionado,
}: Props) {
  const [ativa, setAtiva] = useState<IdEtapa>('informacoes')
  const [info, setInfo] = useState<InformacoesGerais>(INFO_GERAIS_VAZIO)
  const [infoSalva, setInfoSalva] = useState(false)
  const [justificativa, setJustificativa] = useState('')
  const [justificativaSalva, setJustificativaSalva] = useState(false)

  const concluidas = useMemo(() => {
    const set = new Set<IdEtapa>()
    if (infoSalva && validouInformacoes(info)) set.add('informacoes')
    if (justificativaSalva && justificativa.trim().length > 0) set.add('justificativa')
    if (itensMaterial.length > 0) set.add('materiais')
    if (responsaveis.length > 0) set.add('responsaveis')
    return set
  }, [info, infoSalva, justificativa, justificativaSalva, itensMaterial.length, responsaveis.length])

  const indice = ETAPAS.findIndex((e) => e.id === ativa)
  const ehUltima = indice === ETAPAS.length - 1
  const ehPrimeira = indice === 0
  const podeEnviar = concluidas.size === ETAPAS.length

  function avancar() {
    if (!ehUltima) setAtiva(ETAPAS[indice + 1].id)
  }
  function voltar() {
    if (!ehPrimeira) setAtiva(ETAPAS[indice - 1].id)
  }

  return (
    <div className="space-y-6">
      <Stepper ativa={ativa} concluidas={concluidas} onIr={setAtiva} />

      <div>
        {ativa === 'informacoes' && (
          <EtapaInformacoesGerais
            valor={info}
            aoMudar={(v) => { setInfo(v); setInfoSalva(false) }}
            uasg={uasg}
            onSalvo={() => setInfoSalva(true)}
          />
        )}
        {ativa === 'justificativa' && (
          <EtapaJustificativa
            valor={justificativa}
            aoMudar={(v) => { setJustificativa(v); setJustificativaSalva(false) }}
            onSalvo={() => setJustificativaSalva(true)}
          />
        )}
        {ativa === 'materiais' && (
          <EtapaMateriais itens={itensMaterial} onItemAdicionado={onItemAdicionado} />
        )}
        {ativa === 'responsaveis' && (
          <EtapaResponsaveis
            responsaveis={responsaveis}
            onResponsavelAdicionado={onResponsavelAdicionado}
          />
        )}
      </div>

      <div className="flex items-center justify-between gap-3 pt-2">
        <Button variant="outline" onClick={voltar} disabled={ehPrimeira}>
          <ChevronLeft />
          Voltar
        </Button>

        <div className="flex items-center gap-2">
          {!ehUltima ? (
            <Button onClick={avancar}>
              Avançar
              <ChevronRight />
            </Button>
          ) : (
            <Button disabled={!podeEnviar} title={podeEnviar ? 'Envia o DFD ao Compras' : 'Conclua todas as etapas antes de enviar'}>
              <Send />
              Enviar DFD
            </Button>
          )}
        </div>
      </div>
    </div>
  )
}

function validouInformacoes(i: InformacoesGerais): boolean {
  if (!i.dataConclusaoContratacao) return false
  if (!i.descricao.trim()) return false
  if (i.nivelPrioridade === 'ALTO' && !i.justificativaPrioridade.trim()) return false
  return true
}
