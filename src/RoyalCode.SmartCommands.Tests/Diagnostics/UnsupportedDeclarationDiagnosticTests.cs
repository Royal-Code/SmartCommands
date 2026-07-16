using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace RoyalCode.SmartCommands.Tests.Diagnostics;

/// <summary>
/// Declarações não suportadas pela arquitetura gerada (<c>I{Classe}Handler</c>/<c>{Classe}Handler</c>): classe
/// aninhada ou file-local, método estático, abstrato ou inacessível ao handler. Cada uma produz RCCMD000
/// localizado e nenhuma fonte relacionada, sem exceção do generator.
/// </summary>
public class UnsupportedDeclarationDiagnosticTests
{
    private const string Usings =
        """
        using RoyalCode.SmartCommands;
        using RoyalCode.SmartProblems;

        namespace Tests.Unsupported;

        """;

    [Theory]
    [InlineData(
        """
        public class Outer
        {
            public class DoNested
            {
                [Command]
                public Result Execute() => Result.Ok();
            }
        }
        """,
        "top-level",
        "DoNestedHandler")]
    [InlineData(
        """
        public class DoStatic
        {
            [Command]
            public static Result Execute() => Result.Ok();
        }
        """,
        "instance method",
        "DoStaticHandler")]
    [InlineData(
        """
        public abstract class DoAbstract
        {
            [Command]
            public abstract Result Execute();
        }
        """,
        "abstract",
        "DoAbstractHandler")]
    [InlineData(
        """
        public class DoPrivate
        {
            [Command]
            private Result Execute() => Result.Ok();
        }
        """,
        "accessible",
        "DoPrivateHandler")]
    [InlineData(
        """
        file class DoFileLocal
        {
            [Command]
            public Result Execute() => Result.Ok();
        }
        """,
        "file-local",
        "DoFileLocalHandler")]
    public void Unsupported_declaration_reports_RCCMD000_and_emits_no_source(
        string body,
        string messageFragment,
        string forbiddenType)
    {
        Util.Compile(Usings + body, out var output, out var diagnostics);

        var errors = diagnostics.Where(d => d.Id == "RCCMD000").ToArray();
        Assert.Contains(errors, d => d.GetMessage().Contains(messageFragment, StringComparison.Ordinal));
        Assert.All(errors, d => Assert.NotEqual(Location.None, d.Location));
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(output.SyntaxTrees.Skip(1), tree => tree.ToString().Contains(forbiddenType));
    }

    [Fact]
    public void Top_level_internal_instance_method_is_supported()
    {
        const string code = Usings +
            """
            public class DoOk
            {
                [Command]
                internal Result Execute() => Result.Ok();
            }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Id == "RCCMD000");
        Assert.Contains(output.SyntaxTrees.Skip(1), tree => tree.ToString().Contains("IDoOkHandler"));
    }
}
