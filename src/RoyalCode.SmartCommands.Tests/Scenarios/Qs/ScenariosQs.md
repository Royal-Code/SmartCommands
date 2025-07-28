# Cenários Qs: Testes com atributo [Query]

Neste cenário será utilizado o atributo [Query] para criar serviços de consulta que realizam buscas por entidades.

## Cenário 1: Consulta de modelos a partir de uma entidade obtida por ID

### Descrição

Neste cenário, será criado um serviço de consulta que recebe um ID de entidade, obtém a entidade correspondente e chama o método decorado com o atributo [Query] para retornar os modelos associados a essa entidade.

### Código

Classe das Entidades
```csharp
public class Some : IEntity<int>
{
    public int Id { get; set; }
    public int Value { get; set; }
    public ICollection<Other> Others { get; set; }
}

public class Other : IEntity<int>
{
    public int Id { get; set; }
    public string Name { get; set; }
    public Some Some { get; set; }
}
```

Classe do modelo de seleção de Other com o método de consulta decorado com [Query]
```csharp
public class SomeOtherDetails
{
    public int Id { get; set; }
    public string OtherName { get; set; }
    public int SomeValue { get; set; }

    [Query, WithUnitOfWork<IWorkContext>]
    public static IEnumerable<SomeOtherDetails> GetDetails(Some some)
    {
        return some.Others.Select(o => new SomeOtherDetails
        {
            Id = o.Id,
            OtherName = o.Name,
            SomeValue = some.Value
        });
    }
}
```

Classes de serviço, interface e implementação
```csharp

public interface ISomeOtherDetailsQueryHandler
{
    Task<Result<IEnumerable<SomeOtherDetails>>> HandleAsync(Id<Some, int> id, CancellationToken ct);
}

public class SomeOtherDetailsQueryHandler : ISomeOtherDetailsQueryHandler
{
    private readonly IUnitOfWorkAccessor<IWorkContext> accessor;

    public SomeOtherDetailsQueryHandler(IUnitOfWorkAccessor<IWorkContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result<IEnumerable<SomeOtherDetails>>> HandleAsync(Id<Some, int> id, CancellationToken ct)
    {
        var findResult = await accessor.FindEntityAsync(id, ct);
        if (findResult.NotFound(out var notFoundProblem))
            return notFoundProblem;

        var result = SomeOtherDetails.GetDetails(findResult.Entity);

        return new(result);
    }
}
```