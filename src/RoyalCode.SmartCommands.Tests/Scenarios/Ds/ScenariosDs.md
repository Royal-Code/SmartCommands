# Cenários Ds: Executando comandos com UnitOfWork.

Nestes cenários os comandos terão o attribute `WithUnitOfWork` e serão executados com um UnitOfWork.

Possíveis cenários com essa característica:

- [Command, WithValidateModel, WithUnitOfWork]
- [Command, WithValidateModel, WithDecorators, WithUnitOfWork]

Para cada possibilidade de cenário, o método do comando poderá ser síncrono ou assíncrono.

Para cada possibilidade de cenário, o método do comando poderá retornar um objeto ou `Result` ou `Result<T>`.

Serão, no total, 12 cenários.

Nestes cenários o comando receberá um `DbContext` no método de execução.

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

#### Comando que retorna um objeto

```cs
public class DoSomething
{
    public string? Name { get; set; }
    
    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomething>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }
    
    [Command, WithValidateModel, WithUnitOfWork<AppDbContext>]
    public Some Do(AppDbContext db)
    {
        var some = new Some { Id = 1, Name = Name! };
        db.Some.Add(some);
        return some;
    }
}
```

#### Comando que retorna um `Result`

```cs
public class DoSomething
{
    public string? Name { get; set; }
    
    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomething>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }
    
    [Command, WithValidateModel, WithUnitOfWork<AppDbContext>]
    public Result Do(AppDbContext db)
    {
        var some = new Some { Id = 1, Name = Name! };
        db.Some.Add(some);
        return Result.Ok();
    }
}
```

#### Comando que retorna um `Result<T>`

```cs
public class DoSomething
{
    public string? Name { get; set; }
    
    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomething>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }
    
    [Command, WithValidateModel, WithUnitOfWork<AppDbContext>]
    public Result<Some> Do(AppDbContext db)
    {
        var some = new Some { Id = 1, Name = Name! };
        db.Some.Add(some);
        return some;
    }
}
```

### Cenários assíncronos sem decoradores

#### Comando que retorna um objeto

```cs
public class DoSomething
{
    public string? Name { get; set; }
    
    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomething>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }
    
    [Command, WithValidateModel, WithUnitOfWork<AppDbContext>]
    public async Task<Some> DoAsync(AppDbContext db)
    {
        var some = new Some { Id = 1, Name = Name! };
        await db.Some.AddAsync(some);
        return some;
    }
}
```

#### Comando que retorna um `Result`

```cs
public class DoSomething
{
    public string? Name { get; set; }
    
    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomething>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }
    
    [Command, WithValidateModel, WithUnitOfWork<AppDbContext>]
    public async Task<Result> DoAsync(AppDbContext db)
    {
        var some = new Some { Id = 1, Name = Name! };
        await db.Some.AddAsync(some);
        return Result.Ok();
    }
}
```

#### Comando que retorna um `Result<T>`

```cs
public class DoSomething
{
    public string? Name { get; set; }
    
    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomething>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }
    
    [Command, WithValidateModel, WithUnitOfWork<AppDbContext>]
    public async Task<Result<Some>> DoAsync(AppDbContext db)
    {
        var some = new Some { Id = 1, Name = Name! };
        await db.Some.AddAsync(some);
        return some;
    }
}
```

### Cenários síncronos com decoradores

#### Comando que retorna um objeto

```cs
public class DoSomething
{
    public string? Name { get; set; }
    
    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomething>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }
    
    [Command, WithValidateModel, WithDecorators, WithUnitOfWork<AppDbContext>]
    public Some Do(AppDbContext db)
    {
        var some = new Some { Id = 1, Name = Name! };
        db.Some.Add(some);
        return some;
    }
}
```

#### Comando que retorna um `Result`

```cs
public class DoSomething
{
    public string? Name { get; set; }
    
    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomething>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }
    
    [Command, WithValidateModel, WithDecorators, WithUnitOfWork<AppDbContext>]
    public Result Do(AppDbContext db)
    {
        var some = new Some { Id = 1, Name = Name! };
        db.Some.Add(some);
        return Result.Ok();
    }
}
```

#### Comando que retorna um `Result<T>`

```cs
public class DoSomething
{
    public string? Name { get; set; }
    
    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomething>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }
    
    [Command, WithValidateModel, WithDecorators, WithUnitOfWork<AppDbContext>]
    public Result<Some> Do(AppDbContext db)
    {
        var some = new Some { Id = 1, Name = Name! };
        db.Some.Add(some);
        return some;
    }
}
```

### Cenários assíncronos com decoradores

#### Comando que retorna um objeto

```cs
public class DoSomething
{
    public string? Name { get; set; }
    
    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomething>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }
    
    [Command, WithValidateModel, WithDecorators, WithUnitOfWork<AppDbContext>]
    public async Task<Some> DoAsync(AppDbContext db)
    {
        var some = new Some { Id = 1, Name = Name! };
        await db.Some.AddAsync(some);
        return some;
    }
}
```

#### Comando que retorna um `Result`

```cs
public class DoSomething
{
    public string? Name { get; set; }
    
    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomething>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }
    
    [Command, WithValidateModel, WithDecorators, WithUnitOfWork<AppDbContext>]
    public async Task<Result> DoAsync(AppDbContext db)
    {
        var some = new Some { Id = 1, Name = Name! };
        await db.Some.AddAsync(some);
        return Result.Ok();
    }
}
```

#### Comando que retorna um `Result<T>`

```cs
public class DoSomething
{
    public string? Name { get; set; }
    
    [MemberNotNullWhen(false, nameof(Name))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<DoSomething>()
            .NotEmpty(Name)
            .HasProblems(out problems);
    }
    
    [Command, WithValidateModel, WithDecorators, WithUnitOfWork<AppDbContext>]
    public async Task<Result<Some>> DoAsync(AppDbContext db)
    {
        var some = new Some { Id = 1, Name = Name! };
        await db.Some.AddAsync(some);
        return some;
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
