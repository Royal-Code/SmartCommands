# Cenários Hs: Mapeamento de WEB API

Nestes cenários os comandos terão atributos para mapear os handlers a endpoints de minimal API.

Possíveis cenários com essa característica:

- [MapPost] comum, sem editar entidade ou criar nova entidade.
- [MapPost, MapCreatedRoute] para criar nova entidade, utilizado com [ProduceNewEntity]
- [MapPost, MapCreatedRoute] para criar nova entidade a partir de uma existente, utilizando com [EditEntity< Some, int >]
- [MapPut] para editar uma entidade, utilizando [EditEntity< Some, int >]
- [MapPatch] para editar uma entidade, utilizando [EditEntity< Some, int >]
- [MapDelete] para remover uma entidade
- [MapDelete] para inativar uma entidade, utilizando [EditEntity< Some, int >]


Serão, no total, 7 cenários.

### Classes auxiliares

A classe `Some` representa uma entidade.

```cs
public class Some : IEntity<int>
{
    public int Id { get; set; }
    public int Value { get; set; }
    public bool Active { get; set; }
}
```

A classe `AppDbContext` representa um contexto de banco de dados.

```cs
public class AppDbContext : DbContext
{
    public DbSet<Some> Some { get; set; }
    
    public override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseInMemoryDatabase("AppDbContext");
    }
}
```

### Cenários

#### 1 - Comum, sem editar entidade ou criar nova entidade.

```cs
[MapGroup("api/some")]
[MapPost("create")]
public class CreateSome
{
    public int Value { get; set; }

    [Command]
    internal Result Execute()
    {
        return Result.Ok();
    }
}
```

#### 2 - Criar nova entidade, utilizado junto com [ProduceNewEntity]

```cs
[MapGroup("api/some")]
[MapPost("create")]
[MapCreatedRoute("{0}", "Id")]
public class CreateSome
{
    public int Value { get; set; }

    [Command, ProduceNewEntity, WithUnitOfWork<AppDbContext>]
    internal Some Execute(AppDbContext db)
    {
        return new Some()
        {
            Value = Value,
            Active = true
        };
    }
}
```

### template for the code snippets

```cs
public static class Code
{
    public const string Command =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Tests.Scenarios.Gs;

""";
    
    public const string Interface =
"""
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.Gs;

""";
    
    public const string Handler =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.Gs;

namespace Tests.Scenarios.Gs.Internals;

""";
}
```