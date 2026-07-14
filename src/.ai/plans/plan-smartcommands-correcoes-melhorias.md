# Plan: Correções, endurecimento e evolução do SmartCommands (`smartcommands-correcoes-melhorias`)

## Status: RASCUNHO - decisões de contrato público Q1-Q6 ainda abertas

## Progresso

`░░░░░░░░░░░░` **0%** - 0 de 12 fases concluídas

| Fase | Estado |
|---|---|
| Fase 1 - Baseline e decisões de contrato | Pendente |
| Fase 2 - Modelos incrementais e isolamento de entradas inválidas | Pendente |
| Fase 3 - Analyzer semântico e catálogo de diagnósticos | Pendente |
| Fase 4 - Leitura semântica e emissão determinística | Pendente |
| Fase 5 - Binding de `WithParameter` e resolução de `EditEntity` | Pendente |
| Fase 6 - Validações adicionais do comando | Pendente |
| Fase 7 - Confiabilidade do adapter Entity Framework | Pendente |
| Fase 8 - Runtime de decorators, WorkContext e retry | Pendente |
| Fase 9 - Completude dos mapeamentos Minimal API existentes | Pendente |
| Fase 10 - Novas capacidades de mapeamento Minimal API | Pendente |
| Fase 11 - Qualidade transversal, pacote, documentação e CI | Pendente |
| Fase 12 - Compatibilidade, regressão e preparação de release | Pendente |

> **Manutenção deste plano:** ao concluir as tarefas de uma fase, marque cada tarefa com `- [x]`,
> troque o **Estado** da fase para `Concluida` na tabela acima e atualize a barra de progresso
> (um bloco `█` por fase concluída, `%` e `X de 12`). Antes de fechar uma fase, confirme que
> decisões, critérios de aceite, testes e invariantes relacionados foram aplicados.

---

## Contexto

### Fontes verificadas

- `.docs/templates/template-ai-implementation-plan.md` — define a estrutura, a manutenção e a rastreabilidade obrigatórias deste plano.
- `.docs/reviews/2026-07-13-atualizacao-libs-royalcode-breaking-continueasync.md` — registra a atualização das dependências RoyalCode e a validação anterior de 153 testes; a alegação de `async void` será corrigida conforme DF6.
- `Directory.Build.props` — bibliotecas têm alvos `net8.0;net9.0;net10.0`, testes e Demo usam `net10.0`, e o generator usa `netstandard2.0`.
- `RoyalCode.SmartCommands.Generators/Generators/IncrementalGenerator.cs` — combina coleções globais de comandos, finds e searches e deixa modelos inválidos entrarem nas agregações.
- `RoyalCode.SmartCommands.Generators/Generators/CommandHandlerGenerator.cs` — mistura leitura sintática, análise, diagnósticos e emissão; determina async pela sintaxe de `Task<T>` e lê vários argumentos por índice.
- `RoyalCode.SmartCommands.Generators/Generators/MapApiHandlersInformation.cs`, `MapInformation.cs`, `FindInformation.cs`, `MapCreatedInformation.cs` e `MapResponseValuesInformation.cs` — contêm inconsistências verificadas de igualdade/hash e coleções com identidade por referência.
- `RoyalCode.SmartCommands.Generators/Generators/SearchGenerator.cs` e `SearchInformation.cs` — detectam async pelo modificador `async` e forçam `[FromRoute]` em parâmetros `WithParameter`.
- `RoyalCode.SmartCommands/WithParameterAttribute.cs` — documenta o parâmetro como externo ao payload e sujeito ao binding normal de Minimal API.
- `RoyalCode.SmartCommands/EditEntityAttribute.cs` — não possui campo para desambiguar o parâmetro da rota.
- `RoyalCode.SmartCommands.EntityFramework/Adapters/DbContextAccessor.cs` — converte qualquer `Exception`, inclusive cancelamento, em `Result` e também converte falha de rollback em `AggregateException` retornada.
- `RoyalCode.SmartCommands.EntityFramework/Adapters/RepositoryAdapter.cs` — guarda o contexto como campo privado `DbContext`, embora a projeção seja implementada por subclasses.
- `RoyalCode.SmartCommands/Mediator.cs` — mantém um enumerador mutável, não o descarta e altera o restante do pipeline quando `next` é chamado mais de uma vez.
- `RoyalCode.SmartCommands.WorkContext/DefaultConcurrencyRetryProblemFactory.cs` e geração de retry — opções de detalhe/tipo do problema esgotado só passam pela factory quando existe uma operação explícita.
- `RoyalCode.SmartCommands.Tests/Util.cs` — cria um driver novo por compilação, não verifica incrementalidade entre execuções e normaliza snapshots para CRLF.
- `AnalyzerReleases.Shipped.md` e `AnalyzerReleases.Unshipped.md` — registram RCCMD000-RCCMD025; os links dos diagnósticos publicados ainda apontam para `google.com`.
- `pack.targets`, `README.md`, `.docs/instructions.md` e `.docs/commands.md` — possuem metadados/documentação desatualizados e typos verificados.
- [Binding de parâmetros em Minimal APIs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/parameter-binding?view=aspnetcore-10.0) — rota, query, header, body, form, DI e binding customizado podem ser explícitos ou inferidos; nome presente no template determina rota para tipos parseáveis.
- [Respostas de Minimal APIs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0) e [metadados OpenAPI](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/include-metadata?view=aspnetcore-10.0) — `TypedResults` e unions tipadas expõem metadados de resposta; `ProducesProblem`/`ProducesValidationProblem` documentam problemas.
- [Filtros de Minimal APIs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/min-api-filters?view=aspnetcore-10.0) e [tratamento de erros em APIs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0) — filtros executam antes/depois do handler; exceções não tratadas podem ser centralizadas por middleware/`IExceptionHandler` e `ProblemDetails`.
- [Roslyn Incremental Generators Cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.cookbook.md) — modelos do pipeline devem ter igualdade por valor, não carregar símbolos/syntax/locations e encapsular coleções; diagnósticos devem preferencialmente pertencer a analyzer separado.
- `git remote -v` — o repositório correto é `https://github.com/Royal-Code/SmartCommands.git`.
- `git status --short` em 2026-07-13 — há alterações do usuário no plano/Demo e arquivos gerados; este plano não pode sobrescrevê-las.

### Estado atual do código (verificado em 2026-07-13)

- **Build anterior verde:** a revisão registra `dotnet build SmartCommands.sln` sem erros e 153/153 testes aprovados após a atualização RoyalCode; o worktree atual possui mudanças posteriores e precisa de novo baseline na Fase 1.
- **Warnings aceitos:** o último build informado pelo mantenedor contém apenas nove NU5104; eles não serão tratados neste plano.
- **Sem `async void` no pacote:** os extension methods permanecem assíncronos e retornam `Task`; a alteração já aplicada foi a nova assinatura com `TParam`, `CancellationToken` e lambda `static`.
- **Cabeçalho gerado presente:** `Util.GeneratedCode` já espera `// <auto-generated/>` e `#nullable enable`; CS8669 não faz parte do trabalho pendente.
- **Generator incremental apenas nominalmente:** usa `IIncrementalGenerator` e `ForAttributeWithMetadataName`, mas os modelos mutáveis e os `GetHashCode` por referência impedem cache confiável.
- **Falha concreta de igualdade:** `MapApiHandlersInformation.Equals(object)` testa `AddHandlersServicesInformation` e sua igualdade tipada ignora `WithOpenApi`.
- **Falhas concretas nos demais modelos:** `FindInformation` ignora `IdType`; `MapInformation` chama `SequenceEqual` sobre coleções anuláveis; `MapCreatedInformation` e `MapResponseValuesInformation` comparam coleções por referência.
- **Transformações frágeis:** existem casts diretos para `GenericNameSyntax`, acessos `Arguments[0]`/`Arguments[1]`, verificações por texto e extração manual de rota por `Substring`.
- **Entradas inválidas contaminam agregações:** os modelos com erro ainda são coletados para DI e Minimal API; isso permite exceção do generator em vez de somente RCCMD.
- **`WithParameter` inconsistente:** comandos encaminham o parâmetro ao handler, mas search adiciona `[FromRoute]` mesmo quando a rota não declara o nome.
- **`EditEntity` ambíguo:** o endpoint escolhe o primeiro parâmetro encontrado na rota, independentemente do nome do parâmetro da entidade.
- **Mapeamentos existentes:** há `MapPost`, `MapPut`, `MapPatch`, `MapDelete`, `MapGet`, `MapFind` e `MapSearch`; resposta, binding, rota criada e metadados não usam um modelo comum.
- **Adapter EF acoplado ao formato de erro:** `CompleteAsync` captura qualquer exceção e a converte implicitamente em `Result`, embora handlers também possam ser usados fora de HTTP.
- **Cobertura concentrada em `net10.0`:** os pacotes compilam para três TFMs, mas os testes executam somente em `net10.0` e não há consumer-smoke dos pacotes por TFM.
- **Automação ausente:** não existe `.editorconfig` nem workflow de CI; `.github` contém apenas instruções do Copilot.
- **Metadados incorretos:** `pack.targets` aponta para `Royal-Code/EnterpisePatterns`; o remote local aponta para `Royal-Code/SmartCommands`.

