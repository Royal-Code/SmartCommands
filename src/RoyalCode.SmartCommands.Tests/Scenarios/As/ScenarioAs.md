# Cenários As: Executando um comando que retorna um objeto.

Nestes cenários cada um dos comandos retornará um apenas um objeto.

Possíveis cenários com essa característica:

- [Command]
- [Command, WithValidateModel]
- [Command, WithDecorators]
- [Command, WithValidateModel, WithDecorators]

Para cada possibilidade de cenário, o método do comando poderá ser síncrono ou assíncrono.

Quando síncrono, o método do comando deverá retornar um objeto.
Quando assíncrono, o método do comando deverá retornar uma tarefa que retorna um objeto.

### Exemplos de comandos que retornam um objeto (síncrono)

#### Simples comando que retorna um objeto

```cs
public class DoSomething
{
    public string? Name { get; set; }

    [Command]
    public Some Get() => new Some(Name ?? throw new Exception("Bad Name"));
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
    public Some Get() => new Some(Name!);
}
```

### Exemplos de comandos que retornam um objeto (assíncrono)

#### Simples comando que retorna um objeto

```cs
public class DoSomething
{
    public string? Name { get; set; }

    [Command]
    public async Task<Some> GetAsync() => await Task.FromResult(new Some(Name ?? throw new Exception("Bad Name"));
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
    public async Task<Some> GetAsync() => await Task.FromResult(new Some(Name!));
}
```