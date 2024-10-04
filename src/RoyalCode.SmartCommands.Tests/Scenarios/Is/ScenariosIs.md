# Cenários Is: Mapeamento de retorno de comandos a endpoints de minimal API

Nestes cenários os comandos terão atributos para mapear os handlers a endpoints de minimal API.
O Mapeamento tratará o retorno usando os atributos [MapIdResultValue] e [MapResponseValues].

Possíveis cenários com essa característica:

- [MapPost, MapIdResultValue] onde o objeto retornado possui uma propriedade Id.
- [MapPost, MapResponseValues] onde o objeto retornado possuirá as propriedades de retorno.
- [MapPost, MapIdResultValue, MapCreatedRoute] onde o comando terá também com [ProduceNewEntity] para criar nova entidade.
- [MapPost, MapResponseValues, MapCreatedRoute] onde o comando terá também com [ProduceNewEntity] para criar nova entidade.

Serão, no total, 4 cenários.

### Classes auxiliares

A classe `Some` representa uma entidade.

```cs
public class Some : IEntity<int>
{
    public int Id { get; set; }
    public int Value { get; set; }
    public string Name { get; set; }
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

#### 1 - Utilizando [MapPost, MapIdResultValue]

```cs
[MapGroup("some")]
[MapPost("", "create-some"), MapIdResultValue]
public class CreateSome
{
    public int Value { get; set; }
    
    public string Name { get; set; }

    [Command]
    internal Task<Result<Some>> Execute()
    {
        Result<Some> result = new Some()
        {
            Value = Value,
            Name = Name,
            Active = true
        };
        
        return Task.FromResult(result);
    }
}
```

#### 2 - Utilizando [MapPost, MapResponseValues]

```cs
[MapGroup("some")]
[MapPost("", "create-some"), MapResponseValues("Id", "Name")]
public class CreateSome
{
    public int Value { get; set; }
    
    public string Name { get; set; }

    [Command]
    internal Some Execute()
    {
        return new Some()
        {
            Value = Value,
            Name = Name,
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
using RoyalCode.SmartCommands.Tests.Scenarios.Is;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using Microsoft.AspNetCore.Routing;

namespace Tests.Scenarios.Is;

[MapApiHandlers]
public static partial class VaultApis { }
""";
    
    public const string Map =
"""
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RoyalCode.SmartCommands.Tests.Scenarios.Is;
using RoyalCode.SmartProblems;
using RoyalCode.SmartProblems.HttpResults;

namespace Tests.Scenarios.Is;

public static partial class MapSomeApi
{
    public static RouteGroupBuilder MapSomeGroup(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("some");

        group.MapPost("", CreateSomeHandleAsync)
            .WithName("create-some")
            .WithOpenApi();

        return group;
    }

    [ProduceProblems(ProblemCategory.InvalidParameter)]
    private static async Task<CreatedMatch<int>> CreateSomeHandleAsync(
        ICreateSomeHandler handler, 
        CreateSome command, 
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(command, ct);
        return result.CreatedMatch(v => $"some/{v.Id}", v => v.Id);
    }
}

""";
}
```