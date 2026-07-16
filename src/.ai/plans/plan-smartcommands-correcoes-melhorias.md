# Plan: Correções, endurecimento e evolução do SmartCommands (`smartcommands-correcoes-melhorias`)

## Status: RASCUNHO - somente Q4 (novas capacidades HTTP) permanece aberta

## Progresso

`█░░░░░░░░░░░` **8%** - 1 de 12 fases concluídas

| Fase | Estado |
|---|---|
| Fase 1 - Baseline e decisões de contrato | Concluida |
| Fase 2 - Modelos incrementais e isolamento de entradas inválidas | Em andamento (base 0.4.0 pronta; migração bloqueada no gate) |
| Fase 3 - Diagnósticos semânticos e catálogo | Pendente |
| Fase 4 - Leitura semântica e emissão determinística | Pendente |
| Fase 5 - Binding de `WithParameter` e resolução de `EditEntity` | Pendente |
| Fase 6 - Validações adicionais do comando | Pendente |
| Fase 7 - Confiabilidade do adapter Entity Framework | Pendente |
| Fase 8 - Runtime de decorators, WorkContext e retry | Pendente |
| Fase 9 - Completude dos mapeamentos Minimal API existentes | Pendente |
| Fase 10 - Novas capacidades de mapeamento Minimal API | Bloqueada por Q4 |
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
- [Roslyn Incremental Generators Cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.cookbook.md) — modelos do pipeline devem ter igualdade por valor, não carregar símbolos/syntax/locations e encapsular coleções; embora a recomendação geral separe diagnósticos, DF15 adota DTO symbol-free no generator para evitar uma segunda análise semântica, seguindo o precedente do SmartSelector.
- `git remote -v` — o repositório correto é `https://github.com/Royal-Code/SmartCommands.git`.
- `git status --short` em 2026-07-13 — há alterações do usuário no plano/Demo e arquivos gerados; este plano não pode sobrescrevê-las.

### Estado atual do código (verificado em 2026-07-14)

- **Baseline revalidado:** `dotnet build SmartCommands.sln -c Release --no-restore` conclui sem erros e somente com os nove NU5104 aceitos; os testes passam 87/87 no SmartCommands e 68/68 no Demo, totalizando 155/155.
- **Warnings aceitos:** o último build informado pelo mantenedor contém apenas nove NU5104; eles não serão tratados neste plano.
- **Sem `async void` no pacote:** os extension methods permanecem assíncronos e retornam `Task`; a alteração já aplicada foi a nova assinatura com `TParam`, `CancellationToken` e lambda `static`.
- **Cabeçalho gerado presente:** `Util.GeneratedCode` já espera `// <auto-generated/>` e `#nullable enable`; CS8669 não faz parte do trabalho pendente.
- **Generator incremental apenas nominalmente:** usa `IIncrementalGenerator` e `ForAttributeWithMetadataName`, mas os modelos mutáveis e os `GetHashCode` por referência impedem cache confiável.
- **Modelos do pipeline retêm símbolos (verificado em 2026-07-14):** os `*Information` carregam `TypeDescriptor`/`ParameterDescriptor`/`ServiceTypeDescriptor` da lib base, que guardam `ISymbol` — o que mantém a `Compilation` viva entre builds e agrava o problema de cache. A emissão, porém, **nunca acessa `.Symbol`** (zero ocorrências), o que torna a migração para snapshots delimitada e de baixo risco semântico na emissão. Ver `Stack de geração`.
- **Base em 0.3.0:** `RoyalCode.Extensions.SourceGenerator` 0.3.0 já expõe a fronteira descritores/snapshots e documenta que os descritores não devem cruzar para o pipeline; `MatchSelection`, `PropertyMatch`, `PropertySelection` e `AssignDescriptor` deixaram de ter igualdade de valor exatamente para impedir esse uso.
- **Falha concreta de igualdade:** `MapApiHandlersInformation.Equals(object)` testa `AddHandlersServicesInformation` e sua igualdade tipada ignora `WithOpenApi`.
- **Falhas concretas nos demais modelos:** `FindInformation` ignora `IdType`; `MapInformation` chama `SequenceEqual` sobre coleções anuláveis; `MapCreatedInformation` e `MapResponseValuesInformation` comparam coleções por referência.
- **Transformações frágeis:** existem casts diretos para `GenericNameSyntax`, acessos `Arguments[0]`/`Arguments[1]`, verificações por texto e extração manual de rota por `Substring`.
- **Entradas inválidas contaminam agregações:** os modelos com erro ainda são coletados para DI e Minimal API; isso permite exceção do generator em vez de somente RCCMD.
- **`WithParameter` inconsistente:** comandos encaminham o parâmetro ao handler, mas search adiciona `[FromRoute]` mesmo quando a rota não declara o nome.
- **`EditEntity` ambíguo:** o endpoint escolhe o primeiro parâmetro encontrado na rota, independentemente do nome do parâmetro da entidade.
- **Mapeamentos existentes:** há `MapPost`, `MapPut`, `MapPatch`, `MapDelete`, `MapGet`, `MapFind` e `MapSearch`; resposta, binding, rota criada e metadados não usam um modelo comum.
- **Adapter EF acoplado ao formato de erro:** `CompleteAsync` captura qualquer exceção e a converte implicitamente em `Result`, embora handlers também possam ser usados fora de HTTP.
- **Cobertura concentrada em `net10.0`:** os pacotes compilam para três TFMs, mas os testes executam somente em `net10.0` e não há consumer-smoke dos pacotes por TFM.
- **Automação preservada:** não existe `.editorconfig` nem workflow de CI; por DF16, o estado de GitHub Actions não será alterado nesta entrega.
- **Metadados incorretos:** `pack.targets` aponta para `Royal-Code/EnterpisePatterns`; o remote local aponta para `Royal-Code/SmartCommands`.

### Lacunas, conflitos e restrições

- **Contrato público pendente:** somente o primeiro pacote de novas capacidades HTTP permanece dependente de Q4; Q1-Q3, Q5 e Q6 foram fechadas em DF13-DF17.
- **Diagnósticos no generator:** por DF15, o transform produz DTOs equatáveis e symbol-free; `Diagnostic`/`Location` são reconstruídos somente em `RegisterSourceOutput`, sem uma segunda análise por `DiagnosticAnalyzer`.
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

### Stack de geração: Extensions.SourceGenerator, SmartSelector e SmartCommands

Três repositórios formam a stack de geração, e a Fase 2 depende de entender a divisão de trabalho entre eles.

**`RoyalCode.Extensions.SourceGenerator` (repo Utils) — a base.** Não é um generator: é a biblioteca sobre a qual os
generators são escritos. Oferece (a) **descritores** que leem a semântica do Roslyn (`TypeDescriptor`,
`PropertyDescriptor`, `ParameterDescriptor`, `ServiceTypeDescriptor`, `EditTypeDescriptor`), (b) o **matching** de
propriedades entre tipos (`MatchSelection`, `AssignDescriptor` e os resolvers), (c) os **snapshots** symbol-free
(`TypeSnapshot`, `PropertySnapshot`, `MatchSelectionSnapshot`) e (d) os **emitters** de C# (`ValueNode`,
`ClassGenerator`, `MethodGenerator`, `FieldGenerator`...). Distribuída por NuGet e embutida como analyzer dentro dos
pacotes dos generators que a consomem.

**A fronteira que a 0.3.0 tornou explícita.** A lib tem dois modelos, e confundi-los é a causa raiz do problema de
incrementalidade:

| | Modelo de trabalho | Modelo do pipeline |
|---|---|---|
| Tipos | `TypeDescriptor`, `PropertyDescriptor`, `MatchSelection`, `AssignDescriptor` | `TypeSnapshot`, `PropertySnapshot`, `MatchSelectionSnapshot` |
| Contém | `ISymbol`, estado mutável (hints, `Parent`) | apenas escalares |
| Igualdade | por nome/estrutura, **onde existe** — `MatchSelection` e afins **não têm** igualdade de valor (removida na 0.3.0, de propósito) | por valor, em toda a árvore |
| Onde vive | dentro do `transform`, enquanto se resolve a semântica | do `transform` para frente: é o que o pipeline retém e compara entre builds |

