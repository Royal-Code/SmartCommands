# Documentação da API SmartCommands (Commands, Handlers, Decorators, Mapeamento HTTP)

Esta documentação apresenta os conceitos, funcionalidades e exemplos práticos para usar a família de bibliotecas SmartCommands em projetos .NET.
Serve também como referência para ferramentas de IA (ex.: GitHub Copilot) compreenderem e gerarem código correto com base na API da biblioteca.

Projetos alvo: .NET 8, .NET 9 e .NET 10.

Escopo desta documentação:
- `RoyalCode.SmartCommands` (runtime): atributos e contratos para comandos e handlers.
- `RoyalCode.SmartCommands.EntityFramework`: adapters para EF Core (UnitOfWork/Repositories accessor).
- `RoyalCode.SmartCommands.WorkContext`: adapters para WorkContext (UnitOfWork/Repositories accessor e integração DI).
- `RoyalCode.SmartCommands.Generators` (analyzer .NET Standard 2.0): Source Generator que cria handlers e integrações a partir de atributos.

## 1. Introdução

SmartCommands padroniza a maneira de declarar comandos (métodos) em classes de aplicação e gerar automaticamente handlers fortemente tipados, com suporte a:
- Validação do modelo (`HasProblems` / SmartValidations) antes da execução.
- Unit of Work e acesso a repositórios (EF Core e WorkContext).
- Resolução e carregamento de entidades por ID, incluindo coleções.
- Composição via Decorators (pipeline executado antes/depois do comando).
- Mapeamento para endpoints HTTP (Minimal APIs) com metadados (rota, resumo, autorização) e respostas consistentes (Created/Ok/NoContent) integradas a SmartProblems.

Os handlers são gerados pelo Source Generator com base em atributos aplicados ao método do comando e à classe. O runtime expõe atributos leves usados apenas em tempo de compilação.

Benefícios principais:
- Reduz boilerplate de criação de handlers, DI e carregamento de entidades.
- Integra com EF Core e WorkContext via adapters.
- Fluxo de erros padronizado com `Result`/`Problems` (vide `.docs/problems.md`).
- Fácil composição com validações (`RuleSet`) e Decorators.

## 2. Pacotes e responsabilidades

- `RoyalCode.SmartCommands`:
  - `CommandAttribute`: marca o método como um comando elegível para geração.
  - `AddHandlersServicesAttribute`: marca a classe para registrar handlers decorados/serviços auxiliares (geração de DI).

- `RoyalCode.SmartCommands.EntityFramework`:
  - `CommandsDbContextExtensions.AddUnitOfWorkAccessor<TContext>()`: registra adapters `IUnitOfWorkAccessor<T>` e `IRepositoriesAccessor<T>` usando EF Core (`DbContext`).
  - Versão base/impl: `AddUnitOfWorkAccessor<TContextBase, TContextImpl>()`.

- `RoyalCode.SmartCommands.WorkContext`:
  - `CommandsWorkContextExtensions.AddUnitOfWorkAccessor<TDbContext>(IUnitOfWorkBuilder|IWorkContextBuilder)`: registra adapters para `IWorkContext` com `UnitOfWorkAccessor` e `RepositoryAdapter`.
  - `AddUnitOfWorkAccessor<TWorkContext>(IServiceCollection)`: registro direto por serviços.
  - `ConfigureWorkContextAdapterOptions(...)`: configuração de `WorkContextAdapterOptions`.

- `RoyalCode.SmartCommands.Generators`:
  - Gera interface `I{CommandClass}Handler` e implementação `{CommandClass}Handler` com base em atributos.
  - Aplica pipeline de validação, unidade de trabalho, carregamento de entidades (Find), Decorators e mapeamento HTTP.
  - Cria parcial `{CommandClass}_WasValidated.g.cs` para anotação de *null-state* quando houver metadados de validação.

## 3. Conceitos centrais

