# Descobertas Compras.gov.br

Mapa de endpoints, payloads, headers e comportamentos do Compras.gov.br relevantes pra automação. Manter este documento atualizado conforme novos endpoints forem mapeados.

> Convenção: tokens reais nunca aqui. Use placeholders tipo `XXXX_TOKEN_REDACTED`, `999999` (UASG fake), `00000000000` (CPF fake).

## Autenticação

### Token raiz
Obtido manualmente pelo usuário via login em https://www.gov.br/compras + DevTools. Bearer JWT colado na UI do PcaImporter.

### Refresh de sessão

```
GET https://cnetmobile.estaleiro.serpro.gov.br/comprasnet-usuario/v2/sessao/governo/retoken
Authorization: Bearer XXXX_REFRESH_TOKEN_REDACTED
Accept: application/json, text/plain, */*
```

**IMPORTANTE**:
- Método é **GET**, não POST. POST retorna `405 Method Not Allowed` com header `allow: GET`.
- O `Authorization` deve carregar o **refresh token**, não o access token. Refresh com access retorna `401`.
- Sem body. Sem Content-Type.

**Distinção entre access token e refresh token**:
- **Access token**: TTL ~10 min, claims completos (sub, tipo, numero_uasg, id_sessao, mnemonicos, autenticacao, iat, exp). Vai nas chamadas de negócio.
- **Refresh token**: TTL ~30 min, claims mínimos (sub, tipo, id_sessao, iat, exp). Vai apenas no retoken.

**Response (200)**:
```json
{
  "accessToken": "eyJ...",
  "refreshToken": "eyJ..."
}
```

Cada refresh **rotaciona o par**: tanto accessToken quanto refreshToken são reemitidos. Sempre atualizar os dois.

## Endpoints DFD/PCA

### Criar DFD (rascunho)

```
POST https://cnetmobile.estaleiro.serpro.gov.br/comprasnet-artefatos/api/v1/artefato/DFD
Authorization: Bearer XXXX_TOKEN_REDACTED
Content-Type: application/json

{}
```

**Status confirmado**: `201 Created`. Latência observada: ~17s (lento).

**Body**: vazio `{}` é suficiente. Servidor preenche todos os defaults com base nos claims do token.

**Headers**: apenas `Authorization` e `Content-Type` são realmente necessários. Cookies `_ga*`, `User-Agent`, `Origin`, `Referer`, `sec-ch-*` são todos opcionais. Servidor aceita request limpa de backend.

**Response (campos relevantes)**:
```json
{
  "id": 5317480,                           // idArtefato
  "tipo": "DFD",
  "ano": 2026,
  "uasg": 986001,                          // vem do token (numero_uasg)
  "nomeUasg": "PREFEITURA MUNICIPAL DO RIO DE JANEIRO - RJ",
  "numero": 14,
  "status": "RASCUNHO",
  "loginOperacao": 10387189700,            // vem do token (sub)
  "nomeLoginOperacao": "DAMIANA CRISTINA DA CRUZ SOUZA",
  "secoes": [
    { "id": 18248377, "nome": "Documento de Formalização da Demanda", "itens": [...] },
    { "id": 18248378, "nome": "Acompanhamento", "itens": [...] },
    { "id": 18248379, "nome": "Relacionamentos", "itens": [...] }
  ],
  "dfd": {
    "id": 2118873,                         // idFormalizacaoDemanda (campo crítico)
    "idArtefato": 5317480,
    "nivelPrioridade": "BAIXO",
    "pca": {
      "ano": 2027,                         // PCA alvo, determinado pelo servidor
      "status": "EM_ELABORACAO"
    }
  },
  "faseAtual": { "id": 1, "nome": "Planejamento" }
}
```

**Itens da seção "Documento de Formalização da Demanda" (criados em branco)**:
- `tipo: 12` Informações Gerais (obrigatório)
- `tipo: 1` Justificativa de Necessidade (obrigatório)
- `tipo: 13` Materiais/Serviços (obrigatório, é onde entram os itens do PCA)
- `tipo: 6` Responsáveis (obrigatório)

### Adicionar material/serviço ao DFD

