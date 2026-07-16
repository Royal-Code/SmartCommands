using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace RoyalCode.SmartCommands.Tests.Characterization;

/// <summary>
/// <para>
///     Testes de caracterização (Fase 1) para robustez do generator diante de entradas
///     ambíguas ou malformadas. Afirmam o comportamento-alvo (DF8, DF9 e "maps conflitantes"),
///     logo FALHAM no baseline pelo bug documentado. As Fases 2/3 devem torná-los verdes.
/// </para>
/// </summary>
[Trait("Category", "Characterization")]
public class GeneratorRobustnessCharacterizationTests
{
    private const string Usings =
        """
        using RoyalCode.SmartCommands;
        using RoyalCode.SmartProblems;
        using Microsoft.AspNetCore.Routing;

        namespace Tests.Scenarios.Robustness;

        """;

    // DF8: uma classe só pode ter um método [Command]; mais de um deve produzir diagnóstico,
    // nunca colisão de arquivo/tipo nem exceção do generator (CS8785).
    [Fact]
    public void MultipleCommandMethods_InSameClass_ShouldNotCollideNorCrash()
    {
        const string code = Usings +
            """
            public class DoTwoThings
            {
                [Command]
                public Result First() => Result.Ok();

                [Command]
                public Result Second() => Result.Ok();
            }
            """;

        Util.Compile(code, out var output, out var generatorDiagnostics);

        var generatorCrash = generatorDiagnostics.Where(d => d.Id == "CS8785").ToArray();
        var duplicateTypes = output.GetDiagnostics().Where(d => d.Id is "CS0101" or "CS0111").ToArray();
        var rccmdErrors = GeneratorErrors(generatorDiagnostics);
        AssertNoGeneratedSourceContains(output, "DoTwoThingsHandler", "IDoTwoThingsHandler");

        Assert.True(
            generatorCrash.Length == 0 && duplicateTypes.Length == 0 && rccmdErrors.Length > 0,
            $"Esperado diagnóstico RCCMD, nenhum crash do generator e nenhuma colisão de tipo. " +
            $"CS8785={generatorCrash.Length}, CS0101/CS0111={duplicateTypes.Length}. " +
            $"Diagnósticos: {Describe(generatorDiagnostics.Concat(duplicateTypes))}");
    }

    // "maps conflitantes": múltiplos atributos Map* na mesma classe são hoje resolvidos por
    // uma cadeia if/else if que escolhe o primeiro silenciosamente. Alvo: diagnosticar o conflito.
    [Fact]
    public void MultipleMapAttributes_OnSameClass_ShouldBeDiagnosed()
    {
        const string code = Usings +
            """
            [MapPost("/", "create-thing")]
            [MapPut("/{id}", "update-thing")]
            public class AmbiguousMappedThing
            {
                public string? Name { get; set; }

                [Command]
                public Result Do() => Result.Ok();
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var generatorDiagnostics);

        var rccmdErrors = GeneratorErrors(generatorDiagnostics);
        AssertNoGeneratedSourceContains(output, "create-thing", "update-thing");

        Assert.True(
            rccmdErrors.Length > 0,
            $"Esperado um diagnóstico RCCMD para atributos Map* conflitantes, mas nenhum foi reportado. " +
            $"Diagnósticos: {Describe(generatorDiagnostics)}");
    }

    // DF9: entrada malformada não pode derrubar o generator (CS8785); deve produzir RCCMD e nenhuma fonte relacionada.
    // Aqui, [MapPost] com um único argumento atinge o acesso não guardado Arguments[1].
    [Fact]
    public void MalformedMapAttribute_ShouldNotCrashGenerator()
    {
        const string code = Usings +
            """
            [MapPost("/only-one-argument")]
            public class MalformedMappedThing
            {
                [Command]
                public Result Do() => Result.Ok();
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var generatorDiagnostics);

        var generatorCrash = generatorDiagnostics.Where(d => d.Id == "CS8785").ToArray();
        var rccmdErrors = GeneratorErrors(generatorDiagnostics);
        AssertNoGeneratedSourceContains(output, "MalformedMappedThingHandler", "only-one-argument");

        Assert.True(
            generatorCrash.Length == 0 && rccmdErrors.Length > 0,
            $"Entrada malformada deve produzir RCCMD e não causar exceção do generator (CS8785). " +
            $"Diagnósticos: {Describe(generatorDiagnostics)}");
    }

    private static Diagnostic[] GeneratorErrors(System.Collections.Generic.IEnumerable<Diagnostic> diagnostics) =>
        diagnostics
            .Where(d => d.Id.StartsWith("RCCMD") && d.Severity == DiagnosticSeverity.Error)
            .ToArray();

    private static void AssertNoGeneratedSourceContains(Compilation output, params string[] forbiddenValues)
    {
        // Util.CreateCompilation cria uma Ãºnica Ã¡rvore de entrada; as demais foram adicionadas pelo generator.
        var generatedSources = output.SyntaxTrees.Skip(1).Select(tree => tree.ToString()).ToArray();
        foreach (var forbiddenValue in forbiddenValues)
        {
            Assert.DoesNotContain(generatedSources, source =>
                source.Contains(forbiddenValue, System.StringComparison.Ordinal));
        }
    }

    private static string Describe(System.Collections.Generic.IEnumerable<Diagnostic> diagnostics)
    {
        var items = diagnostics.Select(d => $"{d.Id}:{d.Severity}").ToArray();
        return items.Length == 0 ? "(nenhum)" : string.Join(", ", items);
    }
}