Reter um descritor no pipeline mantém a `Compilation` inteira viva entre builds e torna o cache não confiável. Por isso
a conversão via `MatchSelectionSnapshotFactory.Create(selection)` no fim do `transform`, deixando só o snapshot escapar.

**`RoyalCode.SmartSelector` — o precedente.** Já fez essa migração (`feat: add symbol-free incremental snapshots`) e
serve de roteiro para a Fase 2: os `*Information` carregam `MatchSelectionSnapshot` e `DiagnosticInfo[]`, nunca
símbolos. Os **diagnósticos** seguem o modelo adotado em DF15: `DiagnosticInfo` guarda `Id`, argumentos como `string` e
a localização **decomposta** (`FilePath` + `TextSpan` + `LinePositionSpan`) — tudo equatável —, e reconstrói
`Location`/`Diagnostic` apenas em `RegisterSourceOutput`, via `AnalyzerDiagnostics.Get(id)`. Não há `DiagnosticAnalyzer`
separado, e portanto não há duas leituras semânticas para manter em sincronia.

**`RoyalCode.SmartCommands` — o que falta.** Consome a base pesadamente, mas ainda no modelo de trabalho: os
`*Information` (valores do pipeline) carregam `TypeDescriptor`/`ParameterDescriptor`/`ServiceTypeDescriptor`
diretamente, e portanto símbolos. O ponto favorável, verificado: **a emissão nunca acessa `.Symbol`** — os emitters só
usam dados derivados. A migração é delimitada e de baixo risco semântico na emissão; o que falta é a base oferecer
os fatos necessários em forma symbol-free e o SmartCommands assumir as classificações específicas de seu domínio.

**Inventário verificado do que o SmartCommands consome da base** (`RoyalCode.SmartCommands.Generators`, contagem
textual por ocorrência com `rg -o` + `Measure-Object -Line`; os números não representam dependências únicas):

- **Descritores:** `TypeDescriptor` (85), `ParameterDescriptor` (46), `ServiceTypeDescriptor` (29),
  `PropertyDescriptor` (7), `EditTypeDescriptor` (5), `IdPropertyBoundToEntityParameter` (4).
- **Emitters:** `ClassGenerator` (28), `MethodGenerator` (18), `ValueNode` (8), `FieldGenerator` (6),
  `InterfaceGenerator` (4), `ConstructorGenerator` (1). Não retêm estado do pipeline; seguem como estão.
- **Membros de `TypeDescriptor` usados na emissão:** `Name`, `Namespaces` (23), `MayBeNull` (6), `UnderlyingType` (4),
  `IsNullable` (2), `IsArray` (2), `ArrayType` (2), `GenericType` (2), `IsGenericType` (1), `HasValueType` (1),
  `IsVoid` (2), `IsVoidTask` (3), `IsCancellationToken` (4), `MustBeTask()` (2).
- **Hints (o ponto crítico):** `IsEntity` (3), `IsContext` (3), `IsHandlerParameter` (3), `IsCollectionOfEntities` (3),
  mais os `MarkAs*` correspondentes (5). São **estado mutável fora da igualdade** do `TypeDescriptor` — dois descritores
  que diferem apenas por hint comparam iguais. Precisam virar dados de primeira classe no modelo, não hints aplicados
  depois.
- **`.Symbol`: zero usos.** Nenhum emitter depende do símbolo.
- **Não usa** `MatchSelection`/`AssignDescriptor`/snapshots — o matching de propriedades é território do SmartSelector.

**Consequência: a Fase 2 começa por uma release da base (`Extensions.SourceGenerator` 0.4.0)**, que precisa entregar:

1. **`EquatableArray<T>`** — igualdade/hash por conteúdo e ordem, `netstandard2.0`; `default` e vazio representam a
   mesma coleção sem itens e possuem o mesmo hash. A Fase 2 previa criá-lo aqui; o lugar dele é a base, porque todo
   generator da stack precisa do mesmo.
2. **Um snapshot estrutural de tipo suficiente** — o `TypeSnapshot` carrega identidade por metadata name/namespace,
   nulabilidade e forma semântica equatável: tipo nomeado/array, elemento, argumentos genéricos, original definition e
   fatos intrínsecos necessários. Não carrega decisões como `MustBeTask()` nem papéis contextuais; Task/ValueTask,
   Result, CancellationToken e wrapping são classificados pelo SmartCommands a partir desses fatos.
3. **Uso do tipo como snapshot separado** — `TypeUsageSnapshot` combina `TypeSnapshot` com papéis equatáveis
   (`IsEntity`, `IsContext`, `IsHandlerParameter`, `IsCollectionOfEntities`), resolvidos no `transform` e congelados.
   `ParameterSnapshot`/`ServiceTypeSnapshot` referenciam esse uso; o tipo estrutural não muda conforme o papel.
4. **Snapshots dos demais descritores** — equivalentes symbol-free de `ParameterDescriptor`, `ServiceTypeDescriptor`,
   `EditTypeDescriptor` e `IdPropertyBoundToEntityParameter`.
5. **Um `DiagnosticInfo` reutilizável e neutro** — hoje cada generator tem o seu (o do SmartSelector é a referência);
   promovê-lo à base evita a terceira cópia. `ToDiagnostic(Func<string, DiagnosticDescriptor> descriptorResolver)`
   reconstrói `Location`/`Diagnostic` sem conhecer `AnalyzerDiagnostics`; o catálogo pertence ao generator consumidor.
6. **`GenerationCandidate<T>`** — contrato comum para transportar modelo opcional, validade e diagnósticos sem deixar
   entrada inválida alcançar emitters ou agregações.

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
- Alterar SmartProblems, SmartValidations, SmartSearch, SmartSelector, WorkContext ou outras bibliotecas RoyalCode externas a esta solução. **Exceção: `RoyalCode.Extensions.SourceGenerator`** — é a base de geração desta solução e evolui junto com ela (DF12).
- Reestruturar o Demo segundo os planos de arquitetura em edição; o Demo receberá somente cenários necessários para validar SmartCommands.
- Preservar APIs antigas por aliases, duplicação ou `[Obsolete]`.
- Implementar OData, GraphQL, API versioning ou geração baseada em OpenAPI nesta entrega.
- Adicionar retry para falhas transitórias de infraestrutura; `WithRetryOnConcurrency` continua exclusivo de concorrência otimista.

---

## Perguntas ao humano

