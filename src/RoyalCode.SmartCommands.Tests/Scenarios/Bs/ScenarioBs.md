# Cenários Bs: Executando um comando que retorna um Result sem valor.

Nestes cenários cada um dos comandos retornará um `Result` sem valor.

Possíveis cenários com essa característica:

- [Command]
- [Command, WithValidateModel]
- [Command, WithDecorators]
- [Command, WithValidateModel, WithDecorators]

Para cada possibilidade de cenário, o método do comando poderá ser síncrono ou assíncrono.

Quando síncrono, o método do comando deverá retornar um `Result`.
Quando assíncrono, o método do comando deverá retornar uma tarefa que retorna um `Result`.

### Exemplos de comandos que retornam um `Result` sem valor (síncrono)

#### Simples comando que retorna um objeto

```cs
public class DoSomething
{
    public string? Name { get; set; }

    [Command]
    public Result Get() => Result.Ok();
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
    public Result Get() => Result.Ok();
}
```

#### Comando com decoradores

```cs
public class DoSomething
{
    public string? Name { get; set; }

    [Command, WithDecorators]
    public Result Get() => Result.Ok();
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
    public Result Get() => Result.Ok();
}
```

### Exemplos de comandos que retornam um `Result` sem valor (assíncrono)

#### Simples comando que retorna um objeto

```cs
public class DoSomething
{
    public string? Name { get; set; }

    [Command]
    public Task<Result> GetAsync() => Task.FromResult(Result.Ok());
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
    public Task<Result> GetAsync() => Task.FromResult(Result.Ok());
}
```

#### Comando com decoradores

```cs
public class DoSomething
{
    public string? Name { get; set; }

    [Command, WithDecorators]
    public Task<Result> GetAsync() => Task.FromResult(Result.Ok());
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
    public Task<Result> GetAsync() => Task.FromResult(Result.Ok());
}
```
