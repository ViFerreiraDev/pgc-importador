import { PageHeader } from '@/components/comum/PageHeader'
import { PainelDfd } from '@/features/dfd/PainelDfd'
import { useToken } from '@/features/token/TokenContext'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { ShieldOff } from 'lucide-react'

export function Dfds() {
  const { status } = useToken()
  const semSessao = !status || status.estado === 'Ausente' || status.estado === 'Expirado' || status.estado === 'RefreshFalhou'

  return (
    <div className="space-y-8">
      <div className="anim-rise">
        <PageHeader
          kicker="DFDs"
          titulo="Documento de Formalização da Demanda"
          descricao="Crie um DFD e adicione itens do PGC. As chamadas vão para o Compras.gov.br em tempo real."
        />
      </div>

      {semSessao ? (
        <Alert variant="warning">
          <ShieldOff />
          <div>
            <AlertTitle>Sessão necessária</AlertTitle>
            <AlertDescription>
              Conecte um refresh token na barra superior para começar a criar DFDs.
            </AlertDescription>
          </div>
        </Alert>
      ) : (
        <PainelDfd />
      )}
    </div>
  )
}