- **Q4 — Primeiro pacote de novas capacidades HTTP:** quanto deve entrar após corrigir os mapeamentos atuais?
  - **Opções:**
    - **A) Extensibilidade mínima (recomendada):** filtro por endpoint, política explícita de resultado/status e metadados completos; deferir alternate/composite find, upload e streaming.
    - **B) Pacote ampliado:** incluir também `Accepted`/202, find por chave alternativa/composta e binding/form/file/stream assistido.
  - **Impacto se não decidir:** Fase 10 fica limitada ao documento de design e backlog, sem nova API pública.
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
- **DF12 — `RoyalCode.Extensions.SourceGenerator` evolui junto:** a lib base de geração está fora da restrição de "bibliotecas externas". Correções e melhorias comuns — em especial a infraestrutura symbol-free exigida pela Fase 2 — devem ser feitas nela, e não duplicadas dentro do SmartCommands, mesmo que isso exija coordenar a evolução dos pacotes. O benefício esperado é compartilhar esses contratos com os demais generators. O consumo continua por `PackageReference` com versão pinada (`ExSrcGenVer`), então cada evolução da base exige uma release própria consumida aqui. Fonte: decisão do mantenedor. Ver `Stack de geração`.
- **DF13 — Validações adicionais:** adotar `[CommandValidation]` em métodos de instância, com propriedade `Order` opcional e valor padrão `10`; aceitar `Result`, `Task<Result>` e `ValueTask<Result>`, resolver parâmetros por DI/`WithParameter`/`CancellationToken` e executar depois de `HasProblems`, antes de UoW/retry. Validators com o mesmo `Order` não possuem ordem semanticamente relevante para o usuário; o generator desempata por assinatura totalmente qualificada apenas para produzir saída determinística. Fonte: Q1=A e definição do mantenedor.
- **DF14 — Exceções do adapter EF:** `DbContextAccessor.CompleteAsync` captura falhas apenas para tentar a limpeza transacional, preserva cancelamento e relança exceções inesperadas; `Result` representa somente sucesso ou problemas explicitamente conhecidos. Fonte: Q2=A e definição do mantenedor.
- **DF15 — Diagnósticos pertencem ao generator:** não criar `DiagnosticAnalyzer` separado. A leitura semântica ocorre uma vez; o pipeline transporta `DiagnosticInfo` equatável e symbol-free, e `Location`/`Diagnostic` são reconstruídos somente em `RegisterSourceOutput` por `ToDiagnostic(Func<string, DiagnosticDescriptor> descriptorResolver)`. Fonte: Q3=B, precedente do SmartSelector e definição do mantenedor.
- **DF16 — GitHub Actions permanece inalterado:** não criar nem modificar workflows nesta entrega. Build, testes, pack e consumer-smoke continuam como verificações locais reproduzíveis; os workflows existentes, se surgirem por trabalho externo, ficam fora do escopo. Fonte: Q5 e definição do mantenedor.
- **DF17 — `MapCreatedRoute` usa placeholders nomeados:** remover o contrato posicional `"{0}"`; casar placeholders como `"{id}"`, sem diferenciar maiúsculas, com propriedades declaradas por `nameof`, diagnosticando em compilação quantidade, nome, duplicação e propriedade incompatível. Fonte: Q6=A e definição do mantenedor.
- **DF18 — Tipo estrutural separado do uso:** `TypeSnapshot` contém apenas identidade, nulabilidade e forma semântica equatável. Papéis contextuais (`Entity`, `Context`, `HandlerParameter`, `CollectionOfEntities`) pertencem a `TypeUsageSnapshot`, usado pelos snapshots de parâmetro/serviço. Políticas como Task/ValueTask, Result, CancellationToken e wrapping são derivadas no SmartCommands, não armazenadas como decisões no snapshot estrutural. Fonte: definição do mantenedor a partir da revisão arquitetural.
- **DF19 — `EquatableArray<T>` normaliza ausência:** `default(EquatableArray<T>)` e coleção vazia representam a mesma sequência sem itens, com igualdade e hash idênticos; ausência com significado de domínio deve ser modelada explicitamente fora da coleção. Fonte: definição do mantenedor a partir da revisão arquitetural.

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
- `CommandValidationAttribute`: marcador de método de instância com `Order` opcional, padrão `10`, conforme DF13; contrato detalhado na Fase 6, sem acesso implícito a entidade/UoW antes do carregamento.
- `IUnitOfWorkAccessor<T>.CompleteAsync`: mantém `Task<Result>` nesta entrega; falhas inesperadas são limpas e relançadas conforme DF14.
- Diagnósticos: RCCMD000-RCCMD025 mantêm IDs; novos IDs começam em RCCMD026; texto pode ser corrigido diretamente, e cada ID recebe documentação no repositório.
- Hint names: usar nome de metadata totalmente qualificado, sanitizado e com sufixo estável quando necessário; colisões são detectadas antes de `AddSource`.

### Modelo, dados e persistência

`GenerationCandidate<T>`, `EquatableArray<T>` e os snapshots de tipo/parâmetro/serviço vêm de
`RoyalCode.Extensions.SourceGenerator` 0.4.0 (DF12); os modelos abaixo são específicos do SmartCommands e se apoiam
neles.

```text
GenerationCandidate<TModel>            (base)
  Model TModel?                         somente DTO equatável
  IsValid bool                          erro impede todas as saídas dependentes

EquatableArray<T>                       (base)
  Items immutable array                 igualdade/hash por conteúdo e ordem; default == vazio

TypeSnapshot                            (base) substitui TypeDescriptor nos modelos
  Name, MetadataName, Namespace, nulabilidade, Declaration
  forma intrínseca: named/array, elemento, argumentos genéricos, original definition, value/reference/void

TypeUsageSnapshot                       (base) associa o tipo ao papel naquele parâmetro/serviço
  Type TypeSnapshot
  Roles TypeUsageRoles                  Entity|Context|HandlerParameter|CollectionOfEntities

ParameterSnapshot / ServiceTypeSnapshot (base)
  TypeUsage TypeUsageSnapshot           nunca grava o papel no TypeSnapshot estrutural

DiagnosticInfo                          (base, conforme DF15)
  Id, argumentos string, FilePath + TextSpan + LinePositionSpan
  ToDiagnostic(descriptorResolver)      resolver pertence ao generator consumidor

ReturnModel                             (SmartCommands)
  DeclaredType TypeSnapshot
  AsyncKind None|Task|ValueTask
  PayloadType TypeSnapshot?
  ResultKind None|Result|ResultOfT       política derivada no transform, não no snapshot base

CommandModel
  CommandType TypeSnapshot
  CommandMethod MethodRef               nome e ReturnModel
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

- Não armazenar `ISymbol`, `SyntaxNode`, `Location`, `SemanticModel`, `Diagnostic`, `List<T>` mutável ou back-reference entre `CommandModel` e `EndpointModel` após a transformação. Isso vale também para os **descritores** da base (`TypeDescriptor` e afins): eles retêm `ISymbol` e vivem só dentro do `transform`.
- Representar tipos por metadata name/namespace/nullable/shape suficientes para emissão; manter papéis em `TypeUsageSnapshot`; derivar políticas de retorno no SmartCommands; comparar coleções por conteúdo e normalizar `default` para vazio.
- O adapter EF inicia, salva, commit/rollback e preserva a exceção conforme DF14; cancelamento nunca vira sucesso nem problema comum.

### Arquitetura alvo

```text
RoyalCode.SmartCommands.Generators/
  Analysis/
    *FactsReader.cs                     leitura semântica de AttributeData/ISymbol
    *Rules.cs                           regras puras compartilhadas
    ReservedIdentifiers.cs             nomes gerados por escopo
  Models/
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
  Diagnostics/                          diagnóstico, localização e severidade
  Incremental/                          cache/reexecução/determinismo
  Generation/                           fontes geradas e compilação da saída
  Runtime/                              mediator, retry e validação
  Packaging/                            conteúdo e consumer-smoke
```

### Pipeline alvo do handler

```text
HasProblems (quando WithValidateModel)
  -> validações adicionais ordenadas (DF13; uma vez)
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
| Created | placeholders `{0}` e propriedades por string | DF17: placeholders nomeados, validação completa e `Location` determinística | `CreatedAtRoute` por endpoint name |
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
3. **Fase 3 (diagnósticos)** — torna entradas inválidas seguras e observáveis sem repetir a análise semântica.
4. **Fase 4 (semântica/emissão)** — elimina parsing textual e colisões internas.
5. **Fase 5 (binding/EditEntity)** — corrige contratos já decididos sobre a base semântica.
6. **Fase 6 (validações)** — introduz DF13 após estabilizar o pipeline.
7. **Fase 7 (EF)** — aplica DF14 com testes de transação/cancelamento.
8. **Fase 8 (runtime/WorkContext)** — corrige estado do mediator e coerência do retry.
9. **Fase 9 (Minimal API atual)** — fecha bugs e completude dos mapeamentos existentes.
10. **Fase 10 (novos maps)** — implementa somente o pacote selecionado em Q4.
11. **Fase 11 (qualidade/distribuição)** — documentação, typos, pacote, TFM e verificações locais; GitHub Actions permanece como está.
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

**Depende de:** DF1-DF19; somente Q4 permanece necessária para desbloquear a Fase 10.

**Escopo:** solução inteira em modo somente leitura, documentação do plano e novos testes de caracterização sem alterar contratos.

**O que/como:** registrar SHA/status, executar baseline sem aceitar novos warnings, catalogar fontes geradas/hint names/diagnósticos e obter as respostas humanas. Não normalizar arquivos já modificados pelo usuário.

**Tarefas:**

