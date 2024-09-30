using RoyalCode.SmartProblems;
using RoyalCode.WorkContext.Abstractions;

namespace Coreum.NewCommands.Tests.Models;

public interface IPipelineCriarProdutoHandler
{
    public Task<Result<Guid>> HandleAsync(CriarProduto model, CancellationToken ct);
}

public class PipelineCriarProdutoHandler : IPipelineCriarProdutoHandler
{
    private readonly IWorkContext context;
    private readonly IEnumerable<IDecorator<CriarProduto, Result<Guid>>> decorators;

    public PipelineCriarProdutoHandler(
        IWorkContext context,
        IEnumerable<IDecorator<CriarProduto, Result<Guid>>> decorators)
    {
        this.context = context;
        this.decorators = decorators;
    }

    public async Task<Result<Guid>> HandleAsync(CriarProduto model, CancellationToken ct)
    {
        if (model.HasProblems(out var validationProblems))
            return validationProblems;

        var mediator = new Mediator<CriarProduto, Result<Guid>>(
            decorators,
            async () => await model.ExecuteAsync(context, ct),
            model,
            ct);

        return await mediator.NextAsync();
    }

    public async Task<Result<Guid>> SemDecoratorsAsync(CriarProduto model, CancellationToken ct)
    {
        if (model.HasProblems(out var validationProblems))
            return validationProblems;

        return await model.ExecuteAsync(context, ct);
    }
}
