import { FormMaterialServico } from '../FormMaterialServico'
import { ItensDfd } from '../ItensDfd'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { EmptyState } from '@/components/comum/EmptyState'
import { Package } from 'lucide-react'
import type { MaterialServicoCriado } from '../tipos'

interface Props {
  itens: MaterialServicoCriado[]
  onItemAdicionado: () => void
}

export function EtapaMateriais({ itens, onItemAdicionado }: Props) {
  return (
    <div className="space-y-6">
      <FormMaterialServico onItemAdicionado={onItemAdicionado} />

      {itens.length > 0 ? (
        <ItensDfd itens={itens} />
      ) : (
        <Card>
          <CardHeader>
            <CardTitle>Itens adicionados</CardTitle>
            <CardDescription>Os itens enviados ao Compras aparecem aqui.</CardDescription>
          </CardHeader>
          <CardContent className="p-0">
            <EmptyState
              icon={Package}
              titulo="Nenhum item ainda"
              descricao="Use a busca acima pra adicionar materiais ou serviços ao DFD."
            />
          </CardContent>
        </Card>
      )}
    </div>
  )
}