- [x] Registrar no `Resultado da Fase 1` o commit, `git status --short`, SDKs instalados e alterações preexistentes a preservar.
- [x] Executar build/test padrão e registrar contagem por projeto, erros e warnings distintos.
- [x] Criar testes de caracterização que reproduzam as falhas de igualdade, null collection, múltiplos `[Command]`, múltiplos maps e entrada malformada sem ainda redesenhar a implementação.
- [x] Inventariar arquivos/hint names gerados por cada cenário para detectar mudanças não intencionais nas fases seguintes.
- [x] Registrar os comandos `rg`/PowerShell usados no inventário textual de descritores e membros, distinguindo ocorrências de dependências únicas.
- [x] Consolidar Q1-Q3, Q5 e Q6 como DF13-DF17 e atualizar as fases dependentes.
- [x] Responder Q4 ou manter a Fase 10 explicitamente bloqueada, limitada a design/backlog. (Fase 10 mantida explicitamente bloqueada; Q4 permanece Aberta — ver `Resultado da Fase 1`.)

**Critérios de aceite:** baseline reproduzível registrado; nenhuma alteração do usuário perdida; Q4 possui resposta fechada ou a Fase 10 permanece explicitamente bloqueada; testes de caracterização falham somente pelos bugs que pretendem capturar.

**Testes:** build/test padrão; `git diff --check`; `dotnet --info`; filtro xUnit dos novos testes de caracterização.

### Resultado da Fase 1

Executada em 2026-07-15. Solução tratada em modo somente leitura; as únicas escritas foram este plano e os
novos testes de caracterização.

#### 1. Baseline (commit, worktree, SDKs)

- **Commit:** `bbb5b38dcec0b8708496df4bd25a11091a357c85` (`bbb5b38`), branch `main`.
- **`git status --short`:** limpo antes da execução — **nenhuma alteração preexistente do usuário a preservar**
  (o estado descrito no Contexto para 2026-07-13 já havia sido consolidado nos commits). `git diff --check`: limpo.
- **Escritas desta fase (worktree após execução):** somente `RoyalCode.SmartCommands.Tests/Characterization/`
  (2 arquivos novos) e este arquivo de plano.
- **SDK ativo:** .NET SDK `10.0.301` (sem `global.json`). **SDKs instalados:** `8.0.422`, `9.0.100`, `10.0.301`.
  **Runtimes:** `Microsoft.NETCore.App`, `Microsoft.AspNetCore.App` e `Microsoft.WindowsDesktop.App` em `8.0.28`,
  `9.0.0`, `10.0.9`. **Host:** `10.0.9`, x64.

#### 2. Build/test padrão

Comandos (a partir de `src/`): `dotnet restore SmartCommands.sln`; `dotnet build SmartCommands.sln -c Release
--no-restore`; `dotnet test <projeto> -c Release --no-build`.

| Verificação | Resultado |
|---|---|
| `dotnet build SmartCommands.sln -c Release` | **êxito** — 0 erros, **9 avisos, todos NU5104** (aceitos por DF10) |
| `RoyalCode.SmartCommands.Tests` (baseline, sem caracterização) | **87/87** aprovados |
| `RoyalCode.SmartCommands.Demo.Tests` | **68/68** aprovados |
| Baseline total | **155/155** aprovados |

- **Warnings distintos:** apenas `NU5104` ("versão estável com dependência de pré-versão"), originados de
  `RoyalCode.SmartCommands` (dependências `RoyalCode.SmartProblems` e `RoyalCode.SmartValidations` em
  `1.0.0-preview-7.0`) e de `RoyalCode.SmartCommands.EntityFramework` (`RoyalCode.SmartProblems.EntityFramework`),
  multiplicados pelos TFMs `net8.0;net9.0;net10.0`. Nenhum outro erro/warning. Confirma DF10.
- Os testes rodam somente em `net10.0` (lacuna de cobertura por TFM já registrada no Contexto).

#### 3. Testes de caracterização (novos — falham por design)

Pasta `RoyalCode.SmartCommands.Tests/Characterization/`, todos com `[Trait("Category", "Characterization")]`.
Cada teste afirma o **comportamento-alvo** e, por isso, **falha no baseline exatamente pelo bug que documenta**;
as Fases 2/3/4 devem torná-los verdes. Confirmado: `--filter "Category!=Characterization"` mantém **87/87**;
o projeto passa a **96 testes = 87 aprovados + 9 falhos esperados**.

`ModelEqualityCharacterizationTests` (igualdade/hash dos modelos do pipeline):

| Teste | Bug capturado | Falha observada |
|---|---|---|
| `MapApiHandlersInformation_TypedEquality_ShouldConsiderWithOpenApi` | `Equals` tipado ignora `WithOpenApi` | `Assert.NotEqual` → são iguais |
| `MapApiHandlersInformation_ObjectEquality_ShouldMatchSameType` | `Equals(object)` testa `AddHandlersServicesInformation` | `Assert.True` → falso |
| `FindInformation_TypedEquality_ShouldConsiderIdType` | `Equals`/`GetHashCode` ignoram `IdType` | `Assert.NotEqual` → são iguais |
| `MapCreatedInformation_Equality_ShouldCompareByContent` | `string[].Equals` (identidade de referência) | `Assert.Equal` → diferem |
| `MapResponseValuesInformation_Equality_ShouldCompareByContent` | `IList.Equals` (identidade de referência) | `Assert.Equal` → diferem |
| `MapInformation_EqualByContent_ShouldHaveEqualHashCode` | `Equals` por conteúdo vs `GetHashCode` por referência da coleção | `Assert.Equal(hash)` → diferem |

`GeneratorRobustnessCharacterizationTests` (robustez do generator, via `Util.Compile`):

| Teste | Alvo | Falha observada |
|---|---|---|
| `MultipleCommandMethods_InSameClass_ShouldNotCollideNorCrash` | DF8: dois `[Command]` → diagnóstico, sem colisão nem crash | **CS8785=1** (generator lança por hint name duplicado `I{Classe}Handler`) |
| `MultipleMapAttributes_OnSameClass_ShouldBeDiagnosed` | maps conflitantes (`[MapPost]`+`[MapPut]`) diagnosticados | nenhum RCCMD (a cadeia `if/else if` escolhe POST em silêncio) |
| `MalformedMapAttribute_ShouldNotCrashGenerator` | DF9: entrada malformada → RCCMD/nada, sem crash | **CS8785=1** (`[MapPost("x")]` com 1 arg atinge `Arguments[1]` não guardado) |

Os três testes também exigem um erro `RCCMD` e ausência das fontes relacionadas. Assim, não podem ficar verdes
apenas porque o generator deixou de lançar `CS8785` enquanto ainda escolhe silenciosamente uma entrada ambígua ou
emite parcialmente a entrada inválida. O id exato será fixado quando o catálogo correspondente for criado na Fase 3.

**Correção de premissa verificada nesta fase:** o plano previa `NullReferenceException` em `MapInformation.Equals`
com `AuthorizationPolicies` nula. Empiricamente, **não lança**: o `SequenceEqual` em escopo no projeto do generator
(vindo de `RoyalCode.Extensions.SourceGenerator`, já que o arquivo não importa `System.Linq`) **tolera `null`**
(both-null → `true`). O defeito real e demonstrável de `MapInformation` sobre coleções é a **inconsistência
Equals/GetHashCode**: `Equals` compara `AuthorizationPolicies` por conteúdo, mas `GetHashCode` usa a **identidade de
referência** do array (`AuthorizationPolicies?.GetHashCode()`). O teste foi ajustado para capturar esse defeito real.

#### 4. Inventário de arquivos/hint names gerados (baseline para diff futuro)

Método reprodutível: `dotnet build SmartCommands.sln -c Release --no-incremental -p:EmitCompilerGeneratedFiles=true`
e enumeração de `**/obj/Release/**/generated/RoyalCode.SmartCommands.Generators/**/*.g.cs`.

- **Convenção de hint names por comando** (verificada nos emitters e nos arquivos emitidos):
  `I{Classe}Handler.g.cs` (interface), `{Classe}Handler.g.cs` (implementação, namespace `.Internals`) e
  `{Classe}_WasValidated.g.cs` (partial, quando há `HasProblems` + `partial`). POCO de resposta (`MapResponseValues`):
  `{Classe}Response.g.cs`. Hosts `AddServices`/`MapApiHandlers`/`MapFind`/`MapSearch` emitem por host/grupo.
