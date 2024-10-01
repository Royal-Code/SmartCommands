using System.Diagnostics.CodeAnalysis;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using RoyalCode.WorkContext.Abstractions;

namespace RoyalCode.SmartCommands.Tests.Models;

public partial class CriarProduto
{
    public string? Nome { get; set; }

    [MemberNotNullWhen(false, nameof(Nome))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        var result = RuleSet.For<CriarProduto>()
            .NotEmpty(Nome)
            .HasProblems(out problems);
    
        return result;
        //return result ? 1 : 0;
    }

    [Command, WithValidateModel, WithDecorators]
    internal async Task<Result<Guid>> ExecuteAsync(IWorkContext context, CancellationToken ct)
    {
        //WasValidated();

        var entity = new Produto(Nome);
        context.Add(entity);
        var save = await context.SaveAsync(ct);
        if (save.HasProblems(out var problems))
            return problems;

        return entity.Id;
    }
}