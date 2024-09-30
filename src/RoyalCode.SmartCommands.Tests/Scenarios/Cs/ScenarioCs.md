# Cenários Cs: Executando um comando que retorna um Result com Valor.

Nestes cenários cada um dos comandos retornará um `Result<T>` com valor.

Possíveis cenários com essa característica:

- [Command]
- [Command, WithValidateModel]
- [Command, WithDecorators]
- [Command, WithValidateModel, WithDecorators]

Para cada possibilidade de cenário, o método do comando poderá ser síncrono ou assíncrono.

Quando síncrono, o método do comando deverá retornar um `Result<T>`.
Quando assíncrono, o método do comando deverá retornar uma tarefa que retorna um `Result<T>`.

### Exemplos de comandos que retornam um `Result<T>` com valor (síncrono)

#### Simples comando que retorna um resultado com valor

```cs
public class DoSomething
{
    public string? Name { get; set; }

    [Command]
    public Result<string> Get() => Result.Ok(Name);
}
```

#### Comando com validação

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

    [Command, WithValidateModel]
    public Result<string> Get() => Result.Ok(Name);
}
```

#### Comando com decoradores

```cs
public class DoSomething
{
    public string? Name { get; set; }

    [Command, WithDecorators]
    public Result<string> Get() => Result.Ok(Name);
}
```

#### Comando com validação e com decoradores

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
    
    [Command, WithValidateModel, WithDecorators]
    public Result<string> Get() => Result.Ok(Name);
}
```

#### Comando assíncrono que retorna um resultado com valor

```cs
public class DoSomething
{
    public string? Name { get; set; }

    [Command]
    public Task<Result<string>> GetAsync() => Task.FromResult(Result.Ok(Name));
}
```

#### Comando assíncrono com validação

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

    [Command, WithValidateModel]
    public Task<Result<string>> GetAsync() => Task.FromResult(Result.Ok(Name));
}
```

#### Comando assíncrono com decoradores

```cs
public class DoSomething
{
    public string? Name { get; set; }

    [Command, WithDecorators]
    public Task<Result<string>> GetAsync() => Task.FromResult(Result.Ok(Name));
}
```

#### Comando assíncrono com validação e com decoradores

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

    [Command, WithValidateModel, WithDecorators]
    public Task<Result<string>> GetAsync() => Task.FromResult(Result.Ok(Name));
}
```