- **Emissão em disco no estado atual** (poucos consumidores têm `[Command]`):
  - `RoyalCode.SmartCommands.Tests.Models` → `ICriarProdutoHandler.g.cs`, `CriarProdutoHandler.g.cs`,
    `CriarProduto_WasValidated.g.cs`.
  - `RoyalCode.SmartCommands.Demo.Seguranca` → `ICriarUsuarioHandler.g.cs`, `CriarUsuarioHandler.g.cs`,
    `CriarUsuario_WasValidated.g.cs`.
  - Demais módulos `Demo.*` → 0 arquivos do SmartCommands (o `.g.cs` em `Demo.Tests` é do gerador OpenAPI do
    ASP.NET, não deste). `RoyalCode.SmartCommands.Tests` referencia o generator como **biblioteca** (via
    `InternalsVisibleTo`), não como analyzer ativo, e exercita os cenários `Scenarios/{As..Is}` **em memória** por
    `Util.Compile`/`CSharpGeneratorDriver` — validados por comparação de snapshot nas suítes existentes.

#### 5. Comandos do inventário textual de descritores/membros (ocorrências ≠ dependências únicas)

Os números abaixo são **ocorrências textuais** (uma dependência lógica aparece muitas vezes; há matches em
comentários). **Não representam dependências únicas.** Escopo: projeto `RoyalCode.SmartCommands.Generators`
(o `.gitignore` exclui `obj/`). Reexecutar em 2026-07-15 confirmou que os valores **derivam** do estado do código
e divergem do snapshot de 2026-07-14 do Contexto — o que reforça o caráter volátil da contagem.

- **rg (Git Bash):** `rg -o "\bTypeDescriptor\b" -g "*.cs" | wc -l` (trocar o termo por descritor/membro/hint).
  Para métodos: `rg -o "\.MarkAs[A-Za-z]+" -g "*.cs" | wc -l`; para `.Symbol`: `rg -o "\.Symbol\b" -g "*.cs" | wc -l`.
- **PowerShell equivalente:**
  `(Get-ChildItem -Recurse -Filter *.cs | Select-String -Pattern '\bTypeDescriptor\b' -AllMatches | ForEach-Object { $_.Matches } | Measure-Object).Count`.

Contagens em 2026-07-15 (Generators): **Descritores** — `TypeDescriptor` 75, `ParameterDescriptor` 42,
`ServiceTypeDescriptor` 11, `PropertyDescriptor` 7, `EditTypeDescriptor` 5, `IdPropertyBoundToEntityParameter` 4.
**Emitters** — `ClassGenerator` 10, `MethodGenerator` 14, `ValueNode` 8, `FieldGenerator` 6, `ConstructorGenerator` 1,
`InterfaceGenerator` 0. **Hints** — `IsEntity` 4, `IsContext` 3, `IsHandlerParameter` 3, `IsCollectionOfEntities` 5,
`MarkAs*` 5. **Pipeline** — `ForAttributeWithMetadataName` 5, `.Collect()` 4. **`.Symbol` 0** (confirma que a emissão
não acessa símbolo — premissa central da Fase 2).

#### 6. Q4 e Fase 10

Q4 (primeiro pacote de novas capacidades HTTP) **permanece Aberta**. Por decisão de escopo, a **Fase 10 fica
explicitamente bloqueada**, limitada a documento de design e backlog, sem nova API pública — o que **satisfaz o
critério de aceite** ("Q4 fechada ou Fase 10 explicitamente bloqueada"). A decisão será oferecida ao mantenedor
(recomendação do plano: opção A — extensibilidade mínima); ao ser respondida, vira DF e destrava a Fase 10.

#### Critérios de aceite — situação

- Baseline reprodutível registrado (commit/status/SDKs/build/test). ✔
- Nenhuma alteração do usuário perdida (worktree limpo antes; só caracterização + plano depois). ✔
- Q4 fechada **ou** Fase 10 explicitamente bloqueada → **Fase 10 bloqueada**. ✔
- Testes de caracterização falham **somente** pelos bugs-alvo (9/9 verificados individualmente). ✔

**Verificações executadas:** `git rev-parse HEAD`, `git status --short`, `git diff --check`, `dotnet --info`;
`dotnet build SmartCommands.sln -c Release` (9 NU5104, 0 erros); `dotnet test RoyalCode.SmartCommands.Tests` (96:
87 aprovados + 9 caracterização falhos; `Category!=Characterization` → 87/87); `dotnet test
RoyalCode.SmartCommands.Demo.Tests` (68/68); build com `-p:EmitCompilerGeneratedFiles=true` para o inventário;
contagens `rg`.

---

## Fase 2 - Modelos incrementais e isolamento de entradas inválidas

**Depende de:** Fase 1, DF12, DF15, DF18 e DF19.

**Escopo:** `RoyalCode.Extensions.SourceGenerator` (release 0.4.0, ver `Stack de geração`), `Generators/IncrementalGenerator.cs`, `Generators/*Information.cs`, novas pastas `Models`/`Pipelines`, utilitário de testes do generator.

**O que/como:** a base já separa modelo de trabalho (descritores, com símbolo) de modelo de pipeline (snapshots, symbol-free); esta fase leva o SmartCommands para o lado certo dessa fronteira, em vez de recriar a infraestrutura. Primeiro estende a base com o que falta (0.4.0), depois substitui modelos mutáveis por DTOs equatáveis, encapsula coleções, separa candidate válido de erro, impede modelos inválidos nas combinações de DI/map e reduz `Collect()` ao que exige agregação. O SmartSelector já fez essa migração e serve de roteiro.

**Tarefas:**

- [x] **(Base 0.4.0)** Adicionar `EquatableArray<T>` ao `RoyalCode.Extensions.SourceGenerator` (`netstandard2.0`), normalizando `default` para vazio e testando igualdade/hash por conteúdo, ordem e `default == Empty`. Não criar uma cópia local.
- [x] **(Base 0.4.0)** Estender `TypeSnapshot` apenas com fatos estruturais symbol-free: metadata name/namespace, nulabilidade, named/array, elemento, argumentos genéricos, original definition e fatos value/reference/void. Helpers de política como `MustBeTask()` não entram no snapshot base.
- [x] **(Base 0.4.0)** Criar `TypeUsageSnapshot`/`TypeUsageRoles` e fazer os snapshots symbol-free de `ParameterDescriptor`, `ServiceTypeDescriptor`, `EditTypeDescriptor` e `IdPropertyBoundToEntityParameter` referenciarem o uso apropriado; `IsEntity`, `IsContext`, `IsHandlerParameter` e `IsCollectionOfEntities` nunca alteram o `TypeSnapshot` estrutural.
- [x] **(Base 0.4.0)** Adicionar `GenerationCandidate<T>` e `DiagnosticInfo` neutro/reutilizável com `ToDiagnostic(Func<string, DiagnosticDescriptor> descriptorResolver)`; não referenciar `AnalyzerDiagnostics` específico.
- [x] **(Base 0.4.0)** Gerar o `.nupkg`, testar Utils e SmartSelector contra a nova base e inspecionar o pacote. **Pack + testes Utils (80/80) + cross-test SmartSelector (0 regressões) + inspeção concluídos.** Publicação, notas de quebra e atualização de `ExSrcGenVer` **permanecem no gate manual do mantenedor** (não executados). Ver `Resultado da Fase 2 (parcial)`.
- [ ] Modelar command/find/search/map host/add-services sobre os snapshots da base, sem `Diagnostic`, `Location`, syntax, symbol, listas mutáveis ou ciclos. Verificado: a emissão atual não acessa `.Symbol`, portanto a migração é delimitada e possui baixo risco semântico na emissão. **(Bloqueado: exige a 0.4.0 consumida via `ExSrcGenVer` — gate.)**
- [ ] Criar no SmartCommands classificadores puros: `ReturnModel` deriva Task/ValueTask, payload, Result e wrapping; o classificador de parâmetros reconhece `CancellationToken` e demais papéis. Ambos usam os fatos do `TypeSnapshot`, sem parsing de strings e sem transferir política para a base. **(Bloqueado pelo gate.)**
- [ ] Remover `MapInformation.CommandInfo` e construir `EndpointModel` completo na transformação do comando. **(Bloqueado pelo gate.)**
- [ ] Corrigir por substituição os bugs de igualdade de `MapApiHandlersInformation`, `MapInformation`, `FindInformation`, `MapCreatedInformation` e `MapResponseValuesInformation`. **(Bloqueado pelo gate; capturados pelos testes de caracterização da Fase 1.)**
- [ ] Filtrar `GenerationCandidate.IsValid` antes das pipelines de handler, DI e endpoint. **(Bloqueado pelo gate.)**
- [ ] Ordenar agregações por metadata name/endpoint name antes da emissão para saída determinística. **(Bloqueado pelo gate.)**
- [ ] Manter saída por comando independente; agregar somente registros DI e endpoints por host/grupo. **(Bloqueado pelo gate.)**
- [ ] Honrar `CancellationToken` em todas as transformações e seleções incrementais. **(Bloqueado pelo gate.)**
- [x] Estender o test host para reutilizar `GeneratorDriver` e habilitar tracked steps. (`Util.CreateTrackedDriver`/`Util.RunTracked`; tracking names em `IncrementalGenerator`.)
- [x] Criar teste estrutural recursivo que falha se qualquer modelo retido pelo pipeline alcançar `ISymbol`, `SyntaxNode`, `Compilation`, `SemanticModel`, `Location` ou `Diagnostic`. (`Incremental/PipelineRetentionTests` — RED até a migração dos modelos.)

