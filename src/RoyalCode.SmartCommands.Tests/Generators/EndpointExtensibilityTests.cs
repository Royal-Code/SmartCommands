using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace RoyalCode.SmartCommands.Tests.Generators;

/// <summary>
/// <para>
///     Fase 10 (DF23): extensibilidade HTTP comum — <c>[WithEndpointFilter&lt;T&gt;]</c> repetível na ordem
///     declarada, <c>[WithResultStatus]</c> (Ok/Created/NoContent) somente em command maps e
///     <c>[WithTags]</c> nas três superfícies; conflitos e usos inválidos produzem RCCMD051-053 sem fonte.
/// </para>
/// </summary>
public class EndpointExtensibilityTests
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

    private const string FilterTypes =
        """
        public class AuditFilter : Microsoft.AspNetCore.Http.IEndpointFilter
        {
            public ValueTask<object?> InvokeAsync(
                Microsoft.AspNetCore.Http.EndpointFilterInvocationContext context,
                Microsoft.AspNetCore.Http.EndpointFilterDelegate next) => next(context);
        }

        public class TenantFilter : Microsoft.AspNetCore.Http.IEndpointFilter
        {
            public ValueTask<object?> InvokeAsync(
                Microsoft.AspNetCore.Http.EndpointFilterInvocationContext context,
                Microsoft.AspNetCore.Http.EndpointFilterDelegate next) => next(context);
        }

        """;

    // ------------------------------------------------------------------
    // WithEndpointFilter<T>
    // ------------------------------------------------------------------

    [Fact]
    public void Filtros_repetidos_sao_emitidos_na_ordem_declarada_com_nome_qualificado()
    {
        const string code =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Phase10.Filters;

            """ + FilterTypes +
            """
            [MapGroup("things")]
            [MapPost("/", "create-thing")]
            [WithEndpointFilter<AuditFilter>]
            [WithEndpointFilter<TenantFilter>]
            public class CreateThing
            {
                public int Value { get; set; }

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
        var api = Assert.Single(generated, source => source.Contains("AddEndpointFilter"));
        var audit = api.IndexOf(".AddEndpointFilter<global::Tests.Phase10.Filters.AuditFilter>()", StringComparison.Ordinal);
        var tenant = api.IndexOf(".AddEndpointFilter<global::Tests.Phase10.Filters.TenantFilter>()", StringComparison.Ordinal);
        Assert.True(audit >= 0 && tenant >= 0, "os dois filtros devem ser emitidos");
        Assert.True(audit < tenant, "a ordem declarada deve ser preservada");
    }

    [Fact]
    public void Filtros_valem_para_Find_e_Search()
    {
        const string code =
            """
            using System;
            using RoyalCode.SmartCommands;
            using RoyalCode.Entities;

            namespace Tests.Phase10.FindSearchFilters;

            """ + FilterTypes +
            """
            public class Produto : Entity<Guid>
            {
                public string? Nome { get; set; }
            }

            [MapGroup("produtos")]
            [MapFind("{id:guid}", "find-produto"), EntityReference<Produto, Guid>]
            [WithEndpointFilter<AuditFilter>]
            public class ProdutoDetalhes
            {
                public Guid Id { get; set; }
            }

            [MapGroup("produtos")]
            [MapSearch("", "search-produtos"), SearchReference<Produto>]
            [WithEndpointFilter<TenantFilter>]
            public class ProdutoFiltro
            {
                public string? Nome { get; set; }
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        AssertOutputCompiles(output);

        var generated = GeneratedSources(output);
        Assert.Contains(generated, source =>
            source.Contains(".AddEndpointFilter<global::Tests.Phase10.FindSearchFilters.AuditFilter>()") &&
            source.Contains(".AddEndpointFilter<global::Tests.Phase10.FindSearchFilters.TenantFilter>()"));
    }

    [Fact]
    public void Filtro_generico_construido_e_filtro_aninhado_acessivel_sao_aceitos()
    {
        const string code =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Phase10.FilterShapes;

            public sealed class GenericFilter<T> : Microsoft.AspNetCore.Http.IEndpointFilter
            {
                public ValueTask<object?> InvokeAsync(
                    Microsoft.AspNetCore.Http.EndpointFilterInvocationContext context,
                    Microsoft.AspNetCore.Http.EndpointFilterDelegate next) => next(context);
            }

            public static class FilterContainer
            {
                public sealed class NestedFilter : Microsoft.AspNetCore.Http.IEndpointFilter
                {
                    public ValueTask<object?> InvokeAsync(
                        Microsoft.AspNetCore.Http.EndpointFilterInvocationContext context,
                        Microsoft.AspNetCore.Http.EndpointFilterDelegate next) => next(context);
                }
            }

            [MapPost("/things", "create-thing")]
            [WithEndpointFilter<GenericFilter<string>>]
            [WithEndpointFilter<FilterContainer.NestedFilter>]
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

        var generated = Assert.Single(GeneratedSources(output), source => source.Contains("AddEndpointFilter"));
        Assert.Contains(
            ".AddEndpointFilter<global::Tests.Phase10.FilterShapes.GenericFilter<",
            generated,
            StringComparison.Ordinal);
        Assert.Contains("FilterContainer.NestedFilter", generated, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("public class BadFilter { }", "must implement Microsoft.AspNetCore.Http.IEndpointFilter")]
    [InlineData(
        """
        public abstract class BadFilter : Microsoft.AspNetCore.Http.IEndpointFilter
        {
            public ValueTask<object?> InvokeAsync(
                Microsoft.AspNetCore.Http.EndpointFilterInvocationContext context,
                Microsoft.AspNetCore.Http.EndpointFilterDelegate next) => next(context);
        }
        """,
        "must not be abstract")]
    [InlineData("public struct BadFilter { }", "must be a class")]
    public void Filtro_invalido_produz_RCCMD051(string filterDeclaration, string reasonFragment)
    {
        var code =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Phase10.BadFilter;

            """ + filterDeclaration +
            """


            [MapPost("/things", "create-thing")]
            [WithEndpointFilter<BadFilter>]
            public class CreateThing
            {
                public int Value { get; set; }

                [Command]
                public Result Execute() => Result.Ok();
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD051" &&
            d.GetMessage().Contains(reasonFragment, StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(GeneratedSources(output), source => source.Contains("MapPost("));
    }

    // ------------------------------------------------------------------
    // WithTags
    // ------------------------------------------------------------------

    [Fact]
    public void Tags_sao_emitidas_na_ordem_declarada_nas_tres_superficies()
    {
        const string code =
            """
            using System;
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;
            using RoyalCode.Entities;

            namespace Tests.Phase10.Tags;

            public class Produto : Entity<Guid>
            {
                public string? Nome { get; set; }
            }

            [MapGroup("produtos")]
            [MapPost("/", "create-produto")]
            [WithTags("Produtos", "Catalogo")]
            public class CreateProduto
            {
                public int Value { get; set; }

                [Command]
                public Result Execute() => Result.Ok();
            }

            [MapGroup("produtos")]
            [MapFind("{id:guid}", "find-produto"), EntityReference<Produto, Guid>]
            [WithTags("Produtos")]
            public class ProdutoDetalhes
            {
                public Guid Id { get; set; }
            }

            [MapGroup("produtos")]
            [MapSearch("", "search-produtos"), SearchReference<Produto>]
            [WithTags("Busca")]
            public class ProdutoFiltro
            {
                public string? Nome { get; set; }
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        AssertOutputCompiles(output);

        var generated = GeneratedSources(output);
        Assert.Contains(generated, source =>
            source.Contains(".WithTags(\"Produtos\", \"Catalogo\")") &&
            source.Contains(".WithTags(\"Produtos\")") &&
            source.Contains(".WithTags(\"Busca\")"));
    }

    [Theory]
    [InlineData("[WithTags]", "at least one tag")]
    [InlineData("[WithTags(\"ok\", \"  \")]", "not empty or whitespace")]
    public void Tags_invalidas_produzem_RCCMD041(string tagsAttribute, string reasonFragment)
    {
        var code =
            $$"""
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Phase10.BadTags;

            [MapPost("/things", "create-thing")]
            {{tagsAttribute}}
            public class CreateThing
            {
                public int Value { get; set; }

                [Command]
                public Result Execute() => Result.Ok();
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD041" &&
            d.GetMessage().Contains("WithTags", StringComparison.Ordinal) &&
            d.GetMessage().Contains(reasonFragment, StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
    }

    // ------------------------------------------------------------------
    // WithResultStatus
    // ------------------------------------------------------------------

    [Fact]
    public void Ok_explicito_evita_a_inferencia_de_NoContent_no_Delete()
    {
        const string code =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Phase10.OkStatus;

            [MapDelete("/things/{id:int}", "delete-thing")]
            [WithResultStatus(HttpResultStatus.Ok)]
            public class DeleteThing
            {
                [Command]
                public Result Execute([WithParameter] int id) => Result.Ok();
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        AssertOutputCompiles(output);

        var generated = GeneratedSources(output);
        Assert.Contains(generated, source => source.Contains("OkMatch"));
        Assert.DoesNotContain(generated, source => source.Contains("NoContentMatch"));
    }

    [Fact]
    public void NoContent_explicito_descarta_o_valor_e_anuncia_204()
    {
        const string code =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Phase10.NoContentStatus;

            public sealed class Thing
            {
                public int Id { get; set; }
            }

            [MapPost("/things", "create-thing")]
            [WithResultStatus(HttpResultStatus.NoContent)]
            public class CreateThing
            {
                public int Value { get; set; }

                [Command]
                public Result<Thing> Execute() => new Thing { Id = 1 };
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        AssertOutputCompiles(output);

        var generated = GeneratedSources(output);
        // o valor de sucesso é descartado deliberadamente (Result<T> -> Result); problemas preservados
        Assert.Contains(generated, source =>
            source.Contains("NoContentMatch") &&
            source.Contains("return (Result)result;") &&
            source.Contains(".Produces(204)"));
    }

    [Fact]
    public void Created_explicito_sem_MapCreatedRoute_responde_201_sem_Location()
    {
        const string code =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Phase10.CreatedStatus;

            public sealed class Thing
            {
                public int Id { get; set; }
            }

            [MapPost("/things", "create-thing")]
            [WithResultStatus(HttpResultStatus.Created)]
            public class CreateThing
            {
                public int Value { get; set; }

                [Command]
                public Result<Thing> Execute() => new Thing { Id = 1 };
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        AssertOutputCompiles(output);

        var generated = GeneratedSources(output);
        Assert.Contains(generated, source =>
            source.Contains("CreatedMatch<Thing>") &&
            source.Contains("TypedResults.Created((string?)null, value)"));
    }

    [Fact]
    public void Created_explicito_sem_valor_usa_o_CreatedMatch_nao_generico()
    {
        const string code =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Phase10.CreatedNoValue;

            [MapPost("/things", "create-thing")]
            [WithResultStatus(HttpResultStatus.Created)]
            public class CreateThing
            {
                public int Value { get; set; }

                [Command]
                public Result Execute() => Result.Ok();
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        AssertOutputCompiles(output);

        Assert.Contains(GeneratedSources(output), source =>
            source.Contains("static CreatedMatch CreateThingHandle(") &&
            source.Contains("TypedResults.Created()"));
    }

    [Fact]
    public void Created_explicito_com_MapIdResultValue_projeta_o_id_sem_Location()
    {
        const string code =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Phase10.CreatedWithId;

            public sealed class Thing
            {
                public int Id { get; set; }
            }

            [MapPost("/things", "create-thing")]
            [WithResultStatus(HttpResultStatus.Created), MapIdResultValue]
            public class CreateThing
            {
                public int Value { get; set; }

                [Command]
                public Result<Thing> Execute() => new Thing { Id = 1 };
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        AssertOutputCompiles(output);

        Assert.Contains(GeneratedSources(output), source =>
            source.Contains("CreatedMatch<int>") &&
            source.Contains("result.Map(v => v.Id).Match<IResult>"));
    }

    [Fact]
    public void Created_explicito_com_MapCreatedRoute_mantem_a_Location()
    {
        const string code =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Phase10.CreatedRedundant;

            public sealed class Thing
            {
                public int Id { get; set; }
            }

            [MapGroup("things")]
            [MapPost("/", "create-thing")]
            [WithResultStatus(HttpResultStatus.Created)]
            [MapCreatedRoute("{id}", nameof(Thing.Id))]
            public class CreateThing
            {
                public int Value { get; set; }

                [Command]
                public Result<Thing> Execute() => new Thing { Id = 1 };
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        AssertOutputCompiles(output);

        Assert.Contains(GeneratedSources(output), source =>
            source.Contains("CreatedMatch(v => $\"things/{v.Id}\")"));
    }

    [Theory]
    [InlineData("HttpResultStatus.Ok", "MapCreatedRoute")]
    [InlineData("HttpResultStatus.NoContent", "MapCreatedRoute")]
    public void Status_explicito_conflita_com_MapCreatedRoute_produz_RCCMD053(string status, string conflictFragment)
    {
        var code =
            $$"""
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Phase10.StatusConflict;

            public sealed class Thing
            {
                public int Id { get; set; }
            }

            [MapGroup("things")]
            [MapPost("/", "create-thing")]
            [WithResultStatus({{status}})]
            [MapCreatedRoute("{id}", nameof(Thing.Id))]
            public class CreateThing
            {
                public int Value { get; set; }

                [Command]
                public Result<Thing> Execute() => new Thing { Id = 1 };
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD053" &&
            d.GetMessage().Contains(conflictFragment, StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(GeneratedSources(output), source => source.Contains("MapPost("));
    }

    [Fact]
    public void NoContent_com_MapResponseValues_produz_RCCMD053()
    {
        const string code =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Phase10.NoContentBody;

            public sealed class Thing
            {
                public int Id { get; set; }
            }

            [MapPost("/things", "create-thing")]
            [WithResultStatus(HttpResultStatus.NoContent), MapResponseValues(nameof(Thing.Id))]
            public class CreateThing
            {
                public int Value { get; set; }

                [Command]
                public Result<Thing> Execute() => new Thing { Id = 1 };
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD053" &&
            d.GetMessage().Contains("MapResponseValues", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
    }

    [Fact]
    public void WithResultStatus_em_MapFind_e_MapSearch_produz_RCCMD052()
    {
        const string code =
            """
            using System;
            using RoyalCode.SmartCommands;
            using RoyalCode.Entities;

            namespace Tests.Phase10.StatusSurface;

            public class Produto : Entity<Guid>
            {
                public string? Nome { get; set; }
            }

            [MapGroup("produtos")]
            [MapFind("{id:guid}", "find-produto"), EntityReference<Produto, Guid>]
            [WithResultStatus(HttpResultStatus.Ok)]
            public class ProdutoDetalhes
            {
                public Guid Id { get; set; }
            }

            [MapGroup("produtos")]
            [MapSearch("", "search-produtos"), SearchReference<Produto>]
            [WithResultStatus(HttpResultStatus.Ok)]
            public class ProdutoFiltro
            {
                public string? Nome { get; set; }
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.Equal(2, diagnostics.Count(d => d.Id == "RCCMD052" &&
            d.GetMessage().Contains("command maps", StringComparison.Ordinal)));
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(GeneratedSources(output), source => source.Contains("MapGet("));
    }

    [Fact]
    public void Valor_desconhecido_de_HttpResultStatus_produz_RCCMD052()
    {
        const string code =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Phase10.UnknownStatus;

            [MapPost("/things", "create-thing")]
            [WithResultStatus((HttpResultStatus)999)]
            public class CreateThing
            {
                public int Value { get; set; }

                [Command]
                public Result Execute() => Result.Ok();
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD052" &&
            d.GetMessage().Contains("not a known HttpResultStatus", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
    }

    [Fact]
    public void Sem_atributos_da_fase_a_inferencia_e_a_emissao_permanecem_identicas()
    {
        const string code =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Phase10.Baseline;

            [MapDelete("/things/{id:int}", "delete-thing")]
            public class DeleteThing
            {
                [Command]
                public Result Execute([WithParameter] int id) => Result.Ok();
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        AssertOutputCompiles(output);

        var generated = GeneratedSources(output);
        Assert.Contains(generated, source => source.Contains("NoContentMatch"));
        Assert.DoesNotContain(generated, source => source.Contains("WithTags"));
        Assert.DoesNotContain(generated, source => source.Contains("AddEndpointFilter"));
    }
}
