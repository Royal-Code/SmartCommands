using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoyalCode.Extensions.SourceGenerator;
using RoyalCode.SmartCommands.Generators.Generators;
using Xunit;

namespace RoyalCode.SmartCommands.Tests.Generators;

/// <summary>
/// <para>
///     Fase 4: leitura semântica — aliases, nomes qualificados/global::, atributos com array explícito,
///     constantes referenciadas e métodos que retornam Task sem o modificador <c>async</c> devem produzir o
///     mesmo modelo correto. Cada fonte gerada é compilada dentro do teste (não apenas comparada como texto).
/// </para>
/// </summary>
public class SemanticGenerationTests
{
    private static void AssertOutputCompiles(Compilation output)
    {
        var errors = output.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.True(errors.Length == 0,
            "A compilação (incluindo as fontes geradas) deve ser válida. Erros: " +
            string.Join(Environment.NewLine, errors.Select(e => e.ToString())));
    }

    private static string[] GeneratedSources(Compilation output) =>
        output.SyntaxTrees.Skip(1).Select(tree => tree.ToString()).ToArray();

    [Fact]
    public void Alias_de_tipo_no_retorno_resolve_para_o_mesmo_modelo()
    {
        const string code =
            """
            using RoyalCode.SmartCommands;
            using MyResult = RoyalCode.SmartProblems.Result;
            using MyClock = Tests.Semantic.Alias.SystemClock;

            namespace Tests.Semantic.Alias;

            public sealed class SystemClock { }

            public class DoAliased
            {
                [Command]
                public MyResult Execute([WithParameter] MyClock clock) => MyResult.Ok();
            }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        AssertOutputCompiles(output);

        // o alias resolve para Result: a interface gerada usa o tipo real, não o texto do alias
        var generated = GeneratedSources(output);
        Assert.Contains(generated, source => source.Contains("public Result Handle(DoAliased command, SystemClock clock)"));
        Assert.DoesNotContain(generated, source => source.Contains("MyResult"));
        Assert.DoesNotContain(generated, source => source.Contains("MyClock"));
    }

    [Fact]
    public void Atributos_qualificados_e_global_sao_reconhecidos()
    {
        const string code =
            """
            using RoyalCode.SmartProblems;

            namespace Tests.Semantic.Qualified;

            public class DoQualified
            {
                [RoyalCode.SmartCommands.Command]
                [global::RoyalCode.SmartCommands.WithDecorators]
                public Result Execute() => Result.Ok();
            }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        AssertOutputCompiles(output);

        // WithDecorators qualificado foi reconhecido: o handler injeta e usa decorators
        var generated = GeneratedSources(output);
        Assert.Contains(generated, source => source.Contains("IDecorator<DoQualified, Result>"));
    }

    [Fact]
    public void Task_sem_modificador_async_e_detectada_semanticamente()
    {
        const string code =
            """
            using System.Threading.Tasks;
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Semantic.TaskLike;

            public class DoFromResult
            {
                [Command]
                public Task<Result> Execute() => Task.FromResult(Result.Ok());
            }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        AssertOutputCompiles(output);

        // o contrato assíncrono vem do tipo de retorno, não do modificador 'async'
        var generated = GeneratedSources(output);
        Assert.Contains(generated, source => source.Contains("Task<Result> HandleAsync(DoFromResult command, CancellationToken ct)"));
        Assert.Contains(generated, source => source.Contains("await command.Execute()"));
    }

    [Fact]
    public void ValueTask_normaliza_para_Task_no_contrato_do_handler()
    {
        const string code =
            """
            using System.Threading.Tasks;
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Semantic.ValueTasks;

            public class DoValueTask
            {
                [Command]
                public ValueTask<Result> Execute() => ValueTask.FromResult(Result.Ok());
            }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        AssertOutputCompiles(output);

        var generated = GeneratedSources(output);
        Assert.Contains(generated, source => source.Contains("Task<Result> HandleAsync(DoValueTask command, CancellationToken ct)"));
    }