**Critérios de aceite:** editar arquivo não relacionado produz `Cached`/`Unchanged` nos passos e fontes não afetados; alterar `WithOpenApi`, `IdType`, policy ou item de coleção invalida exatamente as saídas dependentes; entrada inválida não chega a nenhum emitter; igualdade e hash obedecem o mesmo conjunto de campos; `default(EquatableArray<T>)` equivale a vazio; alterar somente o papel invalida `TypeUsageSnapshot`, mas não altera a igualdade do `TypeSnapshot` estrutural.

**Testes:** `dotnet test ... --filter "FullyQualifiedName~Incremental"`; rodar o mesmo driver duas vezes, depois com trivia/arquivo irrelevante e depois com alteração semântica; compilar toda saída e afirmar ausência de `CS8785`.

### Resultado da Fase 2 (parcial)

Executado em 2026-07-15. Entregue a **base 0.4.0** completa e o **test host incremental** do SmartCommands; a
**migração dos modelos `*Information` para snapshots permanece bloqueada** pelo gate manual (exige a 0.4.0 publicada
e consumida via `ExSrcGenVer`, o que o plano não faz automaticamente).

#### Base `RoyalCode.Extensions.SourceGenerator` 0.4.0 (repo `Utils`)

Todas as adições são **aditivas** (nenhuma assinatura pública existente mudou). Arquivos novos e alterados:

- `Collections/EquatableArray.cs` — struct imutável com igualdade/hash por conteúdo e ordem; o construtor copia
  arrays recebidos, e `default`, vazio e `null` colapsam na mesma sequência vazia (DF19), com hash `0`.
- `Generation/GenerationCandidate.cs` — `GenerationCandidate<TModel>` (modelo opcional + `EquatableArray<DiagnosticInfo>`
  + `IsValid`), com `Valid`/`Invalid`; modelo nulo sem diagnóstico representa descarte silencioso de entrada
  incompleta/não aplicável, enquanto o overload de diagnóstico único rejeita `null`.
- `Diagnostics/DiagnosticInfo.cs` — DTO symbol-free (id, argumentos, `FilePath`+`TextSpan`+`LinePositionSpan`) com
  `ToDiagnostic(Func<string, DiagnosticDescriptor> resolver)` (DF15; sem catálogo específico).
- `Descriptors/Snapshots/TypeUsageSnapshot.cs` — `TypeUsageRoles` (`[Flags]`: Entity/Context/HandlerParameter/
  CollectionOfEntities) + `TypeUsageSnapshot` (estrutura + papéis), com `CreateFromHints` lendo os hints do descritor.
- `Descriptors/Snapshots/DescriptorSnapshots.cs` — `ParameterSnapshot`, `ServiceTypeSnapshot`, `EditTypeSnapshot`,
  `IdPropertyBindingSnapshot` (símbolo-free; parâmetros carregam papéis via `TypeUsageSnapshot`; `ServiceTypeSnapshot`
  usa `TypeSnapshot` estrutural pois registro DI não tem papel).
- `Descriptors/Snapshots/MatchSelectionSnapshot.cs` — `TypeSnapshot` **estendido** (aditivo) com `IsVoid`,
  `IsNamedType`, rank/elemento de array, `ContainingType`, argumentos genéricos, identidade metadata curta e
  totalmente qualificada e `HasCompleteShape`. Arrays multidimensionais/jagged, tipos aninhados genéricos e tipos
  homônimos de namespaces distintos possuem forma/igualdade estrutural; descritores sem símbolo são explicitamente
  incompletos, sem parsing heurístico. Políticas (Task/Result/wrapping) ficam no SmartCommands por DF18.
- `csproj` — `Ver` 0.3.0 → **0.4.0**.

Verificações: **`RoyalCode.Extensions.SourceGenerator.Tests` = 97/97** (incluindo imutabilidade defensiva,
contratos nulos, formas estruturais difíceis e walker symbol-free). `.nupkg` 0.4.0 gerado e inspecionado (XML doc lista `EquatableArray\`1`,
`GenerationCandidate\`1`, `DiagnosticInfo`, `TypeUsageSnapshot`).

**Cross-test SmartSelector (task 5, pré-gate):** publicado o 0.4.0 num feed local temporário e apontado
`ExtSrcGenVer` para 0.4.0 apenas para teste. Resultado **idêntico** em 0.3.0 e 0.4.0: **69 aprovados / 18 falhos**
(as 18 falhas são pré-existentes no repo SmartSelector, em desenvolvimento na 0.5.1, sem relação com a base).
**Zero regressões** introduzidas pela 0.4.0. Artefatos temporários (feed local, `nuget.config`, bump de
`ExtSrcGenVer`) revertidos; SmartSelector ficou com apenas suas alterações de usuário pré-existentes.

**Não executado (gate do mantenedor):** publicar 0.4.0, registrar notas de quebra da base e atualizar `ExSrcGenVer`
no SmartCommands. Enquanto isso, o SmartCommands continua consumindo a 0.3.0 e a migração dos modelos fica parada.

#### Test host incremental do SmartCommands (não depende do gate)

- `Generators/IncrementalGenerator.cs` — `WithTrackingName` nas transformações, `Collect` e `Combine` que retêm
  modelos; a lista central `RetainedModelSteps` delimita as fronteiras auditadas. Mudança aditiva, saída idêntica.
- `Tests/Util.cs` — `CreateCompilation`, `CreateTrackedDriver` (com `trackIncrementalGeneratorSteps: true`) e
  `RunTracked` (reuso de `GeneratorDriver`).
- `Tests/Incremental/PipelineRetentionTests.cs` — gate estrutural que percorre somente as fronteiras de modelo
  nomeadas (exclui passos internos do Roslyn, que legitimamente carregam syntax/compilation) e falha se elas retiverem
  `ISymbol`/`SyntaxNode`/`SemanticModel`/`Compilation`/`Location`/`Diagnostic`. **RED no baseline** ao encontrar o
  `ISymbol` real de `RoyalCode.SmartProblems.Result` via `TypeDescriptor`; a migração deve torná-lo verde.

Verificações desta revisão: solução em Release **0 erros / 0 warnings**; `RoyalCode.SmartCommands.Tests` = **97 (87 aprovados +
10 falhos esperados**: 9 caracterização da Fase 1 + 1 retenção); `Category!=Characterization` → **87/87**;
`RoyalCode.SmartCommands.Demo.Tests` = **68/68** (mudança de tracking name não altera geração).

#### Próximo passo (após o gate)

Publicar a base 0.4.0, atualizar `ExSrcGenVer`, e então migrar `*Information` → modelos/snapshots symbol-free,
corrigir os bugs de igualdade (capturados na Fase 1), aplicar `GenerationCandidate.IsValid`/ordenação determinística
e virar verdes o `PipelineRetentionTests` e os testes de igualdade.

---

## Fase 3 - Diagnósticos semânticos e catálogo

**Depende de:** Fase 2, DF5, DF8, DF9 e DF15.

**Escopo:** `CmdDiagnostics.cs`, facts/rules semânticas do generator, `DiagnosticInfo` da base, `AnalyzerReleases.*.md`, testes Diagnostics/Generation e documentação de diagnósticos.

