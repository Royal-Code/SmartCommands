using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace RoyalCode.SmartCommands.Tests.Generators;

/// <summary>
/// <para>
///     Fase 9: completude dos mapeamentos Minimal API existentes — comportamento explícito para zero/um/
///     múltiplos <c>Map*</c>, nomes de endpoint/grupo validados (RCCMD044/045) e deduplicados na agregação
///     (RCCMD046/047), validação de <c>MapIdResultValue</c>/<c>MapResponseValues</c> (RCCMD048/049),
///     placeholders nomeados de <c>MapCreatedRoute</c> (DF17, RCCMD050), status por verbo (Delete),
///     assinaturas/rota de <c>MapFind</c> e <c>MapSearch</c> e metadata completa.
/// </para>
/// </summary>
public class MapEndpointCompletenessTests
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

    // ------------------------------------------------------------------
    // T1 — zero/um/múltiplos Map* têm comportamento explícito
    // ------------------------------------------------------------------

    [Fact]
    public void Comando_sem_Map_gera_handler_sem_endpoint_e_sem_diagnostico()
    {
        const string code =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Phase9.NoMap;

            public class DoLocal
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
        Assert.Contains(generated, source => source.Contains("IDoLocalHandler"));
        Assert.DoesNotContain(generated, source => source.Contains("MapGroup("));
    }

    // ------------------------------------------------------------------
    // T2 — MapGroup é opcional de forma consistente (sem prefixo, classe do host)
    // ------------------------------------------------------------------

    [Fact]
    public void Comando_mapeado_sem_MapGroup_e_mapeado_sem_prefixo_na_classe_do_host()
    {
        const string code =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Phase9.NoGroup;

            [MapPost("/things", "create-thing")]
            public class CreateThing
            {
                public int Value { get; set; }

                [Command]
                public Result Execute() => Result.Ok();
            }

            [MapApiHandlers]
            public static partial class MyEndpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        AssertOutputCompiles(output);

        var generated = GeneratedSources(output);
        // sem MapGroup, o grupo não tem prefixo de rota; classe/método vêm do host
        Assert.Contains(generated, source =>
            source.Contains("builder.MapGroup(\"\")") &&
            source.Contains("class MapMyEndpointsApi") &&
            source.Contains("MapMyEndpointsGroup"));
    }

    // ------------------------------------------------------------------
    // T3 — nomes de endpoint/grupo vazios e colisões após a normalização
    // ------------------------------------------------------------------

    [Fact]
    public void Endpoint_name_vazio_produz_RCCMD044_sem_fonte_do_endpoint()
    {
        const string code =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Phase9.EmptyName;

            [MapPost("/x", "   ")]
            public class CreateX
            {
                [Command]
                public Result Execute() => Result.Ok();
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD044" && d.Severity == DiagnosticSeverity.Error);
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(GeneratedSources(output), source => source.Contains("MapPost("));
    }

    [Fact]
    public void MapFind_com_endpoint_name_vazio_produz_RCCMD044()
    {
        const string code =
            """
            using System;
            using RoyalCode.SmartCommands;
            using RoyalCode.Entities;

            namespace Tests.Phase9.FindEmptyName;

            public class Produto : Entity<Guid>
            {
                public string? Nome { get; set; }
            }

            [MapFind("{id:guid}", ""), EntityReference<Produto, Guid>]
            public class ProdutoDetalhes
            {
                public Guid Id { get; set; }
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD044" && d.Severity == DiagnosticSeverity.Error);
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(GeneratedSources(output), source => source.Contains("MapGet("));
    }

    [Fact]
    public void MapSearch_com_endpoint_name_vazio_produz_RCCMD044()
    {
        const string code =
            """
            using System;
            using RoyalCode.SmartCommands;
            using RoyalCode.Entities;

            namespace Tests.Phase9.SearchEmptyName;

            public class Produto : Entity<Guid>
            {
                public string? Nome { get; set; }
            }

            [MapSearch("", " "), SearchReference<Produto>]
            public class ProdutoFiltro
            {
                public string? Nome { get; set; }
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD044" && d.Severity == DiagnosticSeverity.Error);
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(GeneratedSources(output), source => source.Contains("MapGet("));
    }

    [Fact]
    public void MapGroup_sem_conteudo_utilizavel_produz_RCCMD045()
    {
        const string code =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Phase9.BadGroup;

            [MapGroup("///")]
            [MapPost("/x", "create-x")]
            public class CreateX
            {
                [Command]
                public Result Execute() => Result.Ok();
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD045" && d.Severity == DiagnosticSeverity.Error);
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(GeneratedSources(output), source => source.Contains("MapPost("));
    }

    [Fact]
    public void Grupo_com_variavel_de_rota_gera_classe_com_nome_valido()
    {
        const string code =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Phase9.GroupRouteVar;

            [MapGroup("stores/{storeId:int}")]
            [MapPost("/things", "create-thing")]
            public class CreateThing
            {
                public int Value { get; set; }

                [Command]
                public Result Execute([WithParameter] int storeId) => Result.Ok();
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        AssertOutputCompiles(output);

        var generated = GeneratedSources(output);
        // o parâmetro de rota do grupo entra no nome da classe pelo nome da variável
        Assert.Contains(generated, source =>
            source.Contains("class MapStoresStoreIdApi") &&
            source.Contains("builder.MapGroup(\"stores/{storeId:int}\")"));
    }

    [Fact]
    public void Grupos_distintos_que_normalizam_para_a_mesma_classe_produzem_RCCMD046()
    {
        const string code =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Phase9.GroupCollision;

            [MapGroup("my-group")]
            [MapPost("/a", "create-a")]
            public class CreateA
            {
                [Command]
                public Result Execute() => Result.Ok();
            }

            [MapGroup("my/group")]
            [MapPost("/b", "create-b")]
            public class CreateB
            {
                [Command]
                public Result Execute() => Result.Ok();
            }

            [MapGroup("others")]
            [MapPost("/c", "create-c")]
            public class CreateC
            {
                [Command]
                public Result Execute() => Result.Ok();
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        var conflicts = diagnostics.Where(d => d.Id == "RCCMD046").ToArray();
        Assert.Equal(2, conflicts.Length);
        Assert.All(conflicts, d => Assert.Equal(DiagnosticSeverity.Error, d.Severity));
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");

        var generated = GeneratedSources(output);
        // os grupos conflitantes não são emitidos; o grupo não conflitante permanece
        Assert.DoesNotContain(generated, source => source.Contains("MapMyGroupApi"));
        Assert.Contains(generated, source => source.Contains("MapOthersApi"));
    }

    [Fact]
    public void Dois_finds_da_mesma_entidade_no_mesmo_grupo_produzem_RCCMD047()
    {
        const string code =
            """
            using System;
            using RoyalCode.SmartCommands;
            using RoyalCode.Entities;

            namespace Tests.Phase9.MethodCollision;

            public class Produto : Entity<Guid>
            {
                public string? Nome { get; set; }
            }

            [MapGroup("produtos")]
            [MapFind("{id:guid}", "find-produto-a"), EntityReference<Produto, Guid>]
            public class ProdutoDetalhesA
            {
                public Guid Id { get; set; }
            }

            [MapGroup("produtos")]
            [MapFind("resumo/{id:guid}", "find-produto-b"), EntityReference<Produto, Guid>]
            public class ProdutoDetalhesB
            {
                public Guid Id { get; set; }
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        // os dois finds geram FindProdutoHandleAsync no mesmo grupo — C# inválido se emitido
        var duplicates = diagnostics.Where(d => d.Id == "RCCMD047").ToArray();
        Assert.Equal(2, duplicates.Length);
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(GeneratedSources(output), source => source.Contains("FindProdutoHandleAsync"));
    }

    // ------------------------------------------------------------------
    // T4 — MapIdResultValue/MapResponseValues validados
    // ------------------------------------------------------------------

    [Fact]
    public void MapResponseValues_vazio_produz_RCCMD041()
    {
        const string code =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Phase9.EmptyValues;

            public sealed class Thing
            {
                public int Id { get; set; }
            }

            [MapPost("/things", "create-thing"), MapResponseValues]
            public class CreateThing
            {
                public int Value { get; set; }

                [Command]
                public Thing Execute() => new() { Id = 1 };
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD041" &&
            d.GetMessage().Contains("non-empty array of property names"));
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(GeneratedSources(output), source => source.Contains("MapPost("));
    }

    [Fact]
    public void MapResponseValues_com_nome_duplicado_produz_RCCMD041()
    {
        const string code =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Phase9.DupValues;

            public sealed class Thing
            {
                public int Id { get; set; }
            }

            [MapPost("/things", "create-thing"), MapResponseValues("Id", "Id")]
            public class CreateThing
            {
                public int Value { get; set; }

                [Command]
                public Thing Execute() => new() { Id = 1 };
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD041" &&
            d.GetMessage().Contains("declared more than once"));
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
    }

    [Fact]
    public void MapResponseValues_com_nomes_que_diferem_apenas_por_caixa_produz_RCCMD041()
    {
        const string code =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Phase9.DupValuesCase;

            public sealed class Thing
            {
                public int Id { get; set; }

                public int id { get; set; }
            }

            [MapPost("/things", "create-thing"), MapResponseValues("Id", "id")]
            public class CreateThing
            {
                [Command]
                public Thing Execute() => new() { Id = 1, id = 2 };
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD041" &&
            d.GetMessage().Contains("declared more than once"));
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(GeneratedSources(output), source => source.Contains("MapPost("));
    }

    [Fact]
    public void MapResponseValues_com_propriedade_nao_publica_produz_RCCMD048()
    {
        const string code =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Phase9.PrivateValue;

            public sealed class Thing
            {
                public int Id { get; set; }

                internal string? Secret { get; set; }
            }

            [MapPost("/things", "create-thing"), MapResponseValues("Id", "Secret")]
            public class CreateThing
            {
                public int Value { get; set; }

                [Command]
                public Thing Execute() => new() { Id = 1 };
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD048" && d.GetMessage().Contains("Secret"));
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
    }

    [Fact]
    public void MapIdResultValue_com_MapResponseValues_produz_RCCMD049()
    {
        const string code =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Phase9.ConflictingMappings;

            public sealed class Thing
            {
                public int Id { get; set; }

                public string? Name { get; set; }
            }

            [MapPost("/things", "create-thing"), MapIdResultValue, MapResponseValues("Id", "Name")]
            public class CreateThing
            {
                public int Value { get; set; }

                [Command]
                public Thing Execute() => new() { Id = 1 };
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD049" && d.Severity == DiagnosticSeverity.Error);
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(GeneratedSources(output), source => source.Contains("MapPost("));
    }

    // ------------------------------------------------------------------
    // T5 — DF17: MapCreatedRoute com placeholders nomeados
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("{0}", new[] { "Id" }, "does not match any declared property")]
    [InlineData("{id}/{extra}", new[] { "Id" }, "placeholder(s)")]
    [InlineData("{id}", new[] { "Id", "Name" }, "placeholder(s)")]
    [InlineData("{id}/{ID}", new[] { "Id" }, "occurs more than once")]
    [InlineData("{id}", new[] { "Id", "ID" }, "declared more than once")]
    [InlineData("{id:int}", new[] { "Id" }, "must be a simple name")]
    [InlineData("{missing}", new[] { "Missing" }, "was not found on the returned value type")]
    public void MapCreatedRoute_invalido_produz_RCCMD050(string pattern, string[] properties, string reasonFragment)
    {
        var propertiesLiteral = string.Join(", ", properties.Select(p => $"\"{p}\""));
        var code =
            $$"""
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Phase9.CreatedRoute;

            public sealed class Thing
            {
                public int Id { get; set; }

                public string? Name { get; set; }
            }

            [MapGroup("things")]
            [MapPost("/", "create-thing")]
            [MapCreatedRoute("{{pattern}}", {{propertiesLiteral}})]
            public class CreateThing
            {
                public int Value { get; set; }

                [Command]
                public Thing Execute() => new() { Id = 1 };
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD050" &&
            d.GetMessage().Contains(reasonFragment, StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(GeneratedSources(output), source => source.Contains("CreatedMatch"));
    }

    [Fact]
    public void MapCreatedRoute_casa_placeholder_sem_diferenciar_maiusculas()
    {
        const string code =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Phase9.CreatedRouteOk;

            public sealed class Thing
            {
                public int Id { get; set; }

                public string? Slug { get; set; }
            }

            [MapGroup("things")]
            [MapPost("/", "create-thing")]
            [MapCreatedRoute("{ID}/detail/{slug}", nameof(Thing.Id), nameof(Thing.Slug))]
            public class CreateThing
            {
                public int Value { get; set; }

                [Command]
                public Thing Execute() => new() { Id = 1 };
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        AssertOutputCompiles(output);

        // a Location interpola as propriedades declaradas, na caixa exata do C#
        Assert.Contains(GeneratedSources(output), source =>
            source.Contains("CreatedMatch(v => $\"things/{v.Id}/detail/{v.Slug}\")"));
    }

    [Theory]
    [InlineData("things", "/{id}")]
    [InlineData("things/", "{id}")]
    [InlineData("things/", "/{id}")]
    public void MapCreatedRoute_normaliza_a_barra_na_juncao_com_o_grupo(string group, string createdRoute)
    {
        var code =
            $$"""
            using RoyalCode.SmartCommands;

            namespace Tests.Phase9.CreatedRouteSeparator;

            public sealed class Thing
            {
                public int Id { get; set; }
            }

            [MapGroup("{{group}}")]
            [MapPost("/", "create-thing")]
            [MapCreatedRoute("{{createdRoute}}", nameof(Thing.Id))]
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
        Assert.Contains(GeneratedSources(output), source =>
            source.Contains("CreatedMatch(v => $\"things/{v.Id}\")"));
    }

    // ------------------------------------------------------------------
    // T6 — status por verbo: Delete não descarta valor em silêncio
    // ------------------------------------------------------------------

    [Fact]
    public void Delete_sem_valor_responde_NoContent()
    {
        const string code =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Phase9.DeleteNoValue;

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
        // a resposta de sucesso 204 entra na metadata OpenAPI explicitamente
        Assert.Contains(generated, source => source.Contains(".Produces(204)"));
    }

    [Fact]
    public void Delete_com_valor_responde_Ok_com_o_valor()
    {
        const string code =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Phase9.DeleteWithValue;

            public sealed class Removed
            {
                public int Id { get; set; }
            }

            [MapDelete("/things/{id:int}", "delete-thing")]
            public class DeleteThing
            {
                [Command]
                public Result<Removed> Execute([WithParameter] int id) => new Removed { Id = id };
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        AssertOutputCompiles(output);

        var generated = GeneratedSources(output);
        Assert.Contains(generated, source => source.Contains("OkMatch<Removed>"));
        Assert.DoesNotContain(generated, source => source.Contains("NoContentMatch"));
    }

    // ------------------------------------------------------------------
    // T7 — assinaturas e rota de MapFind/MapSearch
    // ------------------------------------------------------------------

    [Fact]
    public void MapFind_sem_variavel_id_na_rota_produz_RCCMD021()
    {
        const string code =
            """
            using System;
            using RoyalCode.SmartCommands;
            using RoyalCode.Entities;

            namespace Tests.Phase9.FindNoId;

            public class Produto : Entity<Guid>
            {
                public string? Nome { get; set; }
            }

            [MapFind("detalhes", "find-produto"), EntityReference<Produto, Guid>]
            public class ProdutoDetalhes
            {
                public Guid Id { get; set; }
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD021" &&
            d.GetMessage().Contains("must declare the route parameter '{id}'"));
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(GeneratedSources(output), source => source.Contains("MapGet("));
    }

    [Fact]
    public void MapFind_com_constraint_incompativel_com_o_id_produz_RCCMD021()
    {
        const string code =
            """
            using System;
            using RoyalCode.SmartCommands;
            using RoyalCode.Entities;

            namespace Tests.Phase9.FindBadConstraint;

            public class Produto : Entity<Guid>
            {
                public string? Nome { get; set; }
            }

            [MapFind("{id:int}", "find-produto"), EntityReference<Produto, Guid>]
            public class ProdutoDetalhes
            {
                public Guid Id { get; set; }
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD021" &&
            d.GetMessage().Contains("the route constraint 'int' expects 'int'"));
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
    }

    [Theory]
    [InlineData("{id=1}")]
    [InlineData("{id:int=1}")]
    public void MapFind_com_valor_default_no_id_produz_RCCMD021(string routePattern)
    {
        var code =
            $$"""
            using RoyalCode.SmartCommands;
            using RoyalCode.Entities;

            namespace Tests.Phase9.FindDefaultId;

            public class Produto : Entity<int>
            {
                public string? Nome { get; set; }
            }

            [MapFind("{{routePattern}}", "find-produto"), EntityReference<Produto, int>]
            public class ProdutoDetalhes
            {
                public int Id { get; set; }
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD021" &&
            d.GetMessage().Contains("must not be optional or have a default value"));
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(GeneratedSources(output), source => source.Contains("MapGet("));
    }

    [Fact]
    public void MapFind_com_id_declarado_no_grupo_e_valido()
    {
        const string code =
            """
            using System;
            using RoyalCode.SmartCommands;
            using RoyalCode.Entities;

            namespace Tests.Phase9.FindIdInGroup;

            public class Produto : Entity<Guid>
            {
                public string? Nome { get; set; }
            }

            [MapGroup("produtos/{id:guid}")]
            [MapFind("estoque", "find-produto-estoque"), EntityReference<Produto, Guid>]
            public class ProdutoDetalhes
            {
                public Guid Id { get; set; }
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains(GeneratedSources(output), source => source.Contains("FindProdutoHandleAsync"));
    }

    [Fact]
    public void Parametro_de_filtro_com_binding_sem_WithParameter_produz_RCCMD023()
    {
        const string code =
            """
            using System;
            using RoyalCode.SmartCommands;
            using RoyalCode.Entities;
            using RoyalCode.SmartSearch;
            using Microsoft.AspNetCore.Mvc;

            namespace Tests.Phase9.FilterBinding;

            public class Produto : Entity<Guid>
            {
                public string? Nome { get; set; }
            }

            [MapGroup("produtos")]
            [MapSearch("", "search-produtos"), SearchReference<Produto>]
            public class ProdutoFiltro
            {
                public string? Nome { get; set; }

                [WithFilter]
                internal void Configure(ICriteria<Produto> criteria, [FromQuery] int valor)
                {
                }
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD023" &&
            d.GetMessage().Contains("binding attributes but is not marked"));
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(GeneratedSources(output), source => source.Contains("SearchProduto"));
    }

    // ------------------------------------------------------------------
    // T8 — metadata completa por superfície (description, summary, policies, problems)
    // ------------------------------------------------------------------

    [Fact]
    public void Find_emite_metadata_completa()
    {
        const string code =
            """
            using System;
            using RoyalCode.SmartCommands;
            using RoyalCode.Entities;

            namespace Tests.Phase9.FindMetadata;

            public class Produto : Entity<Guid>
            {
                public string? Nome { get; set; }
            }

            [MapGroup("produtos")]
            [MapFind("{id:guid}", "find-produto"), EntityReference<Produto, Guid>]
            [WithDescription("Retorna os detalhes do produto")]
            [WithSummary("Detalhes do produto")]
            [WithPolicy("produtos-leitura")]
            public class ProdutoDetalhes
            {
                public Guid Id { get; set; }
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        var generated = GeneratedSources(output);
        Assert.Contains(generated, source =>
            source.Contains(".WithName(\"find-produto\")") &&
            source.Contains(".WithDescription(\"Retorna os detalhes do produto\")") &&
            source.Contains(".WithSummary(\"Detalhes do produto\")") &&
            source.Contains(".RequireAuthorization(\"produtos-leitura\")") &&
            source.Contains("[ProduceProblems(ProblemCategory.NotFound)]"));
    }

    [Fact]
    public void Search_emite_metadata_completa()
    {
        const string code =
            """
            using System;
            using RoyalCode.SmartCommands;
            using RoyalCode.Entities;

            namespace Tests.Phase9.SearchMetadata;

            public class Produto : Entity<Guid>
            {
                public string? Nome { get; set; }
            }

            [MapGroup("produtos")]
            [MapSearch("", "search-produtos"), SearchReference<Produto>]
            [WithDescription("Busca paginada de produtos")]
            [WithSummary("Busca de produtos")]
            [WithAuthorization]
            public class ProdutoFiltro
            {
                public string? Nome { get; set; }
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        var generated = GeneratedSources(output);
        Assert.Contains(generated, source =>
            source.Contains(".WithName(\"search-produtos\")") &&
            source.Contains(".WithDescription(\"Busca paginada de produtos\")") &&
            source.Contains(".WithSummary(\"Busca de produtos\")") &&
            source.Contains(".RequireAuthorization()") &&
            source.Contains("ProblemCategory.InvalidParameter") &&
            source.Contains("ProblemCategory.InternalServerError"));
    }
}
