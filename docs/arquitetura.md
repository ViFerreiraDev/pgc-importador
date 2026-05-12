# Arquitetura do PcaImporter

## Visão geral

Ferramenta standalone que cadastra em massa itens do PCA no Compras.gov.br via API autenticada por JWT. Roda local, sem dependência de Chrome aberto durante execução.

## Princípios

1. **Idempotência**: cada item gera hash determinístico (SHA-256) sobre campos identificadores. Antes de cadastrar, consulta SQLite local. Se já tem sucesso, pula.
2. **Persistência transacional**: cada mudança de status do item é gravada atomicamente. Aplicação caindo no meio do processamento é retomada de onde parou.
3. **Auditabilidade**: todo Request e Response com Compras vai pra tabela de logs estruturados, com headers sensíveis sanitizados.
4. **Token só em RAM**: nunca persiste o JWT. Refresh automático via endpoint dedicado.

## Camadas Clean Architecture

- **Domain**: entidades (Execucao, ItemFila, LogEntrada, TokenSessao), value objects, enums (StatusItem, ModoExecucao). Sem dependência externa.
- **Application**: casos de uso (ImportarPlanilha, ProcessarFila, ValidarCatmat), interfaces (IComprasGovClient, IItemFilaRepositorio), DTOs.
- **Infrastructure**: implementações concretas. EF Core (PcaDbContext, repositórios), HttpClient (cliente Compras), ClosedXML (parser de planilha), serviços de hash.
- **Api**: ASP.NET Core, controllers REST, hubs SignalR, configuração de DI, hosting da SPA via static files.

## Fluxo principal

```
[Usuário] -> [Web/SPA] -> [Api REST] -> [Application UseCase] -> [Infrastructure]
                              ^                                      |
                              |                                      v
                          [SignalR Hub] <-- [Worker BackgroundService]
                                                    |
                                                    v
                                            [Compras.gov.br API]
```

## Decisões arquiteturais

### Hospedagem SPA
Front buildado é copiado pra `wwwroot/` no publish. Em dev, Vite serve front em 5173 com proxy reverso pras rotas `/api`, `/hubs` e `/swagger`. Em prod, único processo serve tudo.

### Refresh de token
Endpoint descoberto: `POST https://cnetmobile.estaleiro.serpro.gov.br/comprasnet-usuario/v2/sessao/governo/retoken`.
TTL aproximado de 30 minutos. Antes de cada request, valida `exp` do JWT. Se restam menos de 60s, faz refresh proativo.

### Throttle e retry
- Delay entre requests: default 2s (configurável)
- Max tentativas: default 3
- Backoff em 429/5xx: 5s, 15s, 60s
- 4xx exceto 429: erro definitivo

### Idempotência
SHA-256 sobre campos identificadores do item PCA (a definir conforme estrutura final do payload). Tabela `ItensFila` indexada por hash.

## Roadmap

- **Fase 0**: bootstrap (esta fase) — solution, projetos, scaffold do front, migration inicial
- **Fase 1**: import e validação de XLSX
- **Fase 2**: gestão de token (UI de paste, decode, refresh, indicador de saúde)
- **Fase 3**: engine (fila persistente, worker com throttle/retry, hash de idempotência, SignalR, modos de execução)
- **Fase 4**: piloto real (DryRun, 10, 100, 1000, completo)

## Possível evolução futura

A engine e o validador devem ficar suficientemente desacoplados pra serem extraídos como módulo do SIGA, sem reescrita.