- Classe de Comando: classe que contém um método anotado com `CommandAttribute`. Opcionalmente parcial para habilitar geração de membros auxiliares.
- Método de Comando: implementa a lógica de domínio/aplicação; pode ser síncrono ou assíncrono. Pode retornar `void`, `Task`, `Result`, `Result<T>` ou `T`.
- Validação: método `HasProblems(out Problems?)` opcional na classe, integrado via atributo `WithValidateModel` (no método de comando) e gerando `WasValidated` quando aplicável.
- Validações adicionais: métodos de instância anotados com `CommandValidation` (com `Order` opcional, padrão `10`) que retornam `Result`, `Task<Result>` ou `ValueTask<Result>`; executam após `HasProblems` e antes da unidade de trabalho/retry, encerrando o handler no primeiro `Result` com problemas.
- Unit of Work / Repositórios: habilitado via `WithUnitOfWork<TContext>` (ou `WithDbContext`/`WithWorkContext`), expondo a infraestrutura por `IUnitOfWorkAccessor<T>` e `IRepositoriesAccessor<T>`. O contrato de exceções do adapter EF (`CompleteAsync`: sucesso/problema conhecido vs. exceção relançada, cancelamento preservado) está em `.docs/entity-framework.md`.
- Find Entities: vincula parâmetros de entidade/coleção às propriedades de ID da classe para carregamento automático antes do comando (`WithFindEntities<TContext>` ou implicitamente via UoW/Repo Accessor).
- Edit Entity: edita uma entidade existente informada pelo primeiro parâmetro do método e o ID passado ao handler (`EditEntity(entityType)`), com validação do tipo e binding.
- Produce New Entity: comando que cria uma nova entidade retornando `Result<T>`/`T` (o gerador valida tipos e retorna corretamente).
- Decorators: `WithDecorators` permite envolver a execução do comando com interceptores (pré/pós).
- Mapeamento HTTP: atributos `MapPost`/`MapPut`/`MapPatch`/`MapDelete`/`MapGet` na classe definem rota e nome do endpoint. Outros atributos agregam metadados: `WithDescription`, `WithSummary`, `WithAuthorization`, `WithPolicy`, `MapGroup`, `MapCreatedRoute`, `MapIdResultValue`, `MapResponseValues`.

Categorias de problemas (vide `.docs/problems.md`): `InvalidParameter`, `ValidationFailed`, `NotAllowed`, `InvalidState`, `NotFound`, `InternalServerError`, `CustomProblem`. Atributo `ProduceProblems(...)` nos métodos/validações lista categorias esperadas.

## 4. Fluxos e atributos suportados (overview funcional)

Principais atributos em métodos de comando:
- `Command`: marca o método como comando.
- `WithValidateModel`: aciona validação via `HasProblems` antes da execução.
- `WithDecorators`: compõe execução via `IDecorator<TCommand, TResult>`; requer retorno não-`void` (gerador valida).
- `WithUnitOfWork<TContext>`: injeta `IUnitOfWorkAccessor<TContext>` e habilita begin/complete no handler.
- `WithDbContext`: similar a UoW mas baseado em `DbContext` genérico.
- `WithWorkContext`: similar a UoW mas baseado em `IWorkContext`.
- `WithTransaction`: exige transação para o comando (o handler emite `BeginAsync(requireTransaction: true, ct)`), mesmo com a opção `BeginTransactions` do adapter desligada. Use quando o corpo produz mais de uma escrita commitada antes do complete (ex.: saves intermediários), para que falha ou retry de concorrência desfaçam o trabalho parcial. Exige um dos atributos de unidade de trabalho (senão RCCMD043); é somente opt-in — não desliga transações habilitadas pela opção do adapter.
- `WithRetryOnConcurrency`: reexecuta o corpo da unidade de trabalho em conflito otimista (suportado apenas com `WithWorkContext`). O problema de esgotamento passa sempre pela `IConcurrencyRetryProblemFactory`: com `Operation`, essa chave; sem `Operation`, a chave default `{namespace}.{Comando}` (`ConcurrencyRetryOperations.DefaultFor<TCommand>()`). As options `ExhaustedProblemDetail`/`ExhaustedProblemTypeId` valem em todos os caminhos, e `AddConcurrencyRetryProblem<TCommand>(factory)` (sem operation) registra pela mesma chave default.
- `WithFindEntities<TContext>`: habilita lookup de entidades por ID/coleções quando não há UoW.
- `ProduceNewEntity`: comando retorna entidade nova (gera fluxo de `Result<T>` quando aplicável).
- `EditEntity(entityType)`: comando edita entidade existente; primeiro parâmetro deve ser do tipo da entidade.
- `MapIdResultValue`: mapeia campo `Id` do valor retornado para rota/resposta.
- `MapResponseValues(params string[])`: seleciona propriedades do valor retornado para compor resposta.
- `ProduceProblems(params ProblemCategory[])`: declara problemas esperados.

Principais atributos na classe de comando (mapeamento HTTP):
- `MapPost(routePattern, endpointName)` e equivalentes para outros verbos. O endpoint name não pode ser vazio
  (RCCMD044) e deve ser único entre todos os endpoints mapeados (RCCMD030).
