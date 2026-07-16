using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using RoyalCode.SmartCommands.Generators.Generators;
using Xunit;

namespace RoyalCode.SmartCommands.Tests.Diagnostics;

/// <summary>
/// Robustez do generator diante de código incompleto (digitação em andamento) e confirmação de DF15:
/// nenhuma entrada malformada deve lançar (<c>CS8785</c>) e não há <see cref="DiagnosticAnalyzer"/> separado —
/// a semântica é lida uma única vez no transform.
/// </summary>
public class IncompleteCodeRobustnessTests
{
    private const string Usings =
        """
        using RoyalCode.SmartCommands;
        using RoyalCode.SmartProblems;

        namespace Tests.Incomplete;

        """;

    [Theory]
    // método sem corpo (parse incompleto)
    [InlineData("public class C { [Command] public Result Execute() }")]
    // corpo ausente e chave da classe não fechada
    [InlineData("public class C { [Command] public Result Execute() => Result.Ok();")]
    // tipo de retorno desconhecido
    [InlineData("public class C { [Command] public Missing Execute() => default!; }")]
    // tipo de parâmetro desconhecido
    [InlineData("public class C { [Command] public Result Execute(Missing value) => Result.Ok(); }")]
    // atributo Map com argumento não constante
    [InlineData("[MapPost(Unknown, \"x\")] public class C { [Command] public Result Execute() => Result.Ok(); }")]
    public void Incomplete_code_does_not_crash_the_generator(string body)
    {
        Util.Compile(Usings + body, out _, out var diagnostics);

        // CS8785 = exceção de generator; AD0001 = exceção de analyzer — nenhuma pode ocorrer.
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id is "CS8785" or "AD0001");

        // Enquanto a declaração está incompleta, não há informação semântica suficiente para atribuir ao usuário
        // um erro de uso do SmartCommands. Os erros de sintaxe/tipo permanecem sob responsabilidade do compilador.
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id.StartsWith("RCCMD", StringComparison.Ordinal));
    }

    [Fact]
    public void Generator_assembly_has_no_separate_diagnostic_analyzer()
    {
        var analyzers = typeof(IncrementalGenerator).Assembly
            .GetTypes()
            .Where(type => typeof(DiagnosticAnalyzer).IsAssignableFrom(type) && !type.IsAbstract)
            .ToArray();

        Assert.Empty(analyzers);
    }
}