### Lacunas, conflitos e restrições

- **Contratos públicos pendentes:** validações adicionais, política de exceções EF, arquitetura de diagnósticos, novo pacote HTTP e rota criada dependem de Q1-Q6.
- **Analyzer versus generator:** mover diagnósticos altera a arquitetura do pacote, mas manter `Diagnostic`/`Location` nos modelos prejudica incrementalidade.
- **Binding inferido:** remover `[FromRoute]` corrige o contrato geral de `WithParameter`, mas tipos complexos em POST podem virar body e tipos registrados podem vir de DI; atributos explícitos precisam ser preservados no delegate.
- **Retry e efeitos colaterais:** validações adicionais devem executar uma vez fora do laço para não repetir consultas/efeitos externos em conflito de concorrência.
- **Bibliotecas não HTTP:** o adapter EF não pode presumir que todo consumidor possui filtro ou middleware de Minimal API.
- **Arquivos do usuário em edição:** mudanças atuais do Demo e do plano `plan-demo-usage-scenarios.md` são fora do escopo e devem ser preservadas.
- **Breaking changes permitidas:** não serão criados aliases antigos, duplicações ou membros `[Obsolete]`; cada quebra deve constar nas notas de release.
- **NU5104 aceito:** build/pack pode conter somente esses warnings conhecidos; qualquer outro warning reprova a fase.

### Superfícies impactadas a mapear

- `RoyalCode.SmartCommands` — atributos públicos, contratos de handler, mediator e documentação XML.
- `RoyalCode.SmartCommands.Generators` — analyzer, modelos incrementais, diagnósticos, geração de handlers/DI/endpoints e empacotamento analyzer.
- `RoyalCode.SmartCommands.EntityFramework` — semântica de transação, cancelamento, exceção e extensibilidade do repository adapter.
- `RoyalCode.SmartCommands.WorkContext` — retry, factory de problema esgotado, decorators e opções.
- `RoyalCode.SmartCommands.Tests` e `RoyalCode.SmartCommands.Tests.Models` — testes unitários, analyzer/generator driver, snapshots e consumer-smoke.
- `RoyalCode.SmartCommands.Demo` e `RoyalCode.SmartCommands.Demo.Tests` — cenários end-to-end de binding, HTTP, validação, persistência e OpenAPI.
- `AnalyzerReleases.*.md`, `pack.targets`, `README.md` e `.docs/**` — catálogo, distribuição, migração e exemplos públicos.

---

## Objetivo

1. Tornar o generator incremental de fato, determinístico e incapaz de emitir código para entradas semanticamente inválidas.
2. Cobrir colisões, ambiguidades e usos inválidos com diagnósticos RCCMD localizados, documentados e testados.
3. Corrigir `WithParameter`, `EditEntity`, async detection, rotas criadas e demais bugs dos mapeamentos atuais.
4. Introduzir validações adicionais síncronas/assíncronas, com `Result`, parâmetros e ordem de execução definida.
5. Tornar EF, decorators e retry previsíveis em sucesso, falha, cancelamento e uso fora de HTTP.
6. Completar a experiência de Minimal API e selecionar novas capacidades a partir de uma matriz de casos de uso.
7. Fechar lacunas de testes, documentação, metadados de pacote, compatibilidade por TFM e automação.

## Fora de escopo

- Eliminar ou suprimir os nove NU5104 aceitos pelo mantenedor.
- Alterar SmartProblems, SmartValidations, SmartSearch, SmartSelector, WorkContext ou outras bibliotecas RoyalCode externas a esta solução.
- Reestruturar o Demo segundo os planos de arquitetura em edição; o Demo receberá somente cenários necessários para validar SmartCommands.
- Preservar APIs antigas por aliases, duplicação ou `[Obsolete]`.
- Implementar OData, GraphQL, API versioning ou geração baseada em OpenAPI nesta entrega.
- Adicionar retry para falhas transitórias de infraestrutura; `WithRetryOnConcurrency` continua exclusivo de concorrência otimista.

---

## Perguntas ao humano

- **Q1 — Contrato das validações adicionais:** qual API pública deve marcar e ordenar métodos adicionais de validação?
  - **Opções:**
    - **A) `[CommandValidation(Order = n)]` (recomendada):** métodos de instância descobertos automaticamente, retorno `Result`, `Task<Result>` ou `ValueTask<Result>`, parâmetros por DI/`WithParameter`/`CancellationToken`, executados após `HasProblems` e antes de UoW/retry.
    - **B) Lista no método `[Command]`:** um atributo no comando informa explicitamente nomes/tipos de validators; reduz descoberta implícita, mas usa strings ou uma API mais verbosa.
  - **Impacto se não decidir:** Fase 6 bloqueada; as fases anteriores podem prosseguir.
  - **Status:** Aberta.

- **Q2 — Exceções do adapter EF:** qual semântica `DbContextAccessor.CompleteAsync` deve expor?
  - **Opções:**
    - **A) Limpar e relançar (recomendada):** capturar somente para tentar rollback, preservar `OperationCanceledException` e relançar falhas inesperadas; `Result` representa apenas sucesso/problemas explicitamente conhecidos.
    - **B) Mapper explícito:** converter somente exceções reconhecidas por um serviço de mapeamento EF e relançar cancelamento/desconhecidas.
    - **C) Manter conversão ampla:** continuar transformando qualquer exceção em `Result`, corrigindo apenas cancelamento e rollback.
  - **Impacto se não decidir:** Fase 7 bloqueada e não há critério final para testes de falha.
  - **Status:** Aberta.

- **Q3 — Dono dos diagnósticos:** diagnósticos de uso devem sair do generator?
  - **Opções:**
    - **A) `DiagnosticAnalyzer` separado no mesmo pacote (recomendada):** analyzer reporta RCCMD; generator compartilha regras puras, filtra modelos inválidos e não carrega `Diagnostic`/`Location` no pipeline.
    - **B) Generator:** manter reporte em `RegisterSourceOutput`, usando DTOs equatáveis de diagnóstico e reconstruindo `Location` apenas na saída.
  - **Impacto se não decidir:** Fases 2 e 3 podem criar testes/modelos, mas a migração final de diagnósticos fica bloqueada.
  - **Status:** Aberta.

- **Q4 — Primeiro pacote de novas capacidades HTTP:** quanto deve entrar após corrigir os mapeamentos atuais?
  - **Opções:**
    - **A) Extensibilidade mínima (recomendada):** filtro por endpoint, política explícita de resultado/status e metadados completos; deferir alternate/composite find, upload e streaming.
    - **B) Pacote ampliado:** incluir também `Accepted`/202, find por chave alternativa/composta e binding/form/file/stream assistido.
  - **Impacto se não decidir:** Fase 10 fica limitada ao documento de design e backlog, sem nova API pública.
  - **Status:** Aberta.

