# Cen�rios Bs: Executando um comando que retorna um Result sem valor.

Nestes cen�rios cada um dos comandos retornar� um `Result` sem valor.

Poss�veis cen�rios com essa caracter�stica:

- [Command]
- [Command, WithValidateModel]
- [Command, WithDecorators]
- [Command, WithValidateModel, WithDecorators]

Para cada possibilidade de cen�rio, o m�todo do comando poder� ser s�ncrono ou ass�ncrono.

Quando s�ncrono, o m�todo do comando dever� retornar um `Result`.
Quando ass�ncrono, o m�todo do comando dever� retornar uma tarefa que retorna um `Result`.

### Exemplos de comandos que retornam um `Result` sem valor (s�ncrono)

#### Simples comando que retorna um objeto

```cs
public class DoSomething
{
    public string? Name { get; set; }

    [Command]
    public Result Get() => Result.Ok();
}
```

#### Comando com valida��o

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

#### Comando com valida��o e com decoradores

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

### Exemplos de comandos que retornam um `Result` sem valor (ass�ncrono)

#### Simples comando que retorna um objeto

```cs
public class DoSomething
{
    public string? Name { get; set; }

    [Command]
    public Task<Result> GetAsync() => Task.FromResult(Result.Ok());
}
```

#### Comando com valida��o

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

#### Comando com valida��o e com decoradores

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
