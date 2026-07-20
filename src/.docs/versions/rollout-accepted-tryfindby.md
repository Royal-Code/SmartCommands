# Rollout — `Accepted` (202) e `MapFindBy` (busca por chave alternativa/composta)

Estado: **preparado em 2026-07-19; nenhuma publicação, versionamento ou atualização de dependente remoto foi
feita.** Cada release abaixo é uma ação explícita do mantenedor. Este documento registra a ordem, as versões
candidatas e a evidência de integração da Fase 6 do plano `smartcommands-accepted-tryfindby`.

> Localização: `.docs/versions/` (a pasta `.docs/releases/` é ignorada pelo `.gitignore`, padrão `Releases/`).

## Ordem de publicação (dependências primeiro)

1. **RoyalCode.SmartProblems** — `AcceptedMatch`/`AcceptedMatch<T>` (ApiResults), extração de critérios e
   `FindResult.ProjectedFrom` (core/EF). **Já publicado** como `1.0.0-preview-8.0` pelo mantenedor e consumido.
2. **EnterprisePatterns (WorkContext/persistência)** — `IFinder<TEntity>` com projeção por predicado (breaking).
   **Já publicado** como `0.10.1` e consumido (`WorkContextVer` no `Directory.Build.props`).
3. **SmartCommands** — runtime + generator + adapters (`HttpResultStatus.Accepted`, `MapAcceptedRoute`,
   `MapFindBy`, `IRepositoryAccessor<TEntity>.FindEntityAsync` por predicado). **Candidato `0.1.0`** (`SCmdVer`);
   aguardando autorização para versionar/publicar. Notas em [0.1.0.md](0.1.0.md).

Os pacotes 1 e 2 já estão publicados e são consumidos pelas versões pinadas; o passo restante é a release do
SmartCommands, que só ocorre com autorização explícita.

## Versões candidatas (pinadas em `Directory.Build.props`)

| Componente | Versão |
|---|---|
| RoyalCode.SmartProblems / .SmartValidations | `1.0.0-preview-8.0` |
| RoyalCode.WorkContext.* (EnterprisePatterns) | `0.10.1` |
| RoyalCode.SmartCommands.* (este repo) | `0.1.0` (candidato) |
| Generator (`RoyalCode.Extensions.SourceGenerator`) | `0.4.0` |

## Superfície pública nova/quebrada (SmartCommands)

- **Novo:** `HttpResultStatus.Accepted`, `MapAcceptedRouteAttribute`, `MapFindByAttribute<TEntity>`,
  `IRepositoryAccessor<TEntity>.FindEntityAsync<TDto>(Expression<Func<TEntity,bool>>, IReadOnlyList<FindCriterion>, CancellationToken)`.
- **Breaking:** o novo membro de `IRepositoryAccessor<TEntity>` exige implementação nos accessors externos.
  O adapter WorkContext já implementa; subclasses do adapter EF abstrato precisam sobrescrever as projeções e
  podem reutilizar o helper protegido. Ver migração em [0.1.0.md](0.1.0.md).
- **Diagnósticos:** RCCMD054 (`MapAcceptedRoute`), RCCMD055 (rotas de Location conflitantes), RCCMD056 (`MapFindBy`).

## Dependência HTTP gerada

`RoyalCode.SmartCommands` declara **`RoyalCode.SmartProblems.ApiResults`** como dependência transitiva porque o
código de `[MapApiHandlers]` usa suas unions HTTP e `ProduceProblems`. O consumidor não precisa adicioná-lo
separadamente. `RoyalCode.SmartProblems.Http` permanece explícito e opcional para quem usa seus filtros e demais
integrações HTTP.

## Evidência de integração (Fase 6)

- **Build/test Release da solução SmartCommands:** 0 erros; único warning `NU5104` (dependências RoyalCode em
  preview); testes **506/506** (generator 379, Demo 95, EntityFramework 32).
- **Consumer-smoke automatizado por `.nupkg`:** `eng/verify-distribution.ps1` empacota os quatro pacotes `0.1.0`
  e executa um consumer isolado em **net8.0/net9.0/net10.0**, exercitando em coexistência
  `MapCreatedRoute` (201), `MapFind` (por ID), `MapAcceptedRoute` (202 + `MapResponseValues`),
  `WithResultStatus(Accepted)` (202 sem corpo), `MapFindBy` simples e composto, e o registro
  WorkContext + referências aos adapters EF/WorkContext. O script inspeciona as fontes geradas e não declara
  `ApiResults` diretamente, comprovando a dependência transitiva. Sem `CS8032`/`CS8785`/`AD0001`.
- **Execução (runtime):** coberta pela Demo em `net10.0` (95 testes HTTP/OpenAPI), que exercita os mesmos
  padrões gerados (202 com/sem Location e header, `MapFindBy` encontrado/não encontrado com `ProblemDetails`
  estruturado). O smoke valida a compilação como pacote nos três TFMs.

## Gate

Nenhuma release é feita automaticamente. Versionar (`SCmdVer`), empacotar para publicação, publicar em nuget.org
ou atualizar dependentes remotos exigem autorização explícita do mantenedor.
