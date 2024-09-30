# Cenários Gs: Executando comandos com WithParameter.

Nestes cenários os métodos de comandos terão parâmetros com o attribute `WithParameter`.

Possíveis cenários com essa característica:

- [Command] e um parâmetro com `WithParameter`
- [Command, EditEntity< Some, int >, WithUnitOfWork< TContext >] e dois parâmetros com `WithParameter`

Serão, no total, 2 cenários.

### Classes auxiliares

A classe `Some` representa uma entidade.

```cs
public class Some : IEntity<int>
{
    public int Id { get; set; }
    public int Value { get; set; }
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

#### Comando e um parâmetro com `WithParameter`

```cs
public class DoWithParameters
{
    public int Value { get; set; }

    [Command]
    internal Result<int> Plus([WithParameter] int other)
    {
        return Value + other;
    }
}
```

#### e dois parâmetros com `WithParameter`

```cs
public class DoWithParameters
{
    public int Value { get; set; }

    [Command, EditEntity<Some, int>, WithUnitOfWork<AppDbContext>]
    internal Result<int> Plus(Some some, [WithParameter] int other, [WithParameter] int another)
    {
        some.Value += Value + other + another;
        return some.Value;
    }
}
```

### template for the code snippets

```cs
public static class Code
{
    public const string Command =
"""
using Coreum.NewCommands;
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
using Coreum.NewCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.Gs;

namespace Tests.Scenarios.Gs.Internals;

""";
}
```