- **Q5 — Automação de CI/CD:** a solução deve receber workflow GitHub Actions nesta entrega?
  - **Opções:**
    - **A) CI multiplataforma (recomendada):** build/test/pack em Windows e Linux, consumer-smoke `net8.0`/`net9.0`/`net10.0`, sem publicar pacotes.
    - **B) Verificação local:** criar scripts reproduzíveis, sem modificar `.github/workflows`.
  - **Impacto se não decidir:** a parte de CI da Fase 11 fica bloqueada; qualidade local pode prosseguir.
  - **Status:** Aberta.

- **Q6 — Contrato de `MapCreatedRoute`:** placeholders posicionais devem ser removidos nesta quebra?
  - **Opções:**
    - **A) Placeholders nomeados (recomendada):** `"{id}"` casa, sem diferenciar maiúsculas, com `nameof(Model.Id)`; quantidade/nome/propriedade são validados em compilação.
    - **B) Manter posicionais:** preservar `"{0}"` + lista ordenada, adicionando somente diagnósticos de quantidade e propriedade.
  - **Impacto se não decidir:** correções seguras da Fase 9 prosseguem, mas a emissão de `Location` criada permanece bloqueada.
  - **Status:** Aberta.

---

## Decisões fechadas

- **DF1 — Breaking changes diretas:** corrigir contratos e nomes sem aliases, duplicações ou membros `[Obsolete]`. Fonte: solicitação do mantenedor.
- **DF2 — Semântica de `WithParameter`:** representa qualquer valor externo ao payload; no handler vira parâmetro público e no delegate de Minimal API também. Sem atributo explícito, o ASP.NET Core infere rota, query, serviço, body ou binding customizado; a presença do nome no template determina rota. Fonte: solicitação do mantenedor e documentação oficial de binding.
- **DF3 — Binding explícito preservado:** atributos suportados de binding aplicados ao parâmetro-fonte serão copiados somente para o parâmetro do delegate HTTP, não para a interface do handler. Fonte: DF2 e precedência oficial de binding.
- **DF4 — Resolução de `EditEntity`:** `RouteParameterName` no atributo será opcional; uma única variável de rota é usada automaticamente; com várias, tentar `${nomeDoParametroDaEntidade}Id` e depois `nomeDoParametroDaEntidade`; sem correspondência única, emitir diagnóstico e não gerar o endpoint. Fonte: solicitação do mantenedor.
- **DF5 — Identificadores reservados:** `command`, `ct`, `accessor`, `decorators` e `retryOptions` são reservados, assim como qualquer outro nome efetivamente emitido no mesmo escopo pelo generator; colisão diagnosticável é erro RCCMD e o generator não renomeia silenciosamente. Fonte: solicitação do mantenedor.
- **DF6 — Sem alegação de `async void`:** a revisão/documentação será corrigida para registrar que o problema da atualização foi a assinatura `(TParam, CancellationToken)` e que os métodos continuam retornando `Task`; lambdas geradas permanecem `static` com token encaminhado. Fonte: esclarecimento do mantenedor.
- **DF7 — Typos:** corrigir nomes públicos/privados, mensagens, comentários, arquivos e documentação diretamente, sem compatibilidade transitória. Fonte: solicitação do mantenedor.
- **DF8 — Um comando por classe:** a arquitetura gerada `I{Classe}Handler`/`{Classe}Handler` suporta exatamente um método `[Command]` por classe; mais de um produzirá diagnóstico em vez de colisão de arquivo/tipo. Fonte: contrato e naming atuais verificados.
- **DF9 — Código inválido não gera fonte:** qualquer erro de análise impede handler, registro DI e endpoint relacionados, sem `CS8785` e sem exceção do generator. Fonte: objetivo de robustez.
- **DF10 — Warnings aceitos:** NU5104 permanece permitido; qualquer erro ou warning adicional introduzido pelo plano reprova a entrega. Fonte: decisão anterior do mantenedor.
- **DF11 — Worktree do usuário:** alterações preexistentes no Demo, planos e gerados não serão revertidas nem reformatadas fora das linhas necessárias. Fonte: estado verificado do repositório.

---

## Histórico de decisões

**Fase preparatória (atualização RoyalCode):**

- **Assinaturas `ContinueAsync`:** foi considerada uma hipótese de `async void` por ligação com `Action`, depois rejeitada pelo mantenedor após inspeção das APIs.
  - **Conclusão:** SUPERSEDED por DF6; a correção válida é a assinatura nova com `TParam`, token e lambda `static`, já implementada.
- **Warnings de build:** CS8669 e demais warnings de código foram tratados; restaram nove NU5104.
  - **Conclusão:** SUPERSEDED por DF10; NU5104 não integra o escopo deste plano.

---

## Design alvo

### Contratos e bordas

- `WithParameterAttribute`: marcador de argumento fornecido pelo chamador; não implica `[FromRoute]`.
- Binding HTTP: copiar `[FromRoute]`, `[FromQuery]`, `[FromHeader]`, `[FromBody]`, `[FromForm]`, `[FromServices]` e `[AsParameters]` quando semanticamente válidos; sem marcador explícito, deixar inferência do framework atuar.
- `EditEntityAttribute<TEntity,TId>.RouteParameterName`: propriedade nomeada opcional para desambiguação; valor inexistente, duplicado ou incompatível produz RCCMD.
- `CommandValidationAttribute` ou alternativa decidida em Q1: contrato descrito na Fase 6, sem acesso implícito a entidade/UoW antes do carregamento.
- `IUnitOfWorkAccessor<T>.CompleteAsync`: mantém `Task<Result>` nesta entrega; a distinção entre problema esperado e exceção inesperada segue Q2.
- Diagnósticos: RCCMD000-RCCMD025 mantêm IDs; novos IDs começam em RCCMD026; texto pode ser corrigido diretamente, e cada ID recebe documentação no repositório.
- Hint names: usar nome de metadata totalmente qualificado, sanitizado e com sufixo estável quando necessário; colisões são detectadas antes de `AddSource`.

### Modelo, dados e persistência

```text
GenerationCandidate<TModel>
  Model TModel?                         somente DTO equatável
  IsValid bool                         erro impede todas as saídas dependentes

EquatableArray<T>
  Items immutable array                igualdade/hash por conteúdo e ordem

CommandModel
  CommandType TypeRef                  namespace + metadata name + nulabilidade necessária
  CommandMethod MethodRef              nome, retorno e flags semânticas
  Parameters EquatableArray<ParameterModel>
  Validations EquatableArray<ValidationModel>
  Persistence PersistenceModel?
  Endpoint EndpointModel?

EndpointModel
  Kind Command|Find|Search
  HttpMethod string
  Route RoutePatternModel
  Name string
  Group string?
  Binding EquatableArray<BindingModel>
  Response ResponseModel
  Metadata EndpointMetadataModel
```

- Não armazenar `ISymbol`, `SyntaxNode`, `Location`, `SemanticModel`, `Diagnostic`, `List<T>` mutável ou back-reference entre `CommandModel` e `EndpointModel` após a transformação.
- Representar tipos por metadata name/namespace/nullable/shape suficientes para emissão; comparar coleções por conteúdo.
- O adapter EF inicia, salva, commit/rollback e preserva a exceção definida em Q2; cancelamento nunca vira sucesso nem problema comum.

### Arquitetura alvo

```text
RoyalCode.SmartCommands.Generators/
  Analysis/
    *FactsReader.cs                     leitura semântica de AttributeData/ISymbol
    *Rules.cs                           regras puras compartilhadas
    ReservedIdentifiers.cs             nomes gerados por escopo
  Analyzers/
    SmartCommandsAnalyzer.cs            reporte RCCMD, condicionado a Q3
  Models/
    GenerationCandidate.cs
    EquatableArray.cs
    CommandModel.cs
    EndpointModel.cs
  Pipelines/
    CommandPipeline.cs                  saída individual
    ServiceRegistrationPipeline.cs      agregação válida e ordenada
    EndpointPipeline.cs                 agregação válida por grupo
  Emitters/
    CommandEmitter.cs
    ServiceRegistrationEmitter.cs
    MinimalApiEmitter.cs

RoyalCode.SmartCommands.Tests/
  Analyzer/                             diagnóstico, localização e severidade
  Incremental/                          cache/reexecução/determinismo
  Generation/                           fontes geradas e compilação da saída
  Runtime/                              mediator, retry e validação
  Packaging/                            conteúdo e consumer-smoke
```