**O que/como:** centralizar regras semânticas no único transform; produzir `DiagnosticInfo` equatável e symbol-free; reportar uma ocorrência por causa em `RegisterSourceOutput`; localizar atributo/argumento/parâmetro responsável; bloquear geração relacionada sem lançar exceção ou executar uma segunda análise.

**Tarefas:**

- [ ] Implementar DF15 sem `DiagnosticAnalyzer` separado: ler a semântica uma vez, transportar somente `DiagnosticInfo` e chamar `ToDiagnostic(CmdDiagnostics.Get)` para reconstruir `Location`/`Diagnostic` na saída.
- [ ] Manter um único catálogo RCCMD no SmartCommands, expor `CmdDiagnostics.Get(string id)` ao resolver usado por `ToDiagnostic` e testar que todo ID produzido existe no catálogo.
- [ ] Corrigir diretamente `Attribte` e demais typos nos descritores existentes, preservando IDs RCCMD000-RCCMD025.
- [ ] Definir RCCMD026+ para colisão reservada, múltiplos commands, declaração não suportada, rota `EditEntity` ambígua/inexistente, maps conflitantes, endpoint/hint duplicado, validation inválida, binding inválido e `MapCreatedRoute` inconsistente.
- [ ] Diagnosticar classe nested/genérica/inacessível, método static/abstract/genérico/inacessível e combinações que gerariam C# inválido.
- [ ] Diagnosticar múltiplos atributos `Map*`, múltiplos hosts `[MapApiHandlers]`, endpoint names duplicados e group names que normalizam para a mesma classe.
- [ ] Diagnosticar identificadores reservados no escopo real de emissão; não rejeitar propriedade que não colide.
- [ ] Atualizar `AnalyzerReleases.Unshipped.md` e substituir links `google.com` por páginas locais/reais de cada regra.
- [ ] Testar ID, severidade, mensagem, argumentos, localização e ausência de fonte relacionada para cada erro.
- [ ] Garantir que código incompleto durante digitação não cause exceção; reportar somente quando houver informação suficiente.

**Critérios de aceite:** nenhuma fixture negativa produz `CS8785` ou stack trace; todos os novos RCCMD constam no catálogo único e em `AnalyzerReleases.Unshipped.md`; nenhum DTO retém `Diagnostic`/`Location`; colisões listadas em DF5 são erros; input válido não recebe falso positivo.

**Testes:** `dotnet test ... --filter "FullyQualifiedName~Diagnostics|FullyQualifiedName~Generation"`; compilações negativas para cada regra; teste de completude do catálogo; `dotnet build RoyalCode.SmartCommands.Generators/... -c Release` com extended analyzer rules.

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
- [ ] Detectar `Task`, `Task<T>`, `ValueTask` e `ValueTask<T>` semanticamente no transform e materializar `ReturnModel`; não usar modificador `async` como contrato de retorno nem reter símbolos.
- [ ] Detectar `Result`/`Result<T>`, entidades, collections, context e `CancellationToken` por símbolo no transform; congelar os fatos estruturais em `TypeSnapshot` e os papéis contextuais em `TypeUsageSnapshot`, sem comparação por nome simples.
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

**Testes:** testes generation/diagnostics filtrados por `WithParameter|EditEntity`; testes de integração via `WebApplicationFactory`; build/test padrão.

### Resultado da Fase 5

*a preencher*

---

## Fase 6 - Validações adicionais do comando

**Depende de:** Fase 5 e DF13.

**Escopo:** novo(s) atributo(s) público(s), modelo/reader/emitter de validation, handler pipeline, DI, metadados de problemas, testes e documentação.

**O que/como:** implementar `[CommandValidation]` conforme DF13; validators retornam `Result` e encerram o handler em falha; async é determinado pelo retorno e sempre aguardado; parâmetros usam resolução compartilhada, com restrições explícitas.

**Tarefas:**

- [ ] Criar `CommandValidationAttribute` com XML docs, `[Conditional("COMPILE_TIME_ONLY")]` quando aplicável e propriedade `Order` opcional com valor padrão `10`.
- [ ] Descobrir validators semanticamente e ordenar por `Order`; para valores iguais, desempatar pela assinatura totalmente qualificada apenas para determinismo, sem prometer essa ordem como contrato observável ao usuário.
- [ ] Aceitar somente `Result`, `Task<Result>` e `ValueTask<Result>` e diagnosticar `void`, `async void`, `Result<T>`, tipos arbitrários, generic/ref/out/params e método inacessível.
- [ ] Resolver `CancellationToken`, dependências DI e parâmetros `[WithParameter]` usando o mesmo modelo do comando.
- [ ] Rejeitar entity/context/UoW em validação pré-carregamento; documentar uma fase pós-load como backlog separado.
- [ ] Mesclar dependências dos validators sem campos/ctor duplicados e diagnosticar mesmo nome com tipos diferentes ou nome reservado.
- [ ] Emitir `HasProblems` primeiro, depois validators, antes de Begin/retry; retornar imediatamente o primeiro `Result` com problemas.
- [ ] Agregar `[ProduceProblems]` dos validators à metadata HTTP e remover duplicatas por categoria.
- [ ] Testar múltiplos validators, sync/async, ordem, short-circuit, DI, parâmetro externo, cancelamento, decorators e retry.
- [ ] Atualizar `.docs/commands.md`, README e Demo com pelo menos um caso de validação assíncrona dependente de serviço.

**Critérios de aceite:** `[CommandValidation]` sem `Order` equivale a `Order = 10`; ordem exata `HasProblems -> validators -> UoW/retry`; validators com mesmo `Order` possuem desempate determinístico, mas nenhuma precedência pública entre si; validator com falha impede Begin/find/decorator/command/Complete; validator roda uma vez mesmo quando command sofre retry; não existe `async void`; cancelamento é observado; metadata lista problemas declarados.

**Testes:** snapshots e compilação de handler; testes runtime com fakes contadores; teste integração HTTP de erro/sucesso; build/test padrão.

### Resultado da Fase 6

*a preencher*

---

## Fase 7 - Confiabilidade do adapter Entity Framework

**Depende de:** Fase 1 e DF14.

**Escopo:** `RoyalCode.SmartCommands.EntityFramework`, testes novos do adapter e integração com handler/filtro HTTP.

**O que/como:** separar limpeza transacional de adaptação de erro; preservar cancelamento; testar transação real em SQLite; expor contexto tipado a subclasses de repository.

**Tarefas:**

- [ ] Implementar DF14 em `DbContextAccessor.CompleteAsync`: capturar somente para cleanup e relançar a exceção inesperada sem esconder a causa original.
- [ ] Tentar rollback quando save/commit falhar e existir transação iniciada pelo adapter; definir token de cleanup que não seja cancelado antes da tentativa.
- [ ] Preservar as duas falhas quando rollback também falhar, sem perder stack/causa da falha primária.
- [ ] Garantir que `OperationCanceledException` permaneça cancelamento e não seja convertido em `Result`/500 interno pelo adapter.
- [ ] Alterar `RepositoryAdapter<TEntity,TContext>` para disponibilizar `protected TContext Context` e usar o tipo concreto internamente.
- [ ] Criar projeto/suite de testes EF se necessário, com SQLite in-memory e doubles para save/commit/rollback.
- [ ] Testar uso direto do handler fora de HTTP e uso HTTP com o filtro/middleware do Demo, sem duplicar tratamento dentro do adapter.
- [ ] Documentar quais exceções são problemas esperados e quais atravessam a borda.

**Critérios de aceite:** sucesso salva e faz commit uma vez; falha tenta rollback e é relançada; cancelamento não vira sucesso/problema; DF14 é observável tanto fora quanto dentro de HTTP; subclasses projetam usando `Context` sem guardar o mesmo contexto novamente.

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

**Depende de:** Fases 3-6 e DF17.

**Escopo:** todos os atributos `Map*` atuais, host/grupo, emitters, Problem metadata, OpenAPI, generator tests e Demo.Tests.

**O que/como:** unificar command/find/search em `EndpointModel`, corrigir inconsistências e validar todo dado que participa da assinatura, rota, nome, resposta ou metadata.