- `MapGroup(routePrefix)`: agrupa endpoints sob um prefixo de rota. **Opcional de forma consistente**: sem
  `MapGroup`, os endpoints são mapeados sem prefixo (`MapGroup("")`), na classe nomeada a partir do host
  (`Map{Host}Api`). O prefixo pode conter variáveis de rota (`lojas/{lojaId:int}`); o nome da classe gerada é
  normalizado a partir do prefixo (RCCMD045/RCCMD046 diagnosticam nomes inválidos ou colidentes).
- `MapCreatedRoute(routePattern, params string[] propertiesNames)`: constrói `Location` em 201 com
  **placeholders nomeados** (DF17) casados, sem diferenciar maiúsculas, com as propriedades declaradas
  (prefira `nameof`): `[MapCreatedRoute("{id}", nameof(Produto.Id))]`. Quantidade, nomes, duplicação e
  propriedade incompatível são validados em compilação (RCCMD050); o formato posicional `"{0}"` foi removido.
- `MapIdResultValue` **ou** `MapResponseValues(params string[])` moldam o corpo da resposta; os dois juntos
  são erro (RCCMD049) e as propriedades precisam ser públicas e legíveis (RCCMD048).
- `WithDescription(text)` / `WithSummary(text)`: metadados de documentação.
- `WithAuthorization` / `WithPolicy(params string[] policies)`: exigem autenticação/política.
- `WithTags(params string[] tags)`: tags OpenAPI na ordem declarada, para command/find/search maps; vazio ou
  whitespace é erro (RCCMD041).
- `WithEndpointFilter<TFilter>` (repetível): adiciona `AddEndpointFilter<TFilter>()` ao endpoint gerado, na
  ordem declarada, com o filtro ativado pela DI do ASP.NET Core; `TFilter` precisa implementar
  `IEndpointFilter` e ser uma classe concreta top-level (RCCMD051). Vale para command/find/search maps.
- `WithResultStatus(HttpResultStatus)`: seleção explícita do status de sucesso (`Ok`, `Created`, `NoContent`)
  para command maps; sem o atributo vale a inferência abaixo. `Created` sem `MapCreatedRoute` responde `201`
  sem `Location`; `NoContent` descarta deliberadamente o valor de sucesso de `Result<T>` (os problemas são
  sempre preservados). Conflitos são erro (RCCMD052/RCCMD053).

Status de sucesso por verbo (inferência, sem `WithResultStatus`):
- `MapGet`/`MapPost`/`MapPut`/`MapPatch`: `200 OK` com o valor do `Result` (ou `201 Created` com
  `MapCreatedRoute`).
- `MapDelete`: `204 No Content` somente quando o contrato não retorna valor nem created; quando o comando
  retorna valor, o endpoint responde `200 OK` com o valor — o valor nunca é descartado em silêncio (o
  descarte é opt-in via `WithResultStatus(NoContent)`).

Comandos que carregam entidades (`EditEntity` ou parâmetros-entidade) incluem `ProblemCategory.NotFound`
automaticamente na metadata declarada (`ProduceProblems`), refletindo o `404` real do runtime.

Capacidades diferidas: `Accepted`/202 e busca por chave alternativa/composta (`TryFindBy`) possuem plano
próprio (`.ai/plans/plan-smartcommands-accepted-tryfindby.md`); upload/form/file/stream permanecem no
mapeamento manual da Minimal API (DF23).

Validações automáticas do Generator:
- Proíbe generics na classe/método de comando.
- Valida retorno quando `WithDecorators` (não `void` puro).
- Incompatibilidades: `WithDbContext` não pode coexistir com `WithUnitOfWork`; `WithWorkContext` não pode coexistir com `WithUnitOfWork`/`WithDbContext`.
- `ProduceNewEntity` requer UoW.
- `EditEntity` requer UoW e primeiro parâmetro do tipo indicado.
- `CancellationToken` só em métodos assíncronos.
- Binding obrigatório para parâmetros de entidade/coleção às propriedades `Id`/`Ids` na classe.

## 5. Adapters e DI (EntityFramework e WorkContext)

- EF Core (`RoyalCode.SmartCommands.EntityFramework`):
  - Registro:
    - `services.AddUnitOfWorkAccessor<MyDbContext>();`
    - ou `services.AddUnitOfWorkAccessor<MyBaseContext, MyContextImpl>();`
  - Expõe `DbContextAccessor<TContext>` como implementação de `IUnitOfWorkAccessor<TContext>` e `IRepositoriesAccessor<TContext>`.