### Pipeline alvo do handler

```text
HasProblems (quando WithValidateModel)
  -> validações adicionais ordenadas (Q1; uma vez)
  -> retry de concorrência (quando habilitado)
       -> Begin UoW
       -> find/edit entities
       -> decorators
       -> método Command
       -> Complete UoW
  -> Result/Result<T>
```

- Qualquer falha de `HasProblems` ou validação adicional encerra antes de iniciar transação.
- Validações adicionais ficam fora do retry e não podem depender de entidades que só existem após `Find`.
- Todos os `Task`/`ValueTask` são aguardados e recebem o `CancellationToken` do handler quando o contrato aceitar token.

### Matriz alvo de Minimal API

| Superfície | Estado atual | Alvo obrigatório | Expansão candidata |
|---|---|---|---|
| Command maps | GET/POST/PUT/PATCH/DELETE, resposta parcialmente inferida pelo verbo | modelo comum de binding, resposta e metadata; conflito de atributos diagnosticado | política explícita `Ok`/`Created`/`Accepted`/`NoContent` |
| `MapFind` | um `Id<TEntity,TId>` chamado `id` | validar rota, alias, tipo, acessibilidade e `NotFound` | chave alternativa/composta |
| `MapSearch` | GET, `[AsParameters]`, serviços hardcoded e `WithParameter` forçado para rota | async semântico, binding geral, grupo opcional consistente | cursor/stream/exportação |
| Created | placeholders `{0}` e propriedades por string | Q6, validação completa e `Location` determinística | `CreatedAtRoute` por endpoint name |
| Erros | `ProduceProblems` no método gerado | metadata OpenAPI coerente por categoria/status | integração configurável de catálogo ProblemDetails |
| Filtros | somente configuração externa | não duplicar exception handling do app | `[WithEndpointFilter<T>]` repetível, condicionado a Q4 |
| Body/form/file | body inferido do command | copiar binding explícito e validar GET/DELETE sem body implícito | upload/form/stream condicionado a Q4 |

### Segurança, concorrência e confiabilidade

- Não incluir mensagem/provider detail de exceções de persistência na resposta HTTP gerada.
- Não converter `OperationCanceledException` em `Result`, 500 ou retry esgotado.
- Não executar validação adicional ou efeito externo novamente em retry otimista.
- Validar `[FromForm]`/arquivos com cenários de antiforgery documentados; a biblioteca não desativa antiforgery.
- Endpoint name, group class name, handler type e hint name devem ser únicos após normalização.
- `Mediator` não manterá enumerador mutável; `next` terá comportamento determinístico quando invocado mais de uma vez.

### Compatibilidade, migração e rollout

- Não manter shims de API; atualizar todos os cenários, snapshots, Demo, README e `.docs/commands.md` na mesma fase da quebra.
- Registrar as quebras em changelog/notas da versão definida na Fase 12.
- Manter pacotes runtime em `net8.0`, `net9.0`, `net10.0` e generator/analyzer em `netstandard2.0`.
- Validar consumo do `.nupkg` local, não apenas `ProjectReference`, em todos os TFMs suportados.
- Não publicar pacote automaticamente neste plano.

---

## Ordem de execução

1. **Fase 1 (baseline e decisões)** — congela evidências e resolve contratos que bloqueiam implementação.
2. **Fase 2 (incrementalidade)** — remove o risco estrutural antes de adicionar regras/features.
3. **Fase 3 (analyzer/diagnósticos)** — torna entradas inválidas seguras e observáveis.
4. **Fase 4 (semântica/emissão)** — elimina parsing textual e colisões internas.
5. **Fase 5 (binding/EditEntity)** — corrige contratos já decididos sobre a base semântica.
6. **Fase 6 (validações)** — introduz a nova feature após Q1 e pipeline estável.
7. **Fase 7 (EF)** — aplica Q2 com testes de transação/cancelamento.
8. **Fase 8 (runtime/WorkContext)** — corrige estado do mediator e coerência do retry.
9. **Fase 9 (Minimal API atual)** — fecha bugs e completude dos mapeamentos existentes.
10. **Fase 10 (novos maps)** — implementa somente o pacote selecionado em Q4.
11. **Fase 11 (qualidade/distribuição)** — documentação, typos, pacote, TFM e Q5.
12. **Fase 12 (release)** — regressão integrada e inventário final de quebra.

Build/test padrão:

```powershell
dotnet restore SmartCommands.sln
dotnet build SmartCommands.sln -c Release --no-restore
dotnet test RoyalCode.SmartCommands.Tests/RoyalCode.SmartCommands.Tests.csproj -c Release --no-build
dotnet test RoyalCode.SmartCommands.Demo.Tests/RoyalCode.SmartCommands.Demo.Tests.csproj -c Release --no-build
```

---

## Fase 1 - Baseline e decisões de contrato

**Depende de:** DF1-DF11; respostas Q1-Q6 para desbloquear as fases indicadas.

**Escopo:** solução inteira em modo somente leitura, documentação do plano e novos testes de caracterização sem alterar contratos.

**O que/como:** registrar SHA/status, executar baseline sem aceitar novos warnings, catalogar fontes geradas/hint names/diagnósticos e obter as respostas humanas. Não normalizar arquivos já modificados pelo usuário.

**Tarefas:**

- [ ] Registrar no `Resultado da Fase 1` o commit, `git status --short`, SDKs instalados e alterações preexistentes a preservar.
- [ ] Executar build/test padrão e registrar contagem por projeto, erros e warnings distintos.
- [ ] Criar testes de caracterização que reproduzam as falhas de igualdade, null collection, múltiplos `[Command]`, múltiplos maps e entrada malformada sem ainda redesenhar a implementação.
- [ ] Inventariar arquivos/hint names gerados por cada cenário para detectar mudanças não intencionais nas fases seguintes.
- [ ] Responder Q1-Q6 e mover cada conclusão para `Decisões fechadas`/`Histórico de decisões`.
- [ ] Marcar as fases dependentes como bloqueadas se alguma pergunta permanecer aberta.

**Critérios de aceite:** baseline reproduzível registrado; nenhuma alteração do usuário perdida; cada Q possui resposta fechada ou fase dependente explicitamente bloqueada; testes de caracterização falham somente pelos bugs que pretendem capturar.

**Testes:** build/test padrão; `git diff --check`; `dotnet --info`; filtro xUnit dos novos testes de caracterização.

### Resultado da Fase 1

*a preencher*

---

## Fase 2 - Modelos incrementais e isolamento de entradas inválidas

**Depende de:** Fase 1; Q3 apenas para definir o destino final do reporte, não para criar modelos equatáveis.

**Escopo:** `Generators/IncrementalGenerator.cs`, `Generators/*Information.cs`, novas pastas `Models`/`Pipelines`, utilitário de testes do generator.

**O que/como:** substituir modelos mutáveis por records/DTOs equatáveis; encapsular coleções; separar candidate válido de erro; impedir modelos inválidos nas combinações de DI/map; reduzir `Collect()` ao que exige agregação.

**Tarefas:**

- [ ] Introduzir `EquatableArray<T>` compatível com `netstandard2.0` e testar igualdade/hash por conteúdo, ordem, vazio e default.
- [ ] Modelar command/find/search/map host/add-services sem `Diagnostic`, `Location`, syntax, symbol, listas mutáveis ou ciclos.
- [ ] Remover `MapInformation.CommandInfo` e construir `EndpointModel` completo na transformação do comando.
- [ ] Corrigir por substituição os bugs de igualdade de `MapApiHandlersInformation`, `MapInformation`, `FindInformation`, `MapCreatedInformation` e `MapResponseValuesInformation`.
- [ ] Filtrar `GenerationCandidate.IsValid` antes das pipelines de handler, DI e endpoint.
- [ ] Ordenar agregações por metadata name/endpoint name antes da emissão para saída determinística.
- [ ] Manter saída por comando independente; agregar somente registros DI e endpoints por host/grupo.
- [ ] Honrar `CancellationToken` em todas as transformações e seleções incrementais.
- [ ] Estender o test host para reutilizar `GeneratorDriver` e habilitar tracked steps.