**Tarefas:**

- [ ] Fazer zero/um/múltiplos `Map*` terem comportamento explícito; múltiplos geram RCCMD, nunca prioridade silenciosa por `else if`.
- [ ] Tornar `MapGroup` opcional de forma consistente ou diagnosticá-lo como obrigatório conforme contrato documentado; remover diagnóstico local atualmente descartado em Search.
- [ ] Validar endpoint/group names vazios, duplicados e colisões após `ToPascalCase`.
- [ ] Validar `MapIdResultValue`/`MapResponseValues`: retorno com valor, propriedade pública legível, tipo emitível, lista não vazia e nomes sem duplicata.
- [ ] Implementar DF17 em `MapCreatedRoute`: aceitar somente placeholders nomeados, casar nomes sem diferenciar maiúsculas e validar quantidade, duplicação e propriedades declaradas por `nameof`; gerar URI sem substituição textual posicional frágil.
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

**Depende de:** Fases 3-10 e DF16.

**Escopo:** estilo, docs, README, pack, analyzer package, smoke consumers e automação.

**O que/como:** corrigir typos/stale docs, alinhar package metadata, verificar conteúdo do `.nupkg`, adicionar testes por TFM e manter GitHub Actions inalterado conforme DF16.

**Tarefas:**

- [ ] Adicionar `.editorconfig` mínimo alinhado ao estilo verificado, sem reformatar em massa arquivos fora do diff.
- [ ] Corrigir `EnterpisePatterns` para `SmartCommands`, adicionar descrições reais por pacote e validar README/icon/license/repository no `.nupkg`.
- [ ] Verificar que o package do generator contém `RoyalCode.SmartCommands.Generators.dll` e dependência necessária em `analyzers/dotnet/cs`, sem assembly runtime indevido.
- [ ] Criar consumer-smoke que restaura os `.nupkg` locais e compila um command/map válido e um inválido em `net8.0`, `net9.0` e `net10.0`; incluir cenário que carrega SmartCommands e SmartSelector juntos para detectar conflito de versão da base/analyzer (`CS8032`, `CS8785`, `AD0001`).
- [ ] Corrigir `README.md` e `.docs/commands.md`, inclusive sintaxe genérica atual de `EditEntity`, validation adicional, binding e maps.
- [ ] Adicionar errata à revisão de 2026-07-13 conforme DF6, sem manter a conclusão falsa de `async void` como fato atual.
- [ ] Renomear `.docs/archtecture.md` para `.docs/architecture.md` e atualizar `SmartCommands.sln`/links; corrigir `SmartProbelms` e textos com encoding inválido.
- [ ] Executar busca de typos conhecidos e revisar XML docs de toda API nova/alterada.
- [ ] Não criar nem modificar `.github/workflows`; registrar e executar localmente os comandos reproduzíveis de build/test/pack/consumer-smoke conforme DF16.
- [ ] Manter allowlist somente de NU5104 e falhar em qualquer outro warning novo.

**Critérios de aceite:** package metadata aponta para o remote correto; consumer-smoke carrega generator/analyzer e compila em três TFMs; documentação não usa APIs antigas; busca de typos conhecidos retorna zero; comandos locais reproduzem build/test/pack sem alterar Actions; somente NU5104 conhecido permanece.

**Testes:** `dotnet pack` dos quatro pacotes em Release; inspeção do `.nupkg`; consumer-smoke net8/net9/net10, isolado e com SmartSelector; markdown/link check local; build/test padrão no ambiente local, sem alteração de GitHub Actions.

### Resultado da Fase 11

*a preencher*

---

## Fase 12 - Compatibilidade, regressão e preparação de release

**Depende de:** Fases 1-11 concluídas e Q4 fechada — por seleção de funcionalidades ou diferimento explícito da Fase 10.

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
| Generator incremental/determinístico | 1-4 | DF8, DF9, DF12, DF15, DF18, DF19 | cache seletivo; modelo por valor; nenhuma fonte inválida; nenhum `ISymbol` no pipeline; tipo separado do uso | tracked steps, equality, compilação da saída |
| Diagnósticos completos | 3-5, 9 | DF4, DF5, DF8, DF9, DF15 | RCCMD localizado; sem `CS8785`; catálogo completo e DTO symbol-free | Diagnostics/Generation negativos |
| Binding e EditEntity corretos | 5 | DF2-DF5 | inferência/atributos preservados; ambiguidade bloqueada | generator + WebApplicationFactory |
| Validações adicionais | 6 | DF2, DF5, DF13 | ordem/short-circuit/async/CT/DI definidos; `Order` padrão 10; empate sem precedência pública | snapshots + fakes + HTTP |
| Runtime EF/WorkContext/decorators confiável | 7-8 | DF5, DF6, DF14 | cancelamento e exceção corretos; retry/decorators determinísticos | SQLite, retry e decorator tests |
| Minimal API completa/evoluída | 9-10 | DF2-DF4, DF17, Q4 | maps atuais completos; placeholders nomeados; somente features aprovadas | HTTP matrix + OpenAPI |
| Qualidade/distribuição/compatibilidade | 11-12 | DF1, DF6, DF7, DF10-DF12, DF16 | docs/pacotes/TFMs/verificações locais verdes; Actions intactas | pack, consumer-smoke, build/test final |

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

- DF13-DF19 aplicadas e Q4 fechada por seleção de funcionalidades ou diferimento explícito.
- Nenhuma falha de igualdade, parsing textual inseguro ou back-reference listada no contexto permanece.
- Generator e regras de diagnóstico cobrem entradas válidas e inválidas sem crash, segunda análise semântica ou fonte parcial.
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
| ID de diagnóstico não existir no catálogo | `DiagnosticInfo` chega à saída com ID sem `DiagnosticDescriptor` correspondente | exceção ao reconstruir o diagnóstico e possível `CS8785` | catálogo único, resolver explícito e teste de completude de todos os IDs produzidos | Aberto |
| Igualdade esconder mudança real | tracked step retorna Cached após alterar campo relevante | fonte obsoleta no IDE/build incremental | testes campo a campo e hash/equals; rerun limpo versus incremental | Aberto |
| Base e consumidores dessincronizarem | Fase 2 depende de uma release da base (0.4.0), consumida por `PackageReference` pinado | SmartCommands bloqueado esperando pacote; SmartSelector quebrado por mudança da base | fechar a superfície da 0.4.0 antes de tocar os `*Information`; rodar a suíte do SmartSelector contra a base nova antes de publicar | Aberto |
| Generators carregarem versões incompatíveis da base | SmartCommands e SmartSelector empacotam versões/pastas Roslyn diferentes de `RoyalCode.Extensions.SourceGenerator.dll` | `CS8032`, `CS8785`, `AD0001` ou um generator deixa de carregar | alinhar versões e validar consumer-smoke com os dois `.nupkg` no mesmo projeto em todos os TFMs | Aberto |
| Refactor incremental alterar todos os hints | diff massivo/duplicado em generated files | revisão difícil e colisão | inventário Fase 1, nomes FQN determinísticos e migração em fase única | Aberto |
| Binding inferido escolher body/DI inesperado | parâmetro complexo sem atributo em POST | endpoint inicia errado ou lê fonte incorreta | preservar binding explícito, diagnósticos e testes reais ASP.NET | Aberto |
| Validator causar efeito repetido | validator colocado dentro do retry | duplicação de consulta/efeito | invariante e teste contador com conflito forçado | Aberto |
| Mudança EF quebrar consumidor não HTTP | consumidor esperava exceção convertida em Result | breaking runtime | DF14, release notes e testes das duas bordas | Aberto |
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
- Publicação automática NuGet — destino: plano de release/segredos; DF16 mantém Actions inalteradas nesta entrega.

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
- `RoyalCode.Extensions.SourceGenerator` (repo Utils) — `Descriptors/`, `Descriptors/Snapshots/MatchSelectionSnapshot.cs` e `Generators/`; base da stack, evolui com este plano (DF12).
- `RoyalCode.SmartSelector.Generators` (repo SmartSelector) — `Models/DiagnosticInfo.cs`, `AnalyzerDiagnostics.cs` e os `*Information`; precedente de pipeline symbol-free e de diagnóstico equatável.
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
