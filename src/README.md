# SmartCommands

SmartCommands is a family of .NET libraries that implements a command-pattern approach using source generators to eliminate boilerplate for handlers, DI registration, validation, unit of work, repository access, decorators, and HTTP endpoint mapping.

Projects target .NET 8, .NET 9, and .NET 10. The analyzer/generator targets .NET Standard 2.0.

- Runtime: `RoyalCode.SmartCommands` — attributes and contracts for commands and handlers.
- EF Core adapter: `RoyalCode.SmartCommands.EntityFramework` — `IUnitOfWorkAccessor<TContext>` and `IRepositoriesAccessor<TContext>`.
- WorkContext adapter: `RoyalCode.SmartCommands.WorkContext` — adapters and DI for `IWorkContext`.
- Source Generator: `RoyalCode.SmartCommands.Generators` — creates handlers, DI wiring, find/edit flows, decorators pipeline, and HTTP mapping.

## Features
- Attribute-driven commands with generated handlers (`I{Command}Handler`, `{Command}Handler`).
- Validation integration via `HasProblems(out Problems?)` and `WithValidateModel`.
- Additional validation methods with `CommandValidation` (`Order` defaults to `10`) and
  `Result`/`Task<Result>`/`ValueTask<Result>` short-circuiting.
- Unit of Work and repositories via `WithUnitOfWork<TContext>` / `WithDbContext` / `WithWorkContext`.
- Entity loading and editing with `WithFindEntities<TContext>` and `EditEntity<TEntity,TId>`.
- Decorators pipeline with `WithDecorators` and `IDecorator<TCommand, TResult>`.
- Minimal APIs mapping using `MapPost/Put/Patch/Delete/Get`, metadata (`WithSummary`, `WithDescription`, `WithAuthorization`, `WithPolicy`, `WithTags`), created/accepted location (`MapCreatedRoute`, `MapAcceptedRoute` — the `202` location is optional), response composition (`MapIdResultValue`, `MapResponseValues`), explicit success status (`WithResultStatus`: `Ok`/`Created`/`Accepted`/`NoContent`), and repeatable endpoint filters (`WithEndpointFilter<TFilter>`).
- Consistent results and problems modeling via SmartProblems (`Result`, `Result<T>`, `Problems`).

## Compatibility
- Commands: .NET 8, .NET 9, .NET 10.
- Generator: .NET Standard 2.0.
- Tested with Microsoft.NET.Test.Sdk 18.x; see `RoyalCode.SmartCommands.Tests` for coverage across sync/async, validate, decorators, find/edit, and mapping scenarios.

## Installation
Add the packages you need:
- `RoyalCode.SmartCommands`
- `RoyalCode.SmartCommands.EntityFramework` (EF Core accessors)
- `RoyalCode.SmartCommands.WorkContext` (WorkContext accessors)
- `RoyalCode.SmartCommands.Generators` (analyzer/source generator)

## Quick start
1. Define a partial command class with a method annotated with `Command` and required attributes.
2. Implement `HasProblems(out Problems?)` when using `WithValidateModel`.
3. Register Unit of Work adapters.
4. Consume the generated handler interface via DI.

Example (create entity with validation + UoW):

```csharp
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

public partial class CreateProduct
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }

    public bool HasProblems(out Problems? problems) =>
        Rules.Set<CreateProduct>()
            .NotEmpty(Name)
            .GreaterThanOrEqual(Price, 0)
            .HasProblems(out problems);

    [Command, WithValidateModel, WithUnitOfWork<MyDbContext>]
    public Result<Product> Execute()
    {
        var entity = new Product(Name, Price);
        return entity;
    }
}
```

EF Core registration:
```csharp
services.AddUnitOfWorkAccessor<MyDbContext>();
```

WorkContext registration:
```csharp
services.AddWorkContext<MyDbContext>()
    .AddUnitOfWorkAccessor<MyDbContext>();
```

HTTP mapping (Minimal APIs):
```csharp
[MapPost("/", "CreateProduct")]
[MapGroup("api/products")]
[WithSummary("Create product")]
[WithDescription("Creates a new product")]
[MapCreatedRoute("{id}", nameof(Product.Id))]
public partial class CreateProduct { /* ... */ }
```

## Guidance
- Prefer returning `Result`/`Result<T>`; avoid exceptions for expected flows.
- Use partial classes to enable `WasValidated` generation for null-state.
- Do not mix `WithUnitOfWork` with `WithDbContext`/`WithWorkContext` on the same command.
- `EditEntity<TEntity,TId>` requires UoW and the first method parameter typed as `TEntity`.
- `CancellationToken` only in async methods.

## Documentation

- [Complete guide](.docs/references/smart-commands.md)
- [Operational rules for AI](.docs/references/smart-commands.ai-rules.md)
- [RCCMD diagnostics](.docs/diagnostics.md)

## Tests
See `RoyalCode.SmartCommands.Tests` for scenarios covering:
- Validation-first pipeline (`WithValidateModel`).
- Additional validation ordering, DI, cancellation and short-circuiting.
- Decorators sync/async with and without results.
- Find entities and collections by id (`MapFind`) or by an alternate/composite key (`MapFindBy<TEntity>`), edit flows, and NotFound problems that name the entity.
- Created and accepted responses with `MapCreatedRoute`, `MapAcceptedRoute`, `MapIdResultValue`, and `MapResponseValues`.
- HTTP status selection, tags, endpoint filters, Find/Search and OpenAPI metadata.