**Critérios de aceite:** editar arquivo não relacionado produz `Cached`/`Unchanged` nos passos e fontes não afetados; alterar `WithOpenApi`, `IdType`, policy ou item de coleção invalida exatamente as saídas dependentes; entrada inválida não chega a nenhum emitter; igualdade e hash obedecem o mesmo conjunto de campos.

**Testes:** `dotnet test ... --filter "FullyQualifiedName~Incremental"`; rodar o mesmo driver duas vezes, depois com trivia/arquivo irrelevante e depois com alteração semântica; compilar toda saída e afirmar ausência de `CS8785`.

### Resultado da Fase 2

*a preencher*

---

## Fase 3 - Analyzer semântico e catálogo de diagnósticos

**Depende de:** Fase 2, Q3 e DF5/DF8/DF9.

**Escopo:** `CmdDiagnostics.cs`, novo analyzer/regras, `AnalyzerReleases.*.md`, testes Analyzer/Generation e documentação de diagnósticos.

**O que/como:** centralizar regras semanticamente; reportar uma ocorrência por causa; localizar atributo/argumento/parâmetro responsável; bloquear geração relacionada sem lançar exceção.

**Tarefas:**

- [ ] Implementar a arquitetura decidida em Q3 e compartilhar regras sem duplicar interpretação entre analyzer e generator.
- [ ] Corrigir diretamente `Attribte` e demais typos nos descritores existentes, preservando IDs RCCMD000-RCCMD025.
- [ ] Definir RCCMD026+ para colisão reservada, múltiplos commands, declaração não suportada, rota `EditEntity` ambígua/inexistente, maps conflitantes, endpoint/hint duplicado, validation inválida, binding inválido e `MapCreatedRoute` inconsistente.
- [ ] Diagnosticar classe nested/genérica/inacessível, método static/abstract/genérico/inacessível e combinações que gerariam C# inválido.
- [ ] Diagnosticar múltiplos atributos `Map*`, múltiplos hosts `[MapApiHandlers]`, endpoint names duplicados e group names que normalizam para a mesma classe.
- [ ] Diagnosticar identificadores reservados no escopo real de emissão; não rejeitar propriedade que não colide.
- [ ] Atualizar `AnalyzerReleases.Unshipped.md` e substituir links `google.com` por páginas locais/reais de cada regra.
- [ ] Testar ID, severidade, mensagem, argumentos, localização e ausência de fonte relacionada para cada erro.
- [ ] Garantir que código incompleto durante digitação não cause exceção; reportar somente quando houver informação suficiente.

**Critérios de aceite:** nenhuma fixture negativa produz `AD0001`, `CS8785` ou stack trace; todos os novos RCCMD constam em `SupportedDiagnostics` e `AnalyzerReleases.Unshipped.md`; colisões listadas em DF5 são erros; input válido não recebe falso positivo.

**Testes:** `dotnet test ... --filter "FullyQualifiedName~Analyzer|FullyQualifiedName~Diagnostics"`; compilações negativas para cada regra; `dotnet build RoyalCode.SmartCommands.Generators/... -c Release` com extended analyzer rules.

### Resultado da Fase 3

*a preencher*

---

## Fase 4 - Leitura semântica e emissão determinística

**Depende de:** Fases 2 e 3.

**Escopo:** readers/emitters de command, DI, find, search e maps; descriptors auxiliares e snapshots.

**O que/como:** usar `TargetSymbol`, `AttributeData.ConstructorArguments/NamedArguments` e comparação por metadata name; eliminar casts/índices/substrings sem guarda; consolidar tipo assíncrono e rotas em modelos comuns.

**Tarefas:**

- [ ] Substituir `TryGetAttribute` textual por símbolos/metadata names nas regras que afetam geração.
- [ ] Ler construtores, arrays e propriedades nomeadas de atributos por `TypedConstant`, emitindo RCCMD quando não forem constantes válidas.
- [ ] Detectar `Task`, `Task<T>`, `ValueTask` e `ValueTask<T>` semanticamente; não usar modificador `async` como contrato de retorno.
- [ ] Detectar `Result`/`Result<T>`, entidades, collections, context e `CancellationToken` por símbolo, não por nome simples.
- [ ] Criar parser único de route pattern para nomes, constraint, catch-all, optional e default; não usar `GetFirstRouteParameterName`/`Substring` espalhados.
- [ ] Gerar hint names e nomes de tipos por metadata name completo, incluindo namespace/nesting sanitizados.
- [ ] Adicionar cabeçalho gerado também a POCOs de resposta e manter `#nullable enable` em toda fonte.
- [ ] Corrigir nomes internos `GenerateReponseClass`, `assigment`, `Invoka`, `exitam`, `commando` e `requered` conforme DF7.
- [ ] Compilar cada fonte gerada dentro do teste, além de comparar snapshots relevantes.

**Critérios de aceite:** aliases, qualified/global names, atributos com array explícito e métodos que retornam `Task` sem modificador `async` geram o mesmo modelo correto; nenhum acesso inseguro conhecido permanece; duas classes homônimas em namespaces diferentes geram fontes distintas; toda fonte possui cabeçalho.

**Testes:** suites `Generation`, `Find`, `Search`, `Map`, `AddServices`; casos com `global::`, alias e `Task.FromResult`; `dotnet test RoyalCode.SmartCommands.Tests/...`.

### Resultado da Fase 4

*a preencher*

---

## Fase 5 - Binding de `WithParameter` e resolução de `EditEntity`

**Depende de:** Fase 4 e DF2-DF5.

**Escopo:** atributos runtime, command/search readers, handler/API emitters, testes unitários e Demo.Tests.

**O que/como:** aplicar o mesmo modelo de parâmetro externo a commands e filters; preservar binding explícito no endpoint; resolver ID de edição pela regra fechada, nunca pelo primeiro token arbitrário.

**Tarefas:**

- [ ] Remover `[FromRoute]` automático de `SearchInformation` para `WithParameter`.
- [ ] Capturar binding attributes suportados do parâmetro-fonte e copiá-los somente para o delegate Minimal API.
- [ ] Diagnosticar mais de uma fonte explícita, `[AsParameters]` incompatível, body implícito em GET/DELETE e `[FromRoute(Name=...)]` ausente no template.
- [ ] Adicionar `RouteParameterName` a `EditEntityAttribute<TEntity,TId>` com XML docs e exemplos.
- [ ] Implementar prioridade: propriedade explícita; única variável; `${entityParameterName}Id`; `entityParameterName`; erro em qualquer outro caso.
- [ ] Validar constraints/optionalidade da rota contra nulabilidade e tipo do ID quando determinável.
- [ ] Manter ordem do handler: ID de edit, command, todos os `WithParameter`, `ct`.
- [ ] Criar testes HTTP para route, query, header, DI, special type, custom `BindAsync` e nomes diferentes via atributo.
- [ ] Criar testes `EditEntity` com zero, uma, várias, match implícito, match explícito e ambiguidade.

**Critérios de aceite:** `WithParameter` sem atributo vem da rota quando seu nome está no template e da query quando não está para tipo parseável; header/form/service explícitos chegam ao método; `EditEntity` não seleciona o primeiro token incorreto; ambiguidade produz RCCMD e nenhuma fonte de endpoint.

**Testes:** testes generator/analyzer filtrados por `WithParameter|EditEntity`; testes de integração via `WebApplicationFactory`; build/test padrão.

### Resultado da Fase 5

*a preencher*

---

## Fase 6 - Validações adicionais do comando

**Depende de:** Fase 5 e Q1 respondida.