- WorkContext (`RoyalCode.SmartCommands.WorkContext`):
  - Registro em `IUnitOfWorkBuilder<TDbContext>` ou `IWorkContextBuilder<TDbContext>`:
    - `builder.AddUnitOfWorkAccessor<TDbContext>(opt => { /* WorkContextAdapterOptions */ });`
  - Registro direto:
    - `services.AddUnitOfWorkAccessor<IWorkContext>();` (ou genérico para tipo de `IWorkContext<TDbContext>`)
  - Configuração:
    - `services.ConfigureWorkContextAdapterOptions(opt => { /* opções */ });`

## 6. Exemplos reais de uso

### 6.1 Classe de comando com validação e UoW
```csharp
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

public partial class CreateProduct
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }

    // Validação de entrada
    public bool HasProblems(out Problems? problems)
    {
        return Rules.Set<CreateProduct>()
            .NotEmpty(Name)
            .GreaterThanOrEqual(Price, 0)
            .HasProblems(out problems);
    }

    // Método de comando
    [Command, WithValidateModel, WithUnitOfWork<MyDbContext>]
    public Result<Product> Execute()
    {
        var entity = new Product(Name, Price);
        // persistência via repositório pelo handler gerado
        return entity; // `ProduceNewEntity` não é necessário quando retorno é Result<T>
    }
}
```

O handler gerado irá:
- Validar `HasProblems` e retornar `Problems` se houver.
- Iniciar `UnitOfWork`, adicionar a entidade, salvar e retornar `Result<Product>`.

### 6.2 Editando entidade existente e carregando por ID
```csharp
public partial class UpdateProduct
{
    public int ProductId { get; set; }
    public string? Name { get; set; }

    // Validação de entrada (Name obrigatório e sem espaços)
    public bool HasProblems(out Problems? problems)
    {
        return Rules.Set<UpdateProduct>()
            .NullOrNotEmpty(Name)
            .NullOrNotMatches(Name, "\s")
            .HasProblems(out problems);
    }

    // O generator criará um método parcial WasValidated que marca membros como not-null quando aplicável
    // Chame WasValidated no início do comando para ajudar o analisador de null-state
    [Command, WithValidateModel, WithUnitOfWork<MyDbContext>, EditEntity(typeof(Product))]
    public Result Execute(Product product)
    {
        WasValidated();
        product.Rename(Name!);
        return Result.Ok();
    }
}
```

O handler requererá um parâmetro `productId` (injetado como primeiro parâmetro do handler), buscará `Product` pelo ID, tratará `NotFound` com `Problems`, executará `Execute(product)` e finalizará o UoW.

### 6.3 Decorators
```csharp
public partial class PublishOrder
{
    [Command, WithDecorators]
    public Result Execute()
    {
        // lógica principal
        return Result.Ok();
    }
}

public interface IDecorator<TCommand, TResult>
{
    Task<TResult> InvokeAsync(TCommand command, Func<TCommand, CancellationToken, Task<TResult>> next, CancellationToken ct);
}
```

Com `WithDecorators`, o handler criará um mediador que invoca `next` com pipeline de decorators registrados.

### 6.4 Mapeamento HTTP com Created
```csharp
[MapPost("/", "CreateProduct")]
[MapGroup("api/products")]
[WithSummary("Create product")]
[WithDescription("Creates a new product")]
[MapCreatedRoute("{id}", nameof(Product.Id))]
public partial class CreateProduct
{
    [Command, ProduceNewEntity, WithUnitOfWork<MyDbContext>]
    public Result<Product> Execute() { /* ... */ }
}
```

O placeholder nomeado `{id}` casa (sem diferenciar maiúsculas) com a propriedade declarada por `nameof`; a
`Location` gerada é `api/products/{produto.Id}` (o prefixo do grupo é prefixado quando presente). Quando a
resposta for `Result<Product>`, `MapIdResultValue` pode ser usado para expor o campo `Id` como corpo da
resposta, combinável com `MapCreatedRoute`.

### 6.5 Integração com WorkContext
```csharp
services.AddWorkContext<MyDbContext>()
    .ConfigureDbContextPool((sp,b) => b.UseSqlite("DataSource=:memory:"))
    .AddUnitOfWorkAccessor<MyDbContext>();
```

O handler gerado quando `WithWorkContext` estiver presente usará `IWorkContext` como accessor, mantendo semântica de UoW/Repos.

Módulo de configuração reutilizável:
```csharp
public static class MyModuleConfigureWorkContext
{
    public static IWorkContextBuilder<TDbContext> ConfigureMyModule<TDbContext>(this IWorkContextBuilder<TDbContext> builder)
        where TDbContext : DbContext
    {
        return builder.ConfigureModel(modelBuilder => modelBuilder.MapMyModule())
            .AddRepositories(typeof(MyModuleConfigureWorkContext).Assembly)
            .ConfigureSearches(typeof(MyModuleConfigureWorkContext).Assembly)
            .ConfigureCommands(typeof(MyModuleConfigureWorkContext).Assembly)
            .ConfigureQueries(typeof(MyModuleConfigureWorkContext).Assembly);
    }
}
```

### 6.6 Validações adicionais (`CommandValidation`)
```csharp
public class CreateSubscription
{
    public string? Email { get; set; }
    public string? Plan { get; set; }

    // roda primeiro (Order menor); síncrona
    [CommandValidation(Order = 5)]
    internal Result ValidatePlan(IPlanCatalog plans)
        => plans.Exists(Plan)
            ? Result.Ok()
            : Problems.InvalidParameter($"Plano '{Plan}' não existe.", nameof(Plan));

    // Order padrão 10; assíncrona (sempre aguardada), com serviço de DI e CancellationToken
    [CommandValidation]
    internal async Task<Result> ValidateEmailAsync(IEmailVerifier verifier, CancellationToken ct)
        => await verifier.IsValidAsync(Email, ct)
            ? Result.Ok()
            : Problems.InvalidParameter("E-mail inválido.", nameof(Email));

    [Command, WithUnitOfWork<MyDbContext>]
    public Subscription Execute() { /* ... */ }
}
```

Regras (DF13):
- Métodos de instância acessíveis (public/internal), não genéricos, sem `ref`/`out`/`in`/`params`.
- Retorno: `Result`, `Task<Result>` ou `ValueTask<Result>` — qualquer outro tipo é RCCMD038.
- Parâmetros usam o mesmo modelo do comando: serviços de DI (mesclados no construtor do handler, sem
  duplicatas), `[WithParameter]` (entra na assinatura do handler e no delegate HTTP, com bindings copiados)
  e `CancellationToken` (somente em validators assíncronos). Entidades, contextos e acessores não estão
  disponíveis nesta etapa (a validação roda antes de qualquer carregamento) — RCCMD039.
- O mesmo nome representa um único parâmetro lógico: deve manter o mesmo tipo e o mesmo papel em todos os
  métodos. Misturar dependência de DI e `[WithParameter]` para o mesmo nome é RCCMD042. Bindings explícitos
  idênticos são deduplicados; fontes diferentes para o mesmo nome são RCCMD033.
- Executam após `HasProblems`, antes de UoW/decorators/retry, **uma única vez** mesmo quando o comando é
  reexecutado por conflito de concorrência; o primeiro `Result` com problemas encerra o handler.
- `[ProduceProblems]` declarados nos validators são agregados à metadata HTTP do endpoint, sem duplicatas.
- Validators com o mesmo `Order` não têm precedência observável entre si (o desempate interno serve apenas
  ao determinismo da saída gerada).
- Limitação: apenas validators declarados na própria classe do comando são descobertos; métodos herdados de
  classes base não participam (declare os validators na classe do comando).

## 7. Comportamento do Handler Gerado (pipeline)

Ordem típica no método `{CommandClass}Handler.Handle(Async)`:
1) `WithValidateModel` → chama `command.HasProblems(...)` e retorna `Problems` se houver.
1.1) `CommandValidation` → invoca cada validator (ordenado por `Order`) e retorna os `Problems` do primeiro `Result` com falha.
2) `WithUnitOfWork`/`WithDbContext`/`WithWorkContext` → cria/acessa accessor e executa `BeginUnitOfWork`.
3) `EditEntity`/Find de entidades e coleções → resolve parâmetros vinculados a propriedades `Id`/`Ids` na classe.
4) `WithDecorators` → constrói mediador e invoca pipeline.
5) Invoca método do comando com parâmetros resolvidos.
6) `CompleteUnitOfWork` → persiste e retorna `Result`/`Result<T>`; compõe `Created` quando `ProduceNewEntity`/`MapCreatedRoute` aplicáveis.

Erros esperados são convertidos em `Problems` conforme atributos `ProduceProblems` e regras mencionadas.

## 8. Boas práticas

