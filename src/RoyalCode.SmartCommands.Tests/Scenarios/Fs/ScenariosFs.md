# Cenários Fs: Executando comandos com EditEntityAttribute.

Nestes cenários os comandos terão o attribute `EditEntity<TEntity, TId>` e serão executados com um UnitOfWork.

Possíveis cenários com essa característica:

- [Command, EditEntity< Some, int >, WithValidateModel, WithUnitOfWork< TContext >]
- [Command, EditEntity< Some, int >, WithValidateModel, WithDecorators, WithUnitOfWork< TContext >]

Para cada possibilidade de cenário, o método do comando poderá ser síncrono ou assíncrono.

Para cada possibilidade de cenário, o método do comando poderá retornar um `Result` ou `Result<TEntity>`.

A classe será partial, e deverá ser gerado o WasValidated.

Serão, no total, 8 cenários.

### Classes auxiliares

A classe `Some` representa uma entidade.

```cs
public class Some : IEntity<int>
{
    public int Id { get; set; }
    public string? Name { get; set; }
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

### Cenários síncronos sem decoradores

#### Comando que retorna um `Result`

```cs
public partial class EditSome
{
    public string? Name { get; set; }
    
    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<EditSome>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }
    
    [Command, EditEntity<Some, int>, WithValidateModel, WithUnitOfWork<AppDbContext>]
    public Result Edit(Some some)
    {
        WasValidated();
        some.Name = Name;
        return Result.Ok();
    }
}
```

#### Comando que retorna um `Result<T>`

```cs
public partial class EditSome
{
    public string? Name { get; set; }
    
    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<EditSome>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }
    
    [Command, EditEntity<Some, int>, WithValidateModel, WithUnitOfWork<AppDbContext>]
    public Result<Some> Edit(Some some)
    {
        WasValidated();
        some.Name = Name;
        return some;
    }
}
```

### Cenários assíncronos sem decoradores

#### Comando que retorna um objeto

```cs
public partial class EditSome
{
    public string? Name { get; set; }
    
    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<EditSome>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }
    
    [Command, EditEntity<Some, int>, WithValidateModel, WithUnitOfWork<AppDbContext>]
    public Task<Result> EditAsync(Some some)
    {
        WasValidated();
        some.Name = Name;
        return Task.FromResult(Result.Ok());
    }
}
```

#### Comando que retorna um `Result<T>`

```cs
public partial class EditSome
{
    public string? Name { get; set; }
    
    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<EditSome>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }
    
    [Command, EditEntity<Some, int>, WithValidateModel, WithUnitOfWork<AppDbContext>]
    public Task<Result<Some>> EditAsync(Some some)
    {
        WasValidated();
        some.Name = Name;
        Result<Some> result = some;
        return return Task.FromResult(result);
    }
}
```

### Cenários síncronos com decoradores

#### Comando que retorna um objeto

```cs
public partial class EditSome
{
    public string? Name { get; set; }
    
    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<EditSome>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }
    
    [Command, EditEntity<Some, int>, WithValidateModel, WithDecorators, WithUnitOfWork<AppDbContext>]
    public Result Edit(Some some)
    {
        WasValidated();
        some.Name = Name;
        return Result.Ok();
    }
}
```

#### Comando que retorna um `Result<T>`

```cs
public partial class EditSome
{
    public string? Name { get; set; }
    
    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<EditSome>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }
    
    [Command, EditEntity<Some, int>, WithValidateModel, WithDecorators, WithUnitOfWork<AppDbContext>]
    public Result<Some> Edit(Some some)
    {
        WasValidated();
        some.Name = Name;
        return some;
    }
}
```

### Cenários assíncronos com decoradores

#### Comando que retorna um objeto

```cs
public partial class EditSome
{
    public string? Name { get; set; }
    
    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<EditSome>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }
    
    [Command, ProduceNewEntity, WithValidateModel, WithDecorators, WithUnitOfWork<AppDbContext>]
    public Task<Result> EditAsync(Some some)
    {
        WasValidated();
        some.Name = Name;
        return Task.FromResult(Result.Ok());
    }
}
```

#### Comando que retorna um `Result<T>`

```cs
public partial class EditSome
{
    public string? Name { get; set; }
    
    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<EditSome>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }
    
    [Command, ProduceNewEntity, WithValidateModel, WithDecorators, WithUnitOfWork<AppDbContext>]
    public Task<Result<Some>> EditAsync(Some some)
    {
        WasValidated();
        some.Name = Name;
        Result<Some> result = some;
        return Task.FromResult(result);
    }
}
```

### Considerações

Todos handlers de comandos com `WithUnitOfWork` serão assíncronos,
pois o UnitOfWork tem métodos assíncronos.

Quando há o atribute `WithValidateModel` o comando é validado antes de ser executado
e o retorno do handler sempre será um `Result` ou `Result<T>`.

Como em todos os cenários haverá `WithUnitOfWork` e `WithValidateModel`
o retorno do handler sempre será um `Task<Result>` ou `Task<Result<T>>`.

### template for the code snippets

```cs
public static class Code
{
    public const string Command =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Tests.Scenarios.Fs;

""";
    
    public const string Interface =
"""
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.Fs;

""";
    
    public const string Handler =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.Fs;

namespace Tests.Scenarios.Fs.Internals;

""";
}
```