**Escopo:** novo(s) atributo(s) público(s), modelo/reader/emitter de validation, handler pipeline, DI, metadados de problemas, testes e documentação.

**O que/como:** implementar exatamente a alternativa de Q1; validators retornam `Result` e encerram o handler em falha; async é determinado pelo retorno e sempre aguardado; parâmetros usam resolução compartilhada, com restrições explícitas.

**Tarefas:**

- [ ] Criar o atributo decidido em Q1 com XML docs, ordem e `Conditional("COMPILE_TIME_ONLY")` quando aplicável.
- [ ] Descobrir validators semanticamente e ordenar por `Order`, rejeitando ordem duplicada se ela tornar execução ambígua.
- [ ] Aceitar somente os retornos decididos em Q1 e diagnosticar `void`, `async void`, `Result<T>`, tipos arbitrários, generic/ref/out/params e método inacessível.
- [ ] Resolver `CancellationToken`, dependências DI e parâmetros `[WithParameter]` usando o mesmo modelo do comando.
- [ ] Rejeitar entity/context/UoW em validação pré-carregamento; documentar uma fase pós-load como backlog separado.
- [ ] Mesclar dependências dos validators sem campos/ctor duplicados e diagnosticar mesmo nome com tipos diferentes ou nome reservado.
- [ ] Emitir `HasProblems` primeiro, depois validators, antes de Begin/retry; retornar imediatamente o primeiro `Result` com problemas.
- [ ] Agregar `[ProduceProblems]` dos validators à metadata HTTP e remover duplicatas por categoria.
- [ ] Testar múltiplos validators, sync/async, ordem, short-circuit, DI, parâmetro externo, cancelamento, decorators e retry.
- [ ] Atualizar `.docs/commands.md`, README e Demo com pelo menos um caso de validação assíncrona dependente de serviço.

**Critérios de aceite:** ordem exata `HasProblems -> validators -> UoW/retry`; validator com falha impede Begin/find/decorator/command/Complete; validator roda uma vez mesmo quando command sofre retry; não existe `async void`; cancelamento é observado; metadata lista problemas declarados.

**Testes:** snapshots e compilação de handler; testes runtime com fakes contadores; teste integração HTTP de erro/sucesso; build/test padrão.

### Resultado da Fase 6

*a preencher*

---

## Fase 7 - Confiabilidade do adapter Entity Framework

**Depende de:** Fase 1 e Q2 respondida.

**Escopo:** `RoyalCode.SmartCommands.EntityFramework`, testes novos do adapter e integração com handler/filtro HTTP.

**O que/como:** separar limpeza transacional de adaptação de erro; preservar cancelamento; testar transação real em SQLite; expor contexto tipado a subclasses de repository.

**Tarefas:**

- [ ] Implementar a política Q2 em `DbContextAccessor.CompleteAsync` sem esconder a exceção original.
- [ ] Tentar rollback quando save/commit falhar e existir transação iniciada pelo adapter; definir token de cleanup que não seja cancelado antes da tentativa.
- [ ] Preservar as duas falhas quando rollback também falhar, sem perder stack/causa da falha primária.
- [ ] Garantir que `OperationCanceledException` permaneça cancelamento e não seja convertido em `Result`/500 interno pelo adapter.
- [ ] Alterar `RepositoryAdapter<TEntity,TContext>` para disponibilizar `protected TContext Context` e usar o tipo concreto internamente.
- [ ] Criar projeto/suite de testes EF se necessário, com SQLite in-memory e doubles para save/commit/rollback.
- [ ] Testar uso direto do handler fora de HTTP e uso HTTP com o filtro/middleware do Demo, sem duplicar tratamento dentro do adapter.
- [ ] Documentar quais exceções são problemas esperados e quais atravessam a borda.

**Critérios de aceite:** sucesso salva e faz commit uma vez; falha tenta rollback; cancelamento não vira sucesso/problema; política Q2 é observável tanto fora quanto dentro de HTTP; subclasses projetam usando `Context` sem guardar o mesmo contexto novamente.

**Testes:** testes EF de begin/save/commit/rollback/cancel; teste integração Demo para ProblemDetails; `dotnet test` dos projetos afetados em Release.

### Resultado da Fase 7

*a preencher*

---

## Fase 8 - Runtime de decorators, WorkContext e retry

**Depende de:** Fases 4 e 6 para ordem do pipeline; DF5.

**Escopo:** `Mediator`, geração de decorators, `ConcurrencyRetryExtensions`, factory/options/DI e testes WorkContext.

**O que/como:** remover enumerador stateful, tornar composição previsível, fazer opções de problema esgotado funcionarem com ou sem `Operation` e revisar observabilidade sem ampliar retry transitório.

**Tarefas:**

- [ ] Substituir o enumerador de `Mediator` por delegate pipeline composto em ordem reversa, sem recurso descartável mantido.
- [ ] Definir/testar que cada chamada de `next` executa novamente o restante do pipeline, sem compartilhar posição mutável.
- [ ] Manter uma instância de mediator/pipeline por `Handle`, sem estado entre requests.
- [ ] Fazer `ExhaustedProblemTypeId` e `ExhaustedProblemDetail` valerem mesmo quando `[WithRetryOnConcurrency]` não informa `Operation`.
- [ ] Definir chave estável default de operação para factory/registro sem usar texto localizado.
- [ ] Preservar validação fora do retry e Begin/find/command/Complete dentro dele.
- [ ] Testar rollback/cleanup entre tentativas, budget `1`, opções default, custom factory por command/operação e cancelamento durante rollback.
- [ ] Avaliar logging/métrica de tentativa e backoff como design; manter fora da implementação salvo nova decisão humana.

**Critérios de aceite:** decorators executam em ordem registrada; `next` repetido é determinístico; nenhuma enumeração fica viva; opções de exhausted são observadas em todos os caminhos; quantidade de tentativas e cleanup corresponde ao contrato; efeitos anteriores ao retry ocorrem uma vez.

**Testes:** suites `Decorators` e `RetryOnConcurrency`; testes concorrentes de handlers separados; build/test padrão.

### Resultado da Fase 8

*a preencher*

---

## Fase 9 - Completude dos mapeamentos Minimal API existentes

**Depende de:** Fases 3-6, Q6 para alteração de Created.

**Escopo:** todos os atributos `Map*` atuais, host/grupo, emitters, Problem metadata, OpenAPI, generator tests e Demo.Tests.

**O que/como:** unificar command/find/search em `EndpointModel`, corrigir inconsistências e validar todo dado que participa da assinatura, rota, nome, resposta ou metadata.

**Tarefas:**

- [ ] Fazer zero/um/múltiplos `Map*` terem comportamento explícito; múltiplos geram RCCMD, nunca prioridade silenciosa por `else if`.
- [ ] Tornar `MapGroup` opcional de forma consistente ou diagnosticá-lo como obrigatório conforme contrato documentado; remover diagnóstico local atualmente descartado em Search.
- [ ] Validar endpoint/group names vazios, duplicados e colisões após `ToPascalCase`.
- [ ] Validar `MapIdResultValue`/`MapResponseValues`: retorno com valor, propriedade pública legível, tipo emitível, lista não vazia e nomes sem duplicata.
- [ ] Implementar Q6 e validar placeholders/propriedades de `MapCreatedRoute`; gerar URI sem substituição textual posicional frágil.
- [ ] Revisar status por verbo: não forçar `NoContent` para Delete quando o contrato retorna valor sem diagnóstico; tornar escolha consistente/documentada.
- [ ] Corrigir assinaturas/metadata de `MapFind` e `MapSearch`, incluindo tipo de ID, `NotFound`, async semântico, group e policies anuláveis.
- [ ] Gerar metadata de todos os `ProduceProblems`, `WithSummary`, `WithDescription`, authorization/policies e respostas de sucesso.
- [ ] Verificar OpenAPI gerado para body obrigatório/opcional, parâmetros, status e ProblemDetails.
- [ ] Criar matriz end-to-end de GET/POST/PUT/PATCH/DELETE/Find/Search, com e sem body e com policies.