- Declare comandos em classes parciais quando precisar de `WasValidated`.
- Retorne `Result`/`Result<T>` e evite exceções no fluxo esperado.
- Use `Rules.Set` (SmartValidations) em `HasProblems` para mensagens consistentes e metadados (`rule/current/expected`).
- Prefira `WithUnitOfWork<TContext>` quando envolver persistência; use `EditEntity`/`ProduceNewEntity` conforme cenário.
- Para coleções, forneça propriedade `Ids` correspondente ao parâmetro do método para binding.
- Configure adapters EF/WorkContext via extensões fornecidas antes de executar comandos.
- Em endpoints, utilize `Map*` e metadados para documentação/segurança.

## 9. Limitações e diagnósticos (do Generator)

- Classe/método com argumentos genéricos não são suportados.
- `CancellationToken` só em métodos assíncronos.
- `WithDbContext` e `WithUnitOfWork` não podem coexistir; idem `WithWorkContext` com ambos.
- `ProduceNewEntity` e `EditEntity` exigem UoW; não podem coexistir entre si no mesmo método.
- `WithDecorators` exige tipo de retorno (não-`void`).
- `MapIdResultValue` exige que o tipo retornado tenha propriedade `Id` pública e legível; não pode coexistir
  com `MapResponseValues` (RCCMD049).
- `MapFind` exige que o template (grupo + rota) declare a variável `{id}` exatamente uma vez, obrigatória,
  não catch-all e com constraint compatível com o tipo do id (`{id:guid}` para `Guid` etc.); a classe do DTO
  deve ser top-level, não genérica e não file-local (mesmas regras para a classe de filtro do `MapSearch`).
- Em filtros de `MapSearch`, atributos de binding só têm efeito em parâmetros `[WithParameter]`; em parâmetros
  de serviço eles são diagnosticados (RCCMD023) em vez de ignorados.
- Dois endpoints do mesmo grupo não podem gerar o mesmo método handler (RCCMD047), e prefixos de grupo que
  normalizam para a mesma classe são diagnosticados (RCCMD046).
- Diagnósticos são emitidos durante a geração com mensagens indicando o ponto de falha; o catálogo completo
  está em `.docs/diagnostics.md`.

## 10. Resumo

SmartCommands automatiza a criação de handlers para comandos, integrando validação, Unit of Work, carregamento de entidades, Decorators e mapeamento HTTP. Com adapters para EF Core e WorkContext, os comandos podem executar persistência e buscas de forma padronizada, retornando `Result`/`Problems` consistentes e prontos para APIs.

## 11. Instruções para Ferramentas de IA (GitHub Copilot)

Diretrizes de geração alinhadas às seções anteriores:
- Comandos e atributos
  - Anote métodos com `Command` e, conforme necessidade, `WithValidateModel`, `WithUnitOfWork<TDbContext>`, `WithDecorators`, `EditEntity`, `ProduceNewEntity`.
  - Vincule parâmetros de entidade/coleção a propriedades `Id`/`Ids` na classe para Find automático.
  - Declare problemas esperados com `ProduceProblems(...)` quando aplicável.
- Resultados e validação
  - Retorne `Result`/`Result<T>`; evite exceções no fluxo esperado.
  - Use `Rules.Set<T>()` em `HasProblems(out Problems?)`.
- Integração EF/WorkContext
  - Registre `AddUnitOfWorkAccessor<TContext>()` (EF) ou `AddUnitOfWorkAccessor<TDbContext>()` (WorkContext builder).
  - Configure `RepositoryAdapter` e opções de `WorkContextAdapterOptions` conforme necessidade.
- Mapeamento HTTP
  - Use `MapPost`/`MapPut`/etc., `MapGroup`, `WithSummary`/`WithDescription`, `WithAuthorization`/`WithPolicy`.
  - Utilize `MapCreatedRoute` e `MapIdResultValue` para construção de Location em 201.
- Padrões de prompt
  - “Implemente um comando que valida entrada com `HasProblems`, usa `WithUnitOfWork<TDbContext>` e retorna `Result<T>` com `MapPost` + `MapCreatedRoute`.”
  - “Crie um comando com `EditEntity(typeof(Entity))` e parâmetro `entity` correspondente; gere handler que carrega por ID e retorna `Result`.”
  - “Habilite `WithDecorators` e componha pipeline para `Result` com interceptores (logging, métricas).”
  - “Configure adapters com `AddUnitOfWorkAccessor<MyDbContext>` e execute comandos via WorkContext.”
