using FluentAssertions;
using Microsoft.CodeAnalysis;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Js;

public class Tests
{
    [Theory]
    [InlineData(CodeJs.Find1, CodeJs.ApiHandlers1)]
    public void JsTests(string findCode, string apiHandlersCode)
    {
        Util.Compile(findCode, out var output, out var diagnostics);

        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedApiHandlers = output.SyntaxTrees.Skip(3).FirstOrDefault()?.ToString();
        generatedApiHandlers.Should().Be(apiHandlersCode);
    }
}

file class CodeJs
{
    public const string Find1 =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using System.ComponentModel;
using System.Linq.Expressions;

namespace Tests.Scenarios.Js;

[MapGroup("produtos")]
[MapFind("{id:guid}", "Get product details"), EntityReference<Produto, Guid>]
[Description("Get product details by ID")]
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

    public const string ApiHandlers1 =
"""
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RoyalCode.SmartProblems.HttpResults;

namespace Tests.Scenarios.Js;

public static partial class MapProdutosApi
{
    public static RouteGroupBuilder MapProdutosGroup(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("produtos");

        group.MapGet("{id:guid}", FindProdutoHandleAsync)
            .WithName("create some")
            .WithOpenApi();

        return group;
    }

    private static Task<OkMatch<Produto>> FindProdutoHandleAsync(
        ICreateSomeHandler handler, 
        CreateSome command)
    {
        var result = handler.Handle(command);
        return result;
    }
}

""";
}