**Critérios de aceite:** cada superfície da coluna “Alvo obrigatório” na matriz está implementada; nenhum atributo é ignorado silenciosamente; OpenAPI contém parâmetros e respostas reais; endpoint names/hints são únicos; todas as rotas geradas iniciam sem exceção do ASP.NET Core.

**Testes:** generator snapshots + compilação; `WebApplicationFactory`; geração/inspeção do JSON OpenAPI; build/test padrão.

### Resultado da Fase 9

*a preencher*

---

## Fase 10 - Novas capacidades de mapeamento Minimal API

**Depende de:** Fase 9 e Q4 respondida.

**Escopo:** novos atributos/contratos selecionados, emitter compartilhado, Demo e documentação.

**O que/como:** implementar somente os casos aprovados em Q4; cada nova API deve reutilizar `EndpointModel`, binding, response metadata e diagnósticos, sem novo gerador paralelo.

**Tarefas:**

- [ ] Escrever uma tabela de caso de uso, declaração do usuário, assinatura gerada, resposta/status, OpenAPI e limitações para cada candidato.
- [ ] Implementar filtro de endpoint genérico/repetível caso aprovado, preservando ordem declarada e DI do ASP.NET Core.
- [ ] Implementar política explícita de resultado para `Ok`/`Created`/`Accepted`/`NoContent` caso aprovada, com diagnóstico de retorno incompatível.
- [ ] Implementar somente os itens ampliados escolhidos: 202/location, chave alternativa/composta, form/file/stream ou nenhum.
- [ ] Evitar um `MapEndpoint` genérico se ele apenas trocar atributos tipados por strings sem diagnóstico melhor.
- [ ] Criar cenário Demo real por feature e teste HTTP/OpenAPI correspondente.
- [ ] Documentar decisão e mover candidatos não escolhidos para backlog com critério de retomada.

**Critérios de aceite:** toda feature aprovada possui contrato público, diagnóstico negativo, código compilado, teste HTTP e metadata OpenAPI; nenhuma regressão nos maps existentes; recursos não aprovados não entram parcialmente.

**Testes:** filtro xUnit por cada feature; WebApplicationFactory e OpenAPI; build/test padrão.

### Resultado da Fase 10

*a preencher*

---

## Fase 11 - Qualidade transversal, pacote, documentação e CI

**Depende de:** Fases 3-10; Q5 para workflow.

**Escopo:** estilo, docs, README, pack, analyzer package, smoke consumers e automação.

**O que/como:** corrigir typos/stale docs, alinhar package metadata, verificar conteúdo do `.nupkg`, adicionar testes por TFM e implementar a opção de Q5.

**Tarefas:**

- [ ] Adicionar `.editorconfig` mínimo alinhado ao estilo verificado, sem reformatar em massa arquivos fora do diff.
- [ ] Corrigir `EnterpisePatterns` para `SmartCommands`, adicionar descrições reais por pacote e validar README/icon/license/repository no `.nupkg`.
- [ ] Verificar que o package do generator contém `RoyalCode.SmartCommands.Generators.dll` e dependência necessária em `analyzers/dotnet/cs`, sem assembly runtime indevido.
- [ ] Criar consumer-smoke que restaura os `.nupkg` locais e compila um command/map válido e um inválido em `net8.0`, `net9.0` e `net10.0`.
- [ ] Corrigir `README.md` e `.docs/commands.md`, inclusive sintaxe genérica atual de `EditEntity`, validation adicional, binding e maps.
- [ ] Adicionar errata à revisão de 2026-07-13 conforme DF6, sem manter a conclusão falsa de `async void` como fato atual.
- [ ] Renomear `.docs/archtecture.md` para `.docs/architecture.md` e atualizar `SmartCommands.sln`/links; corrigir `SmartProbelms` e textos com encoding inválido.
- [ ] Executar busca de typos conhecidos e revisar XML docs de toda API nova/alterada.
- [ ] Implementar workflow ou script conforme Q5; nunca publicar pacote/nuget no workflow desta entrega.
- [ ] Manter allowlist somente de NU5104 e falhar em qualquer outro warning novo.

**Critérios de aceite:** package metadata aponta para o remote correto; consumer-smoke carrega generator/analyzer e compila em três TFMs; documentação não usa APIs antigas; busca de typos conhecidos retorna zero; CI/script reproduz build/test/pack; somente NU5104 conhecido permanece.

**Testes:** `dotnet pack` dos quatro pacotes em Release; inspeção do `.nupkg`; consumer-smoke net8/net9/net10; markdown/link check local; build/test padrão em Windows e Linux quando Q5=A.

### Resultado da Fase 11

*a preencher*

---

## Fase 12 - Compatibilidade, regressão e preparação de release

**Depende de:** Fases 1-11 concluídas e Q1-Q6 fechadas.

**Escopo:** solução, pacotes locais, Demo, changelog/notas e este plano.

**O que/como:** executar a matriz final do zero, revisar API pública e generated diffs, documentar breaking changes e confirmar que nenhuma tarefa/dúvida ficou implicitamente aberta.

**Tarefas:**

- [ ] Limpar somente `bin/obj` conhecidos após verificar caminhos; restaurar e executar build/test/pack do zero.
- [ ] Executar testes incrementais repetidos e consumer-smoke com os `.nupkg` finais.
- [ ] Revisar diff de API pública e registrar cada remoção/adição/quebra, incluindo exemplos de migração direta.
- [ ] Revisar fontes geradas do Demo e snapshots; separar alterações esperadas de ruído.
- [ ] Confirmar que não existe `async void`, `CS8785`, `AD0001`, fonte duplicada, erro de OpenAPI ou warning novo.
- [ ] Registrar versão/release notes sem publicar; atualizar `SCmdVer` somente com autorização explícita do mantenedor.
- [ ] Preencher todos os `Resultado da Fase`, rastreabilidade, riscos e diferidos; marcar o plano concluído somente após critérios globais.

**Critérios de aceite:** todos os comandos finais verdes; 100% das perguntas fechadas; todos os critérios globais satisfeitos; pacote local consumível nos três TFMs; diff final não toca alterações não relacionadas do usuário.

**Testes:** build/test padrão sem cache; pack + consumer-smoke; testes HTTP/OpenAPI; `git diff --check`; inspeção de warnings e generated files.

### Resultado da Fase 12

*a preencher*

---

## Matriz de rastreabilidade

| Objetivo | Fase(s) | Decisão(es) | Critério(s) de aceite | Teste(s) |
|---|---|---|---|---|
| Generator incremental/determinístico | 1-4 | DF8, DF9, Q3 | cache seletivo; modelo por valor; nenhuma fonte inválida | tracked steps, equality, compilação da saída |
| Diagnósticos completos | 3-5, 9 | DF4, DF5, DF8, DF9, Q3 | RCCMD localizado; sem AD0001/CS8785 | Analyzer/Diagnostics negativos |
| Binding e EditEntity corretos | 5 | DF2-DF5 | inferência/atributos preservados; ambiguidade bloqueada | generator + WebApplicationFactory |
| Validações adicionais | 6 | Q1, DF2, DF5 | ordem/short-circuit/async/CT/DI definidos | snapshots + fakes + HTTP |
| Runtime EF/WorkContext/decorators confiável | 7-8 | Q2, DF5, DF6 | cancelamento e exceção corretos; retry/decorators determinísticos | SQLite, retry e decorator tests |
| Minimal API completa/evoluída | 9-10 | DF2-DF4, Q4, Q6 | maps atuais completos; somente features aprovadas | HTTP matrix + OpenAPI |
| Qualidade/distribuição/compatibilidade | 11-12 | DF1, DF6, DF7, DF10, DF11, Q5 | docs/pacotes/TFMs/automação verdes | pack, consumer-smoke, build/test final |

---

## Invariantes a preservar

