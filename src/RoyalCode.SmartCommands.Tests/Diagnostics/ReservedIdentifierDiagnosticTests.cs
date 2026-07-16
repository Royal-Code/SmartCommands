using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace RoyalCode.SmartCommands.Tests.Diagnostics;

/// <summary>
/// DF5: os nomes que o handler gerado emite no mesmo escopo (<c>command</c>, <c>ct</c>, <c>accessor</c>,
/// <c>decorators</c>, <c>retryOptions</c>, ...) são reservados; um parâmetro do comando que colida deve produzir
/// RCCMD029 e nenhuma fonte relacionada, sem o generator renomear silenciosamente.
/// </summary>
public class ReservedIdentifierDiagnosticTests
{
    private const string Usings =
        """
        using RoyalCode.SmartCommands;
        using RoyalCode.SmartProblems;
        using Microsoft.AspNetCore.Routing;
        using System.Threading;
        using System.Threading.Tasks;

        namespace Tests.Reserved;

        """;

    [Fact]
    public void WithParameter_named_command_collides_with_model_instance()
    {
        const string code = Usings +
            """
            public class DoThing
            {
                [Command]
                public Result Execute([WithParameter] string command) => Result.Ok();
            }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        var reserved = diagnostics.Where(d => d.Id == "RCCMD029").ToArray();
        Assert.Single(reserved);
        Assert.Equal(DiagnosticSeverity.Error, reserved[0].Severity);
        Assert.NotEqual(Location.None, reserved[0].Location);
        Assert.True(reserved[0].Location.GetLineSpan().IsValid);
        Assert.Contains("command", reserved[0].GetMessage(), System.StringComparison.Ordinal);

        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(output.SyntaxTrees.Skip(1), tree => tree.ToString().Contains("DoThingHandler"));
    }

    [Fact]
    public void DI_dependency_named_accessor_collides_under_unit_of_work()
    {
        const string code = Usings +
            """
            public class Db : Microsoft.EntityFrameworkCore.DbContext { }
            public interface IClock { }

            public class DoWork
            {
                [Command, WithUnitOfWork<Db>]
                public Result Execute(Db db, [WithParameter] IClock accessor) => Result.Ok();
            }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        var reserved = diagnostics.Where(d => d.Id == "RCCMD029").ToArray();
        Assert.Single(reserved);
        Assert.Contains("accessor", reserved[0].GetMessage(), System.StringComparison.Ordinal);
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(output.SyntaxTrees.Skip(1), tree => tree.ToString().Contains("DoWorkHandler"));
    }

    [Fact]
    public void CancellationToken_named_ct_is_not_a_collision()
    {
        const string code = Usings +
            """
            public class DoAsync
            {
                [Command]
                public async Task<Result> Execute(CancellationToken ct)
                {
                    await Task.CompletedTask;
                    return Result.Ok();
                }
            }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Id == "RCCMD029");
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains(output.SyntaxTrees.Skip(1), tree => tree.ToString().Contains("IDoAsyncHandler"));
    }

    [Theory]
    [InlineData("handler")]
    [InlineData("result")]
    public void WithParameter_collides_with_endpoint_scope_when_command_is_mapped(string parameterName)
    {
        var code = Usings +
            $$"""
            [MapPost("/", "do-mapped")]
            public class DoMapped
            {
                [Command]
                public Result Execute([WithParameter] string {{parameterName}}) => Result.Ok();
            }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        var reserved = diagnostics.Where(d => d.Id == "RCCMD029").ToArray();
        Assert.Single(reserved);
        Assert.Equal(DiagnosticSeverity.Error, reserved[0].Severity);
        Assert.NotEqual(Location.None, reserved[0].Location);
        Assert.Contains(parameterName, reserved[0].GetMessage(), System.StringComparison.Ordinal);
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(output.SyntaxTrees.Skip(1), tree => tree.ToString().Contains("DoMappedHandler"));
    }

    [Theory]
    [InlineData("handler")]
    [InlineData("result")]
    public void Endpoint_scope_names_are_not_reserved_without_mapping(string parameterName)
    {
        var code = Usings +
            $$"""
            public class DoLocal
            {
                [Command]
                public Result Execute([WithParameter] string {{parameterName}}) => Result.Ok();
            }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Id == "RCCMD029");
        Assert.Contains(output.SyntaxTrees.Skip(1), tree => tree.ToString().Contains("IDoLocalHandler"));
    }

    [Fact]
    public void WithParameter_collides_with_edit_entity_id_parameter_of_the_endpoint()
    {
        const string code = Usings +
            """
            public class Db : Microsoft.EntityFrameworkCore.DbContext { }
            public class Person { public int Id { get; set; } }

            [MapPut("/{personId}", "edit-person")]
            public class EditPerson
            {
                public int PersonId { get; set; }

                [Command, WithUnitOfWork<Db>, EditEntity<Person, int>]
                public Result Execute(Person person, [WithParameter] string personId) => Result.Ok();
            }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        var reserved = diagnostics.Where(d => d.Id == "RCCMD029").ToArray();
        Assert.Single(reserved);
        Assert.Contains("personId", reserved[0].GetMessage(), System.StringComparison.Ordinal);
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(output.SyntaxTrees.Skip(1), tree => tree.ToString().Contains("EditPersonHandler"));
    }

    [Fact]
    public void Non_reserved_parameter_name_does_not_trigger()
    {
        const string code = Usings +
            """
            public class DoWithInput
            {
                [Command]
                public Result Execute([WithParameter] string input) => Result.Ok();
            }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Id == "RCCMD029");
        Assert.Contains(output.SyntaxTrees.Skip(1), tree => tree.ToString().Contains("IDoWithInputHandler"));
    }
}
