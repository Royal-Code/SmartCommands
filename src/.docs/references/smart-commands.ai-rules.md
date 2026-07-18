# Regras para IA — SmartCommands

> Verificado contra a API do repositório em 2026-07-18. Para explicações e exemplos completos, consulte
> [`smart-commands.md`](smart-commands.md).

## 1. Pacotes

| Necessidade | Pacote |
|---|---|
| Atributos, mediator e contratos de accessors | `RoyalCode.SmartCommands` |
| Geração de handlers, DI e endpoints | `RoyalCode.SmartCommands.Generators` |
| Adapter direto para EF Core | `RoyalCode.SmartCommands.EntityFramework` |
| Adapter para WorkContext e retry de concorrência | `RoyalCode.SmartCommands.WorkContext` |

Os pacotes runtime suportam `net8.0`, `net9.0` e `net10.0`. O generator usa `netstandard2.0` e deve ser
referenciado como analyzer.

## 2. Regras invioláveis

1. Declare exatamente um método `[Command]` por classe de comando.
2. Prefira `Result`/`Result<T>` para falhas esperadas; não use exceção como fluxo de validação ou domínio.
3. Todo fluxo assíncrono retorna `Task`/`Task<T>`/`ValueTask`/`ValueTask<T>` e propaga `CancellationToken`;
   nunca gere `async void`.
4. `[WithParameter]` significa valor externo ao payload. Ele vira parâmetro do handler e do delegate HTTP;
   não significa rota por si só. Binding de rota depende do template ou de atributo ASP.NET explícito.
5. Use `[EditEntity<TEntity, TId>]`, nunca `EditEntity(typeof(TEntity))`. A entidade carregada deve ser o
   primeiro parâmetro do método do comando.
6. Não combine `WithUnitOfWork<T>`, `WithDbContext` e `WithWorkContext` no mesmo comando.
7. `[WithTransaction]` exige uma unidade de trabalho e apenas força transação; não a desliga.
8. `[WithRetryOnConcurrency]` exige `WithWorkContext`. Validators adicionais executam uma vez, antes do retry.
9. Não use simultaneamente `MapIdResultValue` e `MapResponseValues`.
10. `MapCreatedRoute` usa placeholders nomeados, como `"{id}"`, casados com propriedades declaradas por
    `nameof`; o formato posicional `"{0}"` é inválido.
11. `WithResultStatus` vale somente para command maps. Find/Search mantêm seus contratos próprios.
12. Código que recebe RCCMD deve corrigir a declaração; não contorne o generator nem edite `.g.cs`.

## 3. Pipeline gerado

Ordem semântica:

```text
RequireBody
  -> HasProblems
  -> CommandValidation (Order crescente; default 10)
  -> Begin UnitOfWork
  -> carregamento de entidades / EditEntity
  -> decorators
  -> command
  -> Complete UnitOfWork
```

Com retry de concorrência, validações permanecem fora do laço. O trecho de UoW, carregamento, decorators,
command e complete pode ser repetido.

## 4. Declarações canônicas

```csharp
public partial class CriarProduto
{
    public string? Nome { get; set; }

    public bool HasProblems(out Problems? problems) => /* regras */;

    [CommandValidation(Order = 5)]
    internal Task<Result> ValidarAsync(IServico servico, CancellationToken ct) =>
        servico.ValidarAsync(Nome, ct);

    [Command, WithValidateModel, WithUnitOfWork<AppDbContext>]
    internal Result<Produto> Executar() => new Produto(Nome!);
}
```

```csharp
[MapGroup("produtos")]
[MapPut("/{id:guid}", "editar-produto")]
public partial class EditarProduto
{
    [Command, WithWorkContext, EditEntity<Produto, Guid>]
    internal Task<Result> ExecutarAsync(Produto produto, CancellationToken ct) => /* ... */;
}
```

`CancellationToken` é reconhecido diretamente e não recebe `[WithParameter]`. Valores externos comuns usam o
atributo, por exemplo:

```csharp
internal Task<Result> ExecutarAsync(
    Produto produto,
    [WithParameter] string origem,
    CancellationToken ct);
```

## 5. Minimal API

- Verbos: `MapGet`, `MapPost`, `MapPut`, `MapPatch`, `MapDelete`.
- Casos de leitura: `MapFind` + `EntityReference<TEntity,TId>`; `MapSearch` + `SearchReference<TEntity[,TModel]>`.
- Grupo: `MapGroup`; sem ele, não há prefixo implícito.
- Metadata: `WithSummary`, `WithDescription`, `WithAuthorization`, `WithPolicy`, `WithTags`.
- Filtros: `WithEndpointFilter<TFilter>` repetível; ordem declarada é preservada.
- Status explícito: `WithResultStatus(HttpResultStatus.Ok|Created|NoContent)`.
- `NoContent` pode descartar o valor de sucesso de `Result<T>`, mas nunca descarta problemas.
- `Created` sem `MapCreatedRoute` responde 201 sem `Location`; use `MapCreatedRoute` para produzir `Location`.

## 6. Registro

```csharp
[AddHandlersServices("Catalogo")]
public static partial class HandlerServices { }

[MapApiHandlers]
public static partial class ApiEndpoints { }
```

Use as extensões geradas para registrar handlers e mapear endpoints. Registre também o adapter escolhido:

```csharp
services.AddUnitOfWorkAccessor<AppDbContext>(); // pacote EntityFramework
```

## 7. Checklist

- [ ] Um `[Command]` por classe.
- [ ] Retorno e async compatíveis com os atributos escolhidos.
- [ ] `CancellationToken` propagado.
- [ ] Validação antes de UoW/retry.
- [ ] `EditEntity<TEntity,TId>` e rota sem ambiguidade.
- [ ] Binding externo explícito somente quando necessário.
- [ ] Endpoint name único e não vazio.
- [ ] Status, corpo e OpenAPI coerentes.
- [ ] Nenhum arquivo gerado editado manualmente.
