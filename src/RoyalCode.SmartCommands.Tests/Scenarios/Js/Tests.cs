using FluentAssertions;
using Microsoft.CodeAnalysis;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Js;

public class Tests
{
    [Theory]
    [InlineData(FindCode.Details, FindCode.ApiHandlers)]
    [InlineData(FindCodeWiths.Details, FindCodeWiths.ApiHandlers)]
    public void JsTests(string findCode, string apiHandlersCode)
    {
        Util.Compile(findCode, out var output, out var diagnostics);

        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedApiHandlers = output.SyntaxTrees.Skip(3).FirstOrDefault()?.ToString();
        generatedApiHandlers.Should().Be(apiHandlersCode);
    }
}

file class FindCode
{
    public const string Details =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using System.ComponentModel;
using System.Linq.Expressions;

namespace Tests.Scenarios.Js;

[MapGroup("produtos")]
[MapFind("{id:guid}", "Get product details"), EntityReference<Produto, Guid>]
public partial class ProdutoDetalhes
{
    public Guid Id { get; set; }

    public string Nome { get; set; }

    public bool Ativo { get; set; }
}

public class Produto : Entity<Guid>
{
    public Produto(string nome)
    {
        Nome = nome;
        Ativo = true;
    }

    public string Nome { get; set; }

    public bool Ativo { get; set; }

}

public class CreateSome
{
    public int Value { get; set; }

    [Command]
    internal Result Execute()
    {
        return Result.Ok();
    }
}

[MapApiHandlers]
public static partial class ProgramExtensions
{ }
""";

    public const string ApiHandlers =
"""
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartProblems.Entities;
using RoyalCode.SmartProblems.HttpResults;

namespace Tests.Scenarios.Js;

public static partial class MapProdutosApi
{
    public static RouteGroupBuilder MapProdutosGroup(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("produtos");

        group.MapGet("{id:guid}", FindProdutoHandleAsync)
            .WithName("Get product details")
            .WithOpenApi();

        return group;
    }

    [ProduceProblems(ProblemCategory.NotFound)]
    private static async Task<OkMatch<ProdutoDetalhes>> FindProdutoHandleAsync(
        Id<Produto, Guid> id, 
        IRepositoryAccessor<Produto> accessor, 
        CancellationToken ct)
    {
        var findResult = await accessor.FindEntityAsync<ProdutoDetalhes, Guid>(id, ct);
        if (findResult.NotFound(out var notfoundProblem))
            return notfoundProblem;

        return findResult.Entity;
    }
}

""";
}

file class FindCodeWiths
{
    public const string Details =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using System.ComponentModel;
using System.Linq.Expressions;

namespace Tests.Scenarios.Js;

[MapGroup("produtos")]
[MapFind("{id:guid}", "Get product details"), EntityReference<Produto, Guid>]
[WithDescription("Get product details by ID")]
[WithSummary("Find product")]
[WithPolicy("Admin")]
public partial class ProdutoDetalhes
{
    public Guid Id { get; set; }

    public string Nome { get; set; }

    public bool Ativo { get; set; }
}

public class Produto : Entity<Guid>
{
    public Produto(string nome)
    {
        Nome = nome;
        Ativo = true;
    }

    public string Nome { get; set; }

    public bool Ativo { get; set; }

}

public class CreateSome
{
    public int Value { get; set; }

    [Command]
    internal Result Execute()
    {
        return Result.Ok();
    }
}

[MapApiHandlers]
public static partial class ProgramExtensions
{ }
""";

    public const string ApiHandlers =
"""
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartProblems.Entities;
using RoyalCode.SmartProblems.HttpResults;

namespace Tests.Scenarios.Js;

public static partial class MapProdutosApi
{
    public static RouteGroupBuilder MapProdutosGroup(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("produtos");

        group.MapGet("{id:guid}", FindProdutoHandleAsync)
            .WithName("Get product details")
            .WithDescription("Get product details by ID")
            .WithSummary("Find product")
            .RequireAuthorization("Admin")
            .WithOpenApi();

        return group;
    }

    [ProduceProblems(ProblemCategory.NotFound)]
    private static async Task<OkMatch<ProdutoDetalhes>> FindProdutoHandleAsync(
        Id<Produto, Guid> id, 
        IRepositoryAccessor<Produto> accessor, 
        CancellationToken ct)
    {
        var findResult = await accessor.FindEntityAsync<ProdutoDetalhes, Guid>(id, ct);
        if (findResult.NotFound(out var notfoundProblem))
            return notfoundProblem;

        return findResult.Entity;
    }
}

""";
}
