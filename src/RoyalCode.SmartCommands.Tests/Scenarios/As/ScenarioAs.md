# Cen�rios As: Executando um comando que retorna um objeto.

Nestes cen�rios cada um dos comandos retornar� um apenas um objeto.

Poss�veis cen�rios com essa caracter�stica:

- [Command]
- [Command, WithValidateModel]
- [Command, WithDecorators]
- [Command, WithValidateModel, WithDecorators]

Para cada possibilidade de cen�rio, o m�todo do comando poder� ser s�ncrono ou ass�ncrono.

Quando s�ncrono, o m�todo do comando dever� retornar um objeto.
Quando ass�ncrono, o m�todo do comando dever� retornar uma tarefa que retorna um objeto.

### Exemplos de comandos que retornam um objeto (s�ncrono)

#### Simples comando que retorna um objeto

```cs
public class DoSomething
{
    public string? Name { get; set; }

    [Command]
    public Some Get() => new Some(Name ?? throw new Exception("Bad Name"));
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
    public Some Get() => new Some(Name!);
}
```

#### Comando com decoradores

```cs
public class DoSomething
{
    public string? Name { get; set; }

    [Command, WithDecorators]
    public Some Get() => new Some(Name ?? throw new Exception("Bad Name"));
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
    public Some Get() => new Some(Name!);
}
```

### Exemplos de comandos que retornam um objeto (ass�ncrono)

#### Simples comando que retorna um objeto

```cs
public class DoSomething
{
    public string? Name { get; set; }

    [Command]
    public async Task<Some> GetAsync() => await Task.FromResult(new Some(Name ?? throw new Exception("Bad Name"));
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
    public async Task<Some> GetAsync() => await Task.FromResult(new Some(Name!));
}
```

#### Comando com decoradores

```cs
public class DoSomething
{
    public string? Name { get; set; }

    [Command, WithDecorators]
    public async Task<Some> GetAsync() => await Task.FromResult(new Some(Name ?? throw new Exception("Bad Name"));
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
    public async Task<Some> GetAsync() => await Task.FromResult(new Some(Name!));
}
```