1. Não gerar nem introduzir `async void`; todo trabalho assíncrono é aguardado e recebe token quando aplicável.
2. `HasProblems` e validações adicionais executam antes de transação, find, decorators e command; validações adicionais executam uma vez fora do retry.
3. Entrada inválida produz diagnóstico, não fonte parcial, exceção do generator, `CS8785` ou `AD0001`.
4. `WithParameter` aparece na interface/implementação do handler e no delegate HTTP na mesma ordem, sem significar rota por si só.
5. Identificadores reservados do generator não são renomeados silenciosamente; colisões diagnosticáveis bloqueiam emissão.
6. Cancelamento não é convertido em sucesso, problema comum ou retry esgotado.
7. Handlers permanecem utilizáveis fora de HTTP; tratamento de exceção de Minimal API não vaza para contratos de domínio/persistência.
8. Código gerado mantém `// <auto-generated/>`, `#nullable enable`, nomes determinísticos e compila sem warnings novos.
9. Pacotes runtime continuam em net8/net9/net10 e generator/analyzer em netstandard2.0.
10. Os nove NU5104 aceitos podem permanecer; nenhum outro warning é aceito por padrão.
11. Não reverter, apagar ou reformatar alterações preexistentes do usuário.
12. Toda quebra pública é direta, documentada e testada, sem shim `[Obsolete]`.

---

## Critérios globais de conclusão

- Q1-Q6 respondidas e convertidas em decisões fechadas/histórico.
- Nenhuma falha de igualdade, parsing textual inseguro ou back-reference listada no contexto permanece.
- Analyzer/generator cobrem entradas válidas e inválidas sem crash nem fonte parcial.
- Pipeline do handler respeita a ordem e a semântica de cancelamento definidas.
- Matriz de Minimal API atual possui teste HTTP e OpenAPI por superfície.
- Adapter EF, mediator e retry possuem testes diretos de falha/cancelamento, não apenas snapshots.
- Pacotes locais são consumidos com sucesso em net8.0, net9.0 e net10.0.
- README, docs, AnalyzerReleases, XML docs e release notes refletem os contratos finais.
- `dotnet build SmartCommands.sln -c Release` não possui erro nem warning além de NU5104 aceito.
- Os dois projetos de testes passam integralmente em Release e `git diff --check` não relata erro.

---

## Riscos

| Risco | Gatilho | Impacto | Mitigação | Estado |
|---|---|---|---|---|
| Analyzer e generator divergirem | analyzer aceita/rejeita caso diferente do emitter | falso positivo ou código inválido | compartilhar facts/rules puros e testar o mesmo fixture nos dois | Aberto |
| Igualdade esconder mudança real | tracked step retorna Cached após alterar campo relevante | fonte obsoleta no IDE/build incremental | testes campo a campo e hash/equals; rerun limpo versus incremental | Aberto |
| Refactor incremental alterar todos os hints | diff massivo/duplicado em generated files | revisão difícil e colisão | inventário Fase 1, nomes FQN determinísticos e migração em fase única | Aberto |
| Binding inferido escolher body/DI inesperado | parâmetro complexo sem atributo em POST | endpoint inicia errado ou lê fonte incorreta | preservar binding explícito, diagnósticos e testes reais ASP.NET | Aberto |
| Validator causar efeito repetido | validator colocado dentro do retry | duplicação de consulta/efeito | invariante e teste contador com conflito forçado | Aberto |
| Mudança EF quebrar consumidor não HTTP | consumidor esperava exceção convertida em Result | breaking runtime | Q2, release notes e testes das duas bordas | Aberto |
| Rollback usar token cancelado | cancelamento durante save | transação fica aberta/erro secundário | token de cleanup definido e teste específico | Aberto |
| Novas features HTTP ampliarem escopo | Q4=B sem priorização | atraso e abstrações incompletas | matriz de caso de uso e implementar somente aprovadas | Aberto |
| Metadados OpenAPI divergirem do runtime | status/body real não aparece na spec | clientes gerados incorretos | teste do JSON OpenAPI e resposta HTTP para cada map | Aberto |
| Dependências não suportarem smoke em TFM | restore/compile falha em net8/net9 | pacote anuncia suporte incorreto | consumer-smoke por `.nupkg` antes de release | Aberto |
| Worktree concorrente sofrer sobreposição | arquivos do Demo mudam durante execução | perda/conflito de trabalho do usuário | registrar status por fase e editar somente hunks necessários | Aberto |
| CRLF tornar snapshots frágeis | execução Linux difere de Windows | CI falso negativo | comparar texto normalizado para `\n` sem impor newline do SO | Aberto |

---

## Diferidos e backlog

- Validações pós-load com acesso a entidades/contexto — destino: plano próprio após estabilizar validação pré-UoW.
- Backoff, jitter, métricas e logging por tentativa de concorrência — destino: feature WorkContext/retry; não confundir com transient retry.
- Handler lifetime configurável e geração keyed DI — destino: avaliação de DI futura.
- API versioning, OData e GraphQL — destino: integrações separadas.
- Geração contract-first a partir de OpenAPI — destino: pesquisa separada.
- Code fixes para RCCMD — destino: pacote analyzer posterior ao catálogo estabilizado.
- Benchmark de tempo/memória do generator em solução grande — destino: performance após Fase 2; tracked steps são obrigatórios nesta entrega.
- Suporte amplo a nested command types — destino: implementar somente após naming/visibilidade estabilizados; até lá diagnosticar os casos não suportados.
- Multiple endpoints para a mesma command class — destino: design separado; nesta entrega atributos conflitantes são erro.
- Publicação automática NuGet — destino: plano de release/segredos, fora de Q5.

---

## Referências

- `.docs/templates/template-ai-implementation-plan.md`.
- `.docs/reviews/2026-07-13-atualizacao-libs-royalcode-breaking-continueasync.md`.
- `.docs/commands.md`, `README.md` e `Directory.Build.props`.
- `RoyalCode.SmartCommands.Generators/Generators/IncrementalGenerator.cs`.
- `RoyalCode.SmartCommands.Generators/Generators/CommandHandlerGenerator.cs`.
- `RoyalCode.SmartCommands.Generators/Generators/SearchGenerator.cs` e `SearchInformation.cs`.
- `RoyalCode.SmartCommands.Generators/CmdDiagnostics.cs` e `AnalyzerReleases.*.md`.
- `RoyalCode.SmartCommands.EntityFramework/Adapters/DbContextAccessor.cs` e `RepositoryAdapter.cs`.
- `RoyalCode.SmartCommands/Mediator.cs` e `RoyalCode.SmartCommands.WorkContext/Extensions/ConcurrencyRetryExtensions.cs`.
- [ASP.NET Core Minimal API parameter binding](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/parameter-binding?view=aspnetcore-10.0).
- [ASP.NET Core Minimal API responses](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0).
- [ASP.NET Core OpenAPI metadata](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/include-metadata?view=aspnetcore-10.0).
- [ASP.NET Core Minimal API filters](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/min-api-filters?view=aspnetcore-10.0).
- [ASP.NET Core API error handling](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0).
- [Roslyn Incremental Generators Cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.cookbook.md).

---

## Comandos de manutenção para a IA executora

- Antes de iniciar uma fase, ler `Depende de`, `Decisões fechadas`, `Histórico de decisões`, `Invariantes a preservar`, `Critérios globais de conclusão` e `Riscos`.
- Antes do primeiro edit de uma fase, verificar as fontes citadas e atualizar `Estado atual do código` se estiver divergente.
- Ao encontrar decisão ausente, parar a fase, registrar nova `Q<n>` e marcá-la como `Bloqueada`.
- Marcar tarefa `- [x]` somente após validar o comportamento ou registrar a impossibilidade de validação.
- Registrar em `Resultado da Fase`: entregáveis, arquivos/projetos alterados, decisões aplicadas, testes executados, warnings, desvios e pendências.
- Ao concluir uma fase, atualizar status, barra, tabela, matriz de rastreabilidade e riscos relacionados.
- Antes de editar arquivo já modificado pelo usuário, inspecionar o diff e limitar o patch ao hunk necessário.
- Ao concluir o plano, confirmar que perguntas estão fechadas ou explicitamente diferidas e que nenhum critério global ficou apenas inferido.