```
POST https://cnetmobile.estaleiro.serpro.gov.br/comprasnet-artefatos/api/v1/artefato/dfd/materialservico
Authorization: Bearer XXXX_TOKEN_REDACTED
Content-Type: application/json; charset=utf-8

{
  "idFormalizacaoDemanda": 2118873,
  "tipo": "MATERIAL",
  "codigo": "630788",
  "idClasse": 6515,
  "nomeClasse": "Instrumentos, Equipamentos E Suprimentos Médicos  E  Cirúrgicos",
  "idPadraoDescritivo": 19829,
  "nomePadraoDescritivo": "Pinça Anatômica",
  "descricao": "componente: s/ cremalheira, comprimento total: cerca de 14, ...",
  "quantidade": 333,
  "valorUnitario": 33,
  "moeda": "Real",
  "siglaUnidadeFornecimento": "UN",
  "nomeUnidadeFornecimento": "Unidade"
}
```

**Status confirmado**: `201 Created`. Latência observada: ~200ms (rápido, ao contrário da criação do DFD).

**Importante**: Content-Type DEVE ser `application/json; charset=utf-8` ou o body com acentos retorna `400 Invalid UTF-8 middle byte`.

**Campo de ligação**: `idFormalizacaoDemanda` (= `dfd.id` retornado na criação do DFD).

**Response (campos relevantes)**:
```json
{
  "id": 8278625,                    // idItem do material adicionado
  "idFormalizacaoDemanda": 2118873,
  "tipo": "MATERIAL",
  "codigo": "630788",
  "quantidade": 333,
  "valorUnitario": 33,
  "valorTotal": 10989,              // calculado pelo servidor (qtd * valorUnit)
  "moeda": "Real",
  "loginOperacao": 10387189700,
  "dataHoraOperacao": "2026-05-07T01:15:52.337-03:00",
  ...
}
```

### Demais endpoints DFD/PCA
> A preencher conforme cURLs forem coletados:
> - Atualizar/preencher seções do DFD (Justificativa, Responsáveis)
> - Listar DFDs existentes
> - Excluir DFD em rascunho
> - Excluir item de material do DFD
> - Submeter DFD (sair de RASCUNHO)
> - Validar CATMAT/CATSER

### Validação CATMAT/CATSER (catálogo público)

API de dados abertos, sem autenticação.

**Material**:
```
GET https://dadosabertos.compras.gov.br/modulo-material/4_consultarItemMaterial?pagina=1&tamanhoPagina=10&codigoItem={CODIGO}&bps=false
Accept: */*
```

**Serviço** (provável, a confirmar):
```
GET https://dadosabertos.compras.gov.br/modulo-servico/3_consultarItemServico?pagina=1&tamanhoPagina=10&codigoItem={CODIGO}&bps=false
```

**Response**:
```json
{
  "resultado": [
    {
      "codigoItem": 630788,
      "codigoGrupo": 65,
      "nomeGrupo": "EQUIPAMENTOS E ARTIGOS PARA USO MÉDICO, DENTÁRIO E VETERINÁRIO",
      "codigoClasse": 6515,
      "nomeClasse": "INSTRUMENTOS, EQUIPAMENTOS E SUPRIMENTOS MÉDICOS  E  CIRÚRGICOS",
      "codigoPdm": 19829,
      "nomePdm": "PINÇA ANATÔMICA",
      "descricaoItem": "PINÇA ANATÔMICA, MODELO 2: ...",
      "statusItem": true,
      "itemSustentavel": false,
      "dataHoraAtualizacao": "2025-08-15T12:15:55.088344"
    }
  ],
  "totalRegistros": 1,
  "totalPaginas": 1,
  "paginasRestantes": 0
}
```

**Mapeamento pra adicionar item ao DFD**:
| Catálogo | Body do `dfd/materialservico` |
|---|---|
| `codigoItem` | `codigo` (string) |
| `codigoClasse` | `idClasse` |
| `nomeClasse` | `nomeClasse` |
| `codigoPdm` | `idPadraoDescritivo` |
| `nomePdm` | `nomePadraoDescritivo` |
| `descricaoItem` | `descricao` |
| (fixo) `MATERIAL`/`SERVICO` | `tipo` |

Campos que precisam vir do usuário: `quantidade`, `valorUnitario`, `siglaUnidadeFornecimento`, `nomeUnidadeFornecimento`. `moeda` default `Real`.

### Busca de UASG
> A preencher

## Comportamentos observados

A documentar à medida que forem identificados:
- Códigos HTTP retornados em cada cenário
- Estrutura de erros
- Limites de rate (se houver)
- Necessidade de headers específicos além de Authorization