    [Fact]
    public void WithPolicy_com_array_explicito_gera_o_mesmo_modelo()
    {
        const string code =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;
            using Microsoft.AspNetCore.Routing;

            namespace Tests.Semantic.Policies;

            [MapPost("/", "create-guarded"), WithPolicy(new string[] { "admin", "ops" })]
            public class DoGuarded
            {
                [Command]
                public Result Execute() => Result.Ok();
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        AssertOutputCompiles(output);

        var generated = GeneratedSources(output);
        Assert.Contains(generated, source => source.Contains(".RequireAuthorization(\"admin\", \"ops\")"));
    }

    [Fact]
    public void Constante_referenciada_no_atributo_resolve_o_valor_real()
    {
        const string code =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;
            using Microsoft.AspNetCore.Routing;

            namespace Tests.Semantic.Constants;

            public static class Routes
            {
                public const string Create = "/things";
            }

            [MapPost(Routes.Create, "create-thing")]
            public class CreateThing
            {
                [Command]
                public Result Execute() => Result.Ok();
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        AssertOutputCompiles(output);

        var generated = GeneratedSources(output);
        Assert.Contains(generated, source => source.Contains("MapPost(\"/things\""));
        Assert.Contains(generated, source => source.Contains(".WithName(\"create-thing\")"));
    }

    [Fact]
    public void Classes_homonimas_em_namespaces_diferentes_geram_fontes_distintas()
    {
        const string code =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Semantic.HomonymA
            {
                public class DoSame
                {
                    [Command]
                    public Result Execute() => Result.Ok();
                }
            }

            namespace Tests.Semantic.HomonymB
            {
                public class DoSame
                {
                    [Command]
                    public Result Execute() => Result.Ok();
                }
            }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        // sem colisão de hint name (CS8785) e sem erro; ambos os handlers existem
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        AssertOutputCompiles(output);

        var interfaceHints = output.SyntaxTrees
            .Skip(1)
            .Select(tree => Path.GetFileName(tree.FilePath))
            .Where(path => path.StartsWith("IDoSameHandler.", StringComparison.Ordinal)
                && path.EndsWith(".g.cs", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, interfaceHints.Length);
        Assert.Equal(2, interfaceHints.Distinct(StringComparer.Ordinal).Count());
        Assert.All(interfaceHints, hint => Assert.True(hint.Length <= 54, $"Hint name too long: {hint}"));
    }

    [Fact]
    public void Hint_name_has_bounded_length_and_uses_full_identity_for_uniqueness()
    {
        var readableName = new string('T', 200);
        var first = GeneratedHintName.Create($"Tests.Semantic.First.{readableName}", readableName);
        var second = GeneratedHintName.Create($"Tests.Semantic.Second.{readableName}", readableName);

        Assert.NotEqual(first, second);
        Assert.True(first.Length <= 46, $"Hint name too long: {first}");
        Assert.True(second.Length <= 46, $"Hint name too long: {second}");
        Assert.StartsWith($"{new string('T', 32)}.", first, StringComparison.Ordinal);
        Assert.Equal(8, first.Split('.')[1].Length);
        Assert.All(first.Split('.')[1], character =>
            Assert.True(character is >= 'A' and <= 'Z' or >= '2' and <= '7'));
        Assert.EndsWith(".g.cs", first, StringComparison.Ordinal);
        Assert.EndsWith(".g.cs", second, StringComparison.Ordinal);
    }

    [Fact]
    public void Filtro_em_declaracao_parcial_de_outro_arquivo_nao_derruba_o_generator()
    {
        // regressão apontada em revisão: o método [WithFilter] em outra árvore sintática (classe partial
        // em outro arquivo) não pode usar o semantic model da árvore do [MapSearch]
        const string fileA =
            """
            using RoyalCode.SmartCommands;

            namespace Tests.Semantic.PartialFilter;

            public class Ent
            {
                public int Id { get; set; }
            }

            public sealed class SystemClock { }

            [MapGroup("ents")]
            [MapSearch("/", "search-ents")]
            [SearchReference<Ent>]
            public partial class EntFilter
            {
                public int? Min { get; set; }
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        const string fileB =
            """
            using RoyalCode.SmartCommands;
            using Clock = Tests.Semantic.PartialFilter.SystemClock;

            namespace Tests.Semantic.PartialFilter;

            public partial class EntFilter
            {
                [WithFilter]
                internal void Configure([WithParameter] Clock clock) { }
            }
            """;

        var compilation = Util.CreateCompilation(fileA, fileB);
        var driver = CSharpGeneratorDriver.Create(new RoyalCode.SmartCommands.Generators.Generators.IncrementalGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        AssertOutputCompiles(output);

        // o filtro declarado no outro arquivo participa do endpoint gerado
        var generated = output.SyntaxTrees.Skip(2).Select(tree => tree.ToString()).ToArray();
        Assert.Contains(generated, source => source.Contains("\"search-ents\""));
        Assert.Contains(generated, source => source.Contains("SystemClock clock"));
        Assert.Contains(generated, source => source.Contains("filter.Configure(clock)"));
        Assert.DoesNotContain(generated, source => source.Contains(" Clock clock"));
    }

    [Fact]
    public void Metadados_auxiliares_nulos_produzem_diagnostico_e_nao_geram_endpoint()
    {
        const string code =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;
            using Microsoft.AspNetCore.Routing;

            namespace Tests.Semantic.NullMetadata;

            [MapGroup(null!), MapPost("/", "null-group")]
            public class NullGroup
            {
                [Command]
                public Result Execute() => Result.Ok();
            }

            [MapPost("/", "null-policies"), WithPolicy(null!)]
            public class NullPolicies
            {
                [Command]
                public Result Execute() => Result.Ok();
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.Equal(2, diagnostics.Count(d => d.Id == "RCCMD041"));
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(output.SyntaxTrees.Skip(1), tree => tree.ToString().Contains("null-group"));
        Assert.DoesNotContain(output.SyntaxTrees.Skip(1), tree => tree.ToString().Contains("null-policies"));
    }

    [Fact]
    public void MapCreatedRoute_com_caractere_de_controle_emite_string_csharp_valida()
    {
        const string code =
            """
            using RoyalCode.SmartCommands;
            using Microsoft.AspNetCore.Routing;

            namespace Tests.Semantic.CreatedRoute;

            public sealed class Thing
            {
                public int Id { get; set; }
            }

            [MapGroup("things")]
            [MapPost("/", "create-thing")]
            [MapCreatedRoute("/created/{id}\nnext", nameof(Thing.Id))]
            public class CreateThing
            {
                [Command]
                public Thing Execute() => new() { Id = 1 };
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        AssertOutputCompiles(output);
        Assert.Contains(GeneratedSources(output), source => source.Contains("\\nnext"));
    }

    [Fact]
    public void Localizacao_de_argumento_nomeado_respeita_nome_do_parametro_do_construtor()
    {
        const string code =
            """
            using System;

            public class Target
            {
                [Obsolete(error: true, message: "reason")]
                public void Execute() { }
            }
            """;

        var compilation = Util.CreateCompilation(code);
        var tree = compilation.SyntaxTrees.Single();
        var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var model = compilation.GetSemanticModel(tree);
        var symbol = (IMethodSymbol)model.GetDeclaredSymbol(method)!;
        var attribute = symbol.GetAttributes().Single();
        var arguments = method.AttributeLists.Single().Attributes.Single().ArgumentList!.Arguments;
        var messageArgument = arguments.Single(argument => argument.NameColon?.Name.Identifier.ValueText == "message");
        var errorArgument = arguments.Single(argument => argument.NameColon?.Name.Identifier.ValueText == "error");

        var messageLocation = KnownAttributes.GetArgumentLocation(attribute, 0, default, method.GetLocation());
        var errorLocation = KnownAttributes.GetArgumentLocation(attribute, 1, default, method.GetLocation());

        Assert.Equal(messageArgument.Span, messageLocation.SourceSpan);
        Assert.Equal(errorArgument.Span, errorLocation.SourceSpan);
    }

    [Fact]
    public void Endpoint_name_nulo_constante_produz_diagnostico_em_vez_de_sumir_em_silencio()
    {
        // 'null' é constante válida para o compilador; sem RCCMD o endpoint desapareceria sem aviso
        const string code =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;
            using Microsoft.AspNetCore.Routing;

            namespace Tests.Semantic.Nulls;

            [MapPost(null!, "create-null")]
            public class DoWithNullRoute
            {
                [Command]
                public Result Execute() => Result.Ok();
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD028" && d.Severity == DiagnosticSeverity.Error);
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(output.SyntaxTrees.Skip(1), tree => tree.ToString().Contains("create-null"));
    }

    [Fact]
    public void Poco_de_resposta_recebe_o_cabecalho_de_arquivo_gerado()
    {
        const string code =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;
            using Microsoft.AspNetCore.Routing;

            namespace Tests.Semantic.Responses;

            public class Thing
            {
                public int Id { get; set; }
                public string? Name { get; set; }
            }

            [MapPost("/", "create-with-response"), MapResponseValues(nameof(Thing.Id), nameof(Thing.Name))]
            public class CreateWithResponse
            {
                public string? Name { get; set; }

                [Command]
                public Thing Execute() => new Thing { Id = 1, Name = Name };
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        AssertOutputCompiles(output);

        var responseSource = output.SyntaxTrees
            .FirstOrDefault(tree =>
                Path.GetFileName(tree.FilePath).StartsWith("CreateWithResponseResponse.", StringComparison.Ordinal)
                && tree.FilePath.EndsWith(".g.cs", StringComparison.Ordinal))
            ?.ToString();

        Assert.NotNull(responseSource);
        Assert.StartsWith("// <auto-generated/>", responseSource);
        Assert.Contains("#nullable enable", responseSource);
    }
}

/// <summary>
/// Fase 4: parser único de route pattern — nomes, constraint, catch-all, optional, default e escapes.
/// </summary>
public class RoutePatternParserTests
{
    [Theory]
    [InlineData("/", null)]
    [InlineData("/items", null)]
    [InlineData("/{id}", "id")]
    [InlineData("/{id:int}", "id")]
    [InlineData("/{id?}", "id")]
    [InlineData("/{id:int?}", "id")]
    [InlineData("/{id=42}", "id")]
    [InlineData("/{id:int=42}", "id")]
    [InlineData("/{*slug}", "slug")]
    [InlineData("/{**catchAll}", "catchAll")]
    [InlineData("/literal/{{escaped}}/{real}", "real")]
    [InlineData("/{code:regex(^a=b$)}", "code")]
    public void FirstParameterName_extrai_o_primeiro_parametro(string pattern, string? expected)
    {
        Assert.Equal(expected, RoutePatternParser.FirstParameterName(pattern));
    }

    [Fact]
    public void Parse_extrai_constraint_default_optional_e_catchall()
    {
        var parameters = RoutePatternParser.Parse("/{movieId:int}/reviews/{page:int=1}/{slug?}/{**rest}");

        Assert.Equal(4, parameters.Count);

        Assert.Equal("movieId", parameters[0].Name);
        Assert.Equal("int", parameters[0].Constraint);
        Assert.False(parameters[0].IsOptional);

        Assert.Equal("page", parameters[1].Name);
        Assert.Equal("int", parameters[1].Constraint);
        Assert.Equal("1", parameters[1].DefaultValue);

        Assert.Equal("slug", parameters[2].Name);
        Assert.True(parameters[2].IsOptional);

        Assert.Equal("rest", parameters[3].Name);
        Assert.True(parameters[3].IsCatchAll);
    }
}
