# Guia da API SmartCommands

SmartCommands é uma família de bibliotecas .NET que usa um incremental source generator para transformar
classes de caso de uso em handlers fortemente tipados, registro de DI e, opcionalmente, endpoints Minimal API.
O objetivo é remover boilerplate sem acoplar a lógica do comando ao HTTP ou a uma implementação específica
de persistência.

> **Para IA/agentes:** use [`smart-commands.ai-rules.md`](smart-commands.ai-rules.md). Este guia explica o
> contrato e as escolhas de uso. A documentação XML do pacote é a fonte da verdade para assinaturas exatas.
>
> **Verificado contra:** código do repositório em 2026-07-18; runtime `net8.0`/`net9.0`/`net10.0` e generator
> `netstandard2.0`.

## 1. Pacotes e responsabilidades

| Pacote | Responsabilidade |
|---|---|
| `RoyalCode.SmartCommands` | Atributos, mediator, decorators e contratos de accessors |
| `RoyalCode.SmartCommands.Generators` | Generator/analyzer de handlers, DI, diagnósticos e endpoints |
| `RoyalCode.SmartCommands.EntityFramework` | Implementação dos accessors diretamente sobre EF Core |
| `RoyalCode.SmartCommands.WorkContext` | Adapters WorkContext, UnitOfWork e retry de concorrência |

O pacote do generator é uma dependência de compilação. Handlers gerados continuam utilizáveis fora de
HTTP: Minimal API é apenas uma superfície opcional sobre o mesmo handler.

## 2. Comando e handler gerado

Uma classe torna-se comando quando declara exatamente um método anotado com `[Command]`:

```csharp
public class VerificarSku
{
    [Command]
    internal Result<bool> Executar([WithParameter] string sku) =>
        !string.IsNullOrWhiteSpace(sku);
}
```

O generator produz `IVerificarSkuHandler` e `VerificarSkuHandler`. Propriedades do objeto comando compõem o
payload; parâmetros marcados com `[WithParameter]` são expostos na assinatura pública do handler. O atributo
não determina rota: no endpoint, o ASP.NET Core usa o template e os atributos de binding aplicáveis para
resolver rota, query, header, body ou serviço.

Métodos podem ser síncronos ou assíncronos e retornar valor puro, `Result`, `Result<T>`, `Task` ou formas
assíncronas equivalentes suportadas. Prefira `Result` para falhas esperadas e sempre propague
`CancellationToken` em operações assíncronas.

## 3. Validação

### 3.1 Validação do modelo

`[WithValidateModel]` faz o handler chamar `HasProblems(out Problems?)` antes de iniciar persistência:

```csharp
public partial class CriarProduto
{
    public string? Nome { get; set; }

    public bool HasProblems(out Problems? problems) =>
        Rules.Set<CriarProduto>()
            .NotEmpty(Nome)
            .HasProblems(out problems);

    [Command, WithValidateModel]
    internal Result Executar() => Result.Ok();
}
```

A classe parcial permite que o generator emita `WasValidated` para ajudar a análise de nulabilidade quando
o contrato de validação estabelece propriedades não nulas.

### 3.2 Validações adicionais

Métodos `[CommandValidation]` retornam `Result`, `Task<Result>` ou `ValueTask<Result>`. Eles podem receber
serviços de DI, valores externos com `[WithParameter]` e `CancellationToken` quando assíncronos:

```csharp
[CommandValidation(Order = 5)]
internal async Task<Result> ValidarSkuAsync(ISkuService service, CancellationToken ct) =>
    await service.ValidarAsync(Sku, ct);
```

O valor default de `Order` é 10. O primeiro resultado com problemas encerra o handler. Empates não possuem
precedência pública; o generator usa apenas um desempate determinístico. Validators executam uma vez, antes
da unidade de trabalho e fora do retry.

## 4. Pipeline

O fluxo conceitual é:

```text
validação do body
  -> HasProblems
  -> CommandValidation
  -> abertura da unidade de trabalho
  -> carregamento de entidades
  -> decorators
  -> método do comando
  -> complete da unidade de trabalho
```

Falhas de validação e `NotFound` retornam problemas. Cancelamento e exceções inesperadas atravessam a borda;
os adapters não as convertem silenciosamente em sucesso ou problema de domínio.

## 5. Persistência e entidades

### 5.1 Unidade de trabalho

- `[WithUnitOfWork<TContext>]`: contrato genérico do núcleo.
- `[WithDbContext]`: adapter direto do pacote EntityFramework.
- `[WithWorkContext]`: adapter do ecossistema WorkContext.
- `[WithTransaction]`: exige transação naquele comando e requer uma das formas de UoW.

Não combine as três seleções de adapter no mesmo comando. Registre a implementação escolhida, por exemplo:

```csharp
services.AddUnitOfWorkAccessor<AppDbContext>();
```

### 5.2 Carregamento e edição

