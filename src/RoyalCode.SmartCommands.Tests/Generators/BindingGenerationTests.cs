using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace RoyalCode.SmartCommands.Tests.Generators;

/// <summary>
/// <para>
///     Fase 5 (DF2/DF3): atributos de binding do parâmetro-fonte (<c>[WithParameter]</c> de comandos e
///     parâmetros de filtros do Search) são copiados somente para o delegate Minimal API; sem atributo, o
///     ASP.NET Core infere a fonte. Fontes conflitantes (RCCMD033), <c>[AsParameters]</c> (RCCMD034),
///     <c>FromRoute</c> fora do template (RCCMD035) e body implícito em GET/DELETE (RCCMD036) são erros.
/// </para>
/// </summary>
public class BindingGenerationTests
{
    private const string Usings =
        """
        using RoyalCode.SmartCommands;
        using RoyalCode.SmartProblems;
        using Microsoft.AspNetCore.Http;
        using Microsoft.AspNetCore.Mvc;
        using Microsoft.AspNetCore.Routing;

        namespace Tests.Bindings;

        """;

    private const string Host =
        """

        [MapApiHandlers]
        public static partial class Endpoints { }
        """;

    private static string[] GeneratedSources(Compilation output) =>
        output.SyntaxTrees.Skip(1).Select(tree => tree.ToString()).ToArray();

    [Theory]
    [InlineData("[WithParameter, FromQuery] string? origem", "[FromQuery]")]
    [InlineData("[WithParameter, FromQuery(Name = \"o\")] string? origem", "[FromQuery(Name = \"o\")]")]
    [InlineData("[WithParameter, FromHeader(Name = \"x-user\")] string? origem", "[FromHeader(Name = \"x-user\")]")]
    [InlineData("[WithParameter, FromServices] object origem", "[FromServices]")]
    public void Binding_explicito_e_copiado_para_o_delegate_e_nao_para_a_interface(string parameter, string expected)
    {
        var code = Usings +
            $$"""
            [MapPost("/", "do-bound")]
            public class DoBound
            {
                [Command]
                public Result Execute({{parameter}}) => Result.Ok();
            }
            """ + Host;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        var generated = GeneratedSources(output);
        var endpoint = generated.First(source => source.Contains("MapEndpointsApi"));
        Assert.Contains(expected, endpoint);

        // DF3: o binding não vai para a interface do handler
        var handlerInterface = generated.First(source => source.Contains("IDoBoundHandler"));
        Assert.DoesNotContain("From", handlerInterface);
    }

    [Fact]
    public void Fontes_conflitantes_produzem_RCCMD033_e_nenhuma_fonte()
    {
        var code = Usings +
            """
            [MapPost("/", "do-conflicted")]
            public class DoConflicted
            {
                [Command]
                public Result Execute([WithParameter, FromQuery, FromHeader(Name = "x")] string valor) => Result.Ok();
            }
            """ + Host;

        Util.Compile(code, out var output, out var diagnostics);

        var conflicts = diagnostics.Where(d => d.Id == "RCCMD033").ToArray();
        Assert.Single(conflicts);
        Assert.Equal(DiagnosticSeverity.Error, conflicts[0].Severity);
        Assert.NotEqual(Location.None, conflicts[0].Location);
        Assert.Contains("valor", conflicts[0].GetMessage(), StringComparison.Ordinal);
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(output.SyntaxTrees.Skip(1), tree => tree.ToString().Contains("DoConflictedHandler"));
    }

    [Fact]
    public void AsParameters_produz_RCCMD034()
    {
        var code = Usings +
            """
            public class Filtro { public string? Nome { get; set; } }

            [MapPost("/", "do-asparameters")]
            public class DoAsParameters
            {
                [Command]
                public Result Execute([WithParameter, AsParameters] Filtro filtro) => Result.Ok();
            }
            """ + Host;

        Util.Compile(code, out _, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD034" && d.Severity == DiagnosticSeverity.Error);
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
    }

    [Fact]
    public void FromRoute_com_nome_fora_do_template_produz_RCCMD035()
    {
        var code = Usings +
            """
            [MapPost("/", "do-noroute")]
            public class DoNoRoute
            {
                [Command]
                public Result Execute([WithParameter, FromRoute(Name = "nope")] int valor) => Result.Ok();
            }
            """ + Host;

        Util.Compile(code, out _, out var diagnostics);

        var missing = diagnostics.Where(d => d.Id == "RCCMD035").ToArray();
        Assert.Single(missing);
        Assert.Contains("nope", missing[0].GetMessage(), StringComparison.Ordinal);
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
    }

    [Fact]
    public void FromRoute_pode_apontar_para_variavel_do_grupo()
    {
        var code = Usings +
            """
            [MapGroup("tenants/{tenant}")]
            [MapPost("/", "do-tenant")]
            public class DoTenant
            {
                [Command]
                public Result Execute([WithParameter, FromRoute(Name = "tenant")] string tenant) => Result.Ok();
            }
            """ + Host;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains(GeneratedSources(output), source => source.Contains("[FromRoute(Name = \"tenant\")]"));
    }

    [Theory]
    [InlineData("MapGet")]
    [InlineData("MapDelete")]
    public void Body_implicito_em_GET_e_DELETE_produz_RCCMD036_e_nenhuma_fonte(string mapAttribute)
    {
        var code = Usings +
            $$"""
            [{{mapAttribute}}("/", "with-body")]
            public class WithBody
            {
                public string? Nome { get; set; }

                [Command]
                public Result Execute() => Result.Ok();
            }
            """ + Host;

        Util.Compile(code, out var output, out var diagnostics);

        var implicitBody = diagnostics.Where(d => d.Id == "RCCMD036").ToArray();
        Assert.Single(implicitBody);
        Assert.Contains(mapAttribute, implicitBody[0].GetMessage(), StringComparison.Ordinal);
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(output.SyntaxTrees.Skip(1), tree => tree.ToString().Contains("\"with-body\""));
    }

    [Fact]
    public void GET_com_BindAsync_customizado_nao_produz_RCCMD036()
    {
        // o ASP.NET Core usa o BindAsync do próprio tipo; não há body inferido
        var code = Usings +
            """
            [MapGet("/", "get-bindable")]
            public class GetBindable
            {
                public string? Nome { get; set; }

                public static ValueTask<GetBindable?> BindAsync(HttpContext context, System.Reflection.ParameterInfo parameter)
                    => ValueTask.FromResult<GetBindable?>(new GetBindable());

                [Command]
                public Result Execute() => Result.Ok();
            }
            """ + Host;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Id == "RCCMD036");
        Assert.Contains(GeneratedSources(output), source => source.Contains("\"get-bindable\""));
    }

