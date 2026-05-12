import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Badge } from '@/components/ui/badge'
import type { MaterialServicoCriado } from './tipos'
import { formatarBrl } from '@/lib/utils'

export function ItensDfd({ itens }: { itens: MaterialServicoCriado[] }) {
  const totalGeral = itens.reduce((acc, i) => acc + Number(i.valorTotal), 0)

  return (
    <Card>
      <CardHeader>
        <div className="flex items-baseline justify-between">
          <div>
            <CardTitle>Itens adicionados</CardTitle>
            <CardDescription>{itens.length} {itens.length === 1 ? 'item' : 'itens'} no DFD ativo</CardDescription>
          </div>
          <div className="text-right">
            <div className="text-xs uppercase tracking-wide text-muted-foreground">Total acumulado</div>
            <div className="text-lg font-semibold tabular-nums">{formatarBrl(totalGeral)}</div>
          </div>
        </div>
      </CardHeader>
      <CardContent className="pt-0">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>idItem</TableHead>
              <TableHead>Tipo</TableHead>
              <TableHead>Código</TableHead>
              <TableHead>Padrão descritivo</TableHead>
              <TableHead className="text-right">Qtd</TableHead>
              <TableHead className="text-right">Valor unit.</TableHead>
              <TableHead className="text-right">Total</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {itens.map((i) => (
              <TableRow key={i.id}>
                <TableCell className="font-mono text-xs">{i.id}</TableCell>
                <TableCell><Badge variant="muted">{i.tipo}</Badge></TableCell>
                <TableCell className="font-mono">{i.codigo}</TableCell>
                <TableCell className="max-w-xs">
                  <div className="truncate">{i.nomePadraoDescritivo}</div>
                  <div className="text-xs text-muted-foreground truncate">{i.nomeClasse}</div>
                </TableCell>
                <TableCell className="text-right tabular-nums">{i.quantidade}</TableCell>
                <TableCell className="text-right tabular-nums">{formatarBrl(Number(i.valorUnitario))}</TableCell>
                <TableCell className="text-right tabular-nums font-medium">{formatarBrl(Number(i.valorTotal))}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  )
}