`[WithFindEntities<TContext>]` habilita carregamento automático quando não existe UoW. Parâmetros de entidade
e coleções usam as propriedades de identificador correspondentes no objeto comando.

Para editar uma entidade existente use a forma genérica atual:

```csharp
[MapGroup("produtos")]
[MapPut("/{id:guid}", "editar-produto")]
public class EditarProduto
{
    [Command, WithDbContext, EditEntity<Produto, Guid>]
    internal Result Executar(Produto produto) => produto.Atualizar(/* ... */);
}
```

A entidade é o primeiro parâmetro do método. A rota é resolvida por `RouteParameterName`, por uma única
variável no template ou pelas convenções `{parametroId}`/`{parametro}`. Ambiguidade produz diagnóstico.

## 6. Decorators e mediator

`[WithDecorators]` envolve o comando com `IDecorator<TCommand,TResult>`. Cada decorator recebe o modelo e um
mediator que representa o restante do pipeline. Chame `NextAsync()` para continuar. O pipeline preserva a
ordem fornecida pela DI e não deve reutilizar estado mutável entre invocações.

Decorators são apropriados para preocupações transversais do caso de uso; não os use para esconder falhas de
binding ou regras que pertencem ao próprio comando.

## 7. Retry de concorrência

`[WithRetryOnConcurrency]` funciona com `[WithWorkContext]`. A operação pode ser explícita ou derivada por
`ConcurrencyRetryOperations.DefaultFor<TCommand>()`; `MaxAttempts` pode vir do atributo ou das opções.

Cada tentativa repete o trecho transacional do comando, não as validações. Ao esgotar, o problema é produzido
por `IConcurrencyRetryProblemFactory` e pode ser especializado por delegates/providers registrados na DI.
Cancelamento não vira problema de retry.

## 8. Minimal APIs

### 8.1 Host gerado

```csharp
[AddHandlersServices("Catalogo")]
public static partial class HandlerServices { }

[MapApiHandlers]
public static partial class ApiEndpoints { }
```

As extensões geradas registram handlers e mapeiam endpoints no host. `[WithOpenApi]` no host habilita a chamada
de OpenAPI quando a dependência correspondente existe.

### 8.2 Commands mapeados

Use um único verbo por classe: `MapGet`, `MapPost`, `MapPut`, `MapPatch` ou `MapDelete`. Cada atributo recebe
template e endpoint name. `MapGroup` é opcional e, quando ausente, não cria prefixo implícito.

Metadados compartilhados:

- `WithSummary` e `WithDescription`;
- `WithAuthorization` e `WithPolicy`;
- `WithTags` com uma ou mais tags;
- `WithEndpointFilter<TFilter>` repetível, preservando a ordem declarada.

O filtro deve ser uma classe concreta acessível que implemente `IEndpointFilter`. Tipos genéricos construídos
e aninhados são aceitos quando acessíveis.

### 8.3 Status e corpo

Sem seleção explícita, o generator infere o status conforme verbo e retorno. Para commands, é possível usar:

```csharp
[WithResultStatus(HttpResultStatus.Ok)]
[WithResultStatus(HttpResultStatus.Created)]
[WithResultStatus(HttpResultStatus.NoContent)]
```

`Created` pode responder sem `Location`; adicione `MapCreatedRoute("{id}", nameof(Produto.Id))` para construir
a localização com placeholders nomeados. `NoContent` descarta somente o valor de sucesso de `Result<T>` e
preserva respostas de problema. `MapIdResultValue` e `MapResponseValues` moldam o corpo, mas são mutuamente
exclusivos.

### 8.4 Find e Search

`MapFind` trabalha com `EntityReference<TEntity,TId>` e responde `NotFound` quando necessário:

```csharp
[MapGroup("produtos")]
[MapFind("/{id:guid}", "obter-produto")]
[EntityReference<Produto, Guid>]
public class ProdutoDetalhes { }
```

`MapSearch` usa `SearchReference<TEntity>` ou `SearchReference<TEntity,TModel>` e integra o filtro ao mecanismo
SmartSearch. Métodos `[WithFilter]` podem ajustar os critérios antes da execução. Tags e endpoint filters
também se aplicam a Find/Search; `WithResultStatus` não.

## 9. Diagnósticos

Entradas inválidas produzem RCCMD localizado e bloqueiam a fonte relacionada. Não devem produzir fonte
parcial, `CS8785` ou exceção do generator. Consulte o [catálogo RCCMD](../diagnostics.md) para causas e
correções.

Nomes como `command`, `ct`, `accessor`, `decorators` e `retryOptions` são reservados quando emitidos no mesmo
escopo; colisões diagnosticáveis não são renomeadas silenciosamente.

## 10. Referências relacionadas

- [SmartProblems](problems.md)
- [SmartValidations](validations.md)
- [WorkContext](workcontext.md)
- [SmartSelector](selector.md)
- [SmartSearch](smartsearch.md)
- [Arquitetura Feature Slice](../feature-slice-architecture.md)
- [Arquitetura legada](../legacy-architecture.md)