    [Fact]
    public void FromBody_em_WithParameter_conflita_com_o_body_do_comando_RCCMD037()
    {
        var code = Usings +
            """
            public class Payload { public string? Valor { get; set; } }

            [MapPost("/", "do-two-bodies")]
            public class DoTwoBodies
            {
                public string? Nome { get; set; }

                [Command]
                public Result Execute([WithParameter, FromBody] Payload payload) => Result.Ok();
            }
            """ + Host;

        Util.Compile(code, out var output, out var diagnostics);

        var conflicts = diagnostics.Where(d => d.Id == "RCCMD037").ToArray();
        Assert.Single(conflicts);
        Assert.Contains("payload", conflicts[0].GetMessage(), StringComparison.Ordinal);
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(output.SyntaxTrees.Skip(1), tree => tree.ToString().Contains("do-two-bodies"));
    }

    [Fact]
    public void FromForm_conflita_com_body_JSON_implicito_RCCMD037()
    {
        var code = Usings +
            """
            [MapPost("/", "do-form-body")]
            public class DoFormBody
            {
                public string? Nome { get; set; }

                [Command]
                public Result Execute([WithParameter, FromForm] string campo) => Result.Ok();
            }
            """ + Host;

        Util.Compile(code, out _, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD037" && d.Severity == DiagnosticSeverity.Error);
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
    }

    [Fact]
    public void FromBody_unico_em_comando_sem_body_nao_produz_RCCMD037()
    {
        var code = Usings +
            """
            public class Payload { public string? Valor { get; set; } }

            [MapPost("/", "do-explicit-body")]
            public class DoExplicitBody
            {
                [Command]
                public Result Execute([WithParameter, FromBody] Payload payload) => Result.Ok();
            }
            """ + Host;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains(GeneratedSources(output), source => source.Contains("[FromBody]"));
    }

    [Fact]
    public void GET_sem_body_nao_produz_RCCMD036()
    {
        var code = Usings +
            """
            [MapGet("/", "get-bodyless")]
            public class GetBodyless
            {
                [Command]
                public Result Execute() => Result.Ok();
            }
            """ + Host;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains(GeneratedSources(output), source => source.Contains("\"get-bodyless\""));
    }

    [Fact]
    public void Comando_nao_mapeado_ignora_bindings_sem_diagnostico()
    {
        var code = Usings +
            """
            public class DoLocal
            {
                [Command]
                public Result Execute([WithParameter, FromQuery, FromHeader(Name = "x")] string valor) => Result.Ok();
            }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains(output.SyntaxTrees.Skip(1), tree => tree.ToString().Contains("IDoLocalHandler"));
    }

    [Fact]
    public void Search_nao_emite_mais_FromRoute_automatico_para_WithParameter()
    {
        var code = Usings +
            """
            public class Ent { public int Id { get; set; } }

            [MapGroup("ents")]
            [MapSearch("/{id:int}", "search-ents")]
            [SearchReference<Ent>]
            public class EntFilter
            {
                public string? Nome { get; set; }

                [WithFilter]
                internal void Configure([WithParameter] int id) { }
            }
            """ + Host;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        var endpoint = GeneratedSources(output).First(source => source.Contains("SearchEntByEntFilterAsync"));
        Assert.DoesNotContain("[FromRoute]", endpoint);
        Assert.Contains("int id", endpoint);
    }

    [Fact]
    public void Search_copia_binding_explicito_do_parametro_do_filtro()
    {
        var code = Usings +
            """
            public class Ent { public int Id { get; set; } }

            [MapGroup("ents")]
            [MapSearch("/", "search-ents")]
            [SearchReference<Ent>]
            public class EntFilter
            {
                public string? Nome { get; set; }

                [WithFilter]
                internal void Configure([WithParameter, FromHeader(Name = "x-k")] string chave) { }
            }
            """ + Host;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        var endpoint = GeneratedSources(output).First(source => source.Contains("SearchEntByEntFilterAsync"));
        Assert.Contains("[FromHeader(Name = \"x-k\")]", endpoint);
    }

    [Fact]
    public void Search_com_FromRoute_fora_do_template_produz_RCCMD035_e_nenhuma_fonte()
    {
        var code = Usings +
            """
            public class Ent { public int Id { get; set; } }

            [MapGroup("ents")]
            [MapSearch("/", "search-ents")]
            [SearchReference<Ent>]
            public class EntFilter
            {
                public string? Nome { get; set; }

                [WithFilter]
                internal void Configure([WithParameter, FromRoute(Name = "nope")] int valor) { }
            }
            """ + Host;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD035" && d.Severity == DiagnosticSeverity.Error);
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(output.SyntaxTrees.Skip(1), tree => tree.ToString().Contains("search-ents"));
    }
}
