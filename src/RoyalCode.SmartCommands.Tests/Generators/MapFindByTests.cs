using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace RoyalCode.SmartCommands.Tests.Generators;

/// <summary>
/// <para>
///     Fase 5 (DF10): <c>MapFindBy&lt;TEntity&gt;</c> — busca por chave alternativa/composta projetada em DTO.
///     Emissão positiva (chave simples/composta, derivação de tipo, metadata comum) e diagnósticos RCCMD056
///     para propriedade/placeholder ausente, duplicado, incompatível, não direto ou ambíguo, sem gerar fonte.
/// </para>
/// </summary>
public class MapFindByTests
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

    private const string Entities =
        """
        public class Produto : Entity<Guid>
        {
            public string Sku { get; set; } = "";
            public string Nome { get; set; } = "";
            public int Lote { get; set; }
        }

        """;

    // ------------------------------------------------------------------
    // Emissão positiva
    // ------------------------------------------------------------------

    [Fact]
    public void Chave_simples_emite_handler_com_filtro_criterios_e_projecao()
    {
        const string code =
            """
            using System;
            using RoyalCode.SmartCommands;
            using RoyalCode.Entities;

            namespace Tests.Phase5.Simple;

            """ + Entities +
            """
            [MapGroup("produtos")]
            [MapFindBy<Produto>("por-sku/{sku}", "produto-por-sku", nameof(Produto.Sku))]
            public class ProdutoPorSku
            {
                public Guid Id { get; set; }
                public string Sku { get; set; } = "";
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        AssertOutputCompiles(output);

        var generated = GeneratedSources(output);
        Assert.Contains(generated, source =>
            source.Contains("group.MapGet(\"por-sku/{sku}\", FindProdutoPorSkuBySkuAsync)") &&
            source.Contains("Task<OkMatch<ProdutoPorSku>> FindProdutoPorSkuBySkuAsync(") &&
            source.Contains("string Sku,") &&
            source.Contains("IRepositoryAccessor<Produto> accessor,") &&
            source.Contains("accessor.FindEntityAsync<ProdutoPorSku>(e => e.Sku == Sku,") &&
            source.Contains("new global::RoyalCode.SmartProblems.Entities.FindCriterion(\"Sku\", Sku)") &&
            source.Contains("if (findResult.NotFound(out var notfoundProblem))") &&
            source.Contains("return findResult.Entity;"));
    }

    [Fact]
    public void Chave_composta_ANDs_os_criterios_na_ordem_declarada_e_deriva_os_tipos()
    {
        const string code =
            """
            using System;
            using RoyalCode.SmartCommands;
            using RoyalCode.Entities;

            namespace Tests.Phase5.Composite;

            """ + Entities +
            """
            [MapGroup("itens")]
            [MapFindBy<Produto>("{sku}/lote/{lote:int}", "produto-por-sku-lote", nameof(Produto.Sku), nameof(Produto.Lote))]
            public class ProdutoPorSkuLote
            {
                public Guid Id { get; set; }
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        AssertOutputCompiles(output);

        var generated = GeneratedSources(output);
        Assert.Contains(generated, source =>
            source.Contains("FindProdutoPorSkuLoteBySkuLoteAsync(") &&
            // o tipo do parâmetro é derivado da propriedade da entidade
            source.Contains("string Sku,") &&
            source.Contains("int Lote,") &&
            source.Contains("accessor.FindEntityAsync<ProdutoPorSkuLote>(e => e.Sku == Sku && e.Lote == Lote,") &&
            source.Contains("new global::RoyalCode.SmartProblems.Entities.FindCriterion(\"Sku\", Sku), " +
                "new global::RoyalCode.SmartProblems.Entities.FindCriterion(\"Lote\", Lote)"));
    }

    [Fact]
    public void Sem_MapGroup_e_mapeado_sem_prefixo()
    {
        const string code =
            """
            using System;
            using RoyalCode.SmartCommands;
            using RoyalCode.Entities;

            namespace Tests.Phase5.NoGroup;

            """ + Entities +
            """
            [MapFindBy<Produto>("produtos/por-sku/{sku}", "produto-por-sku", nameof(Produto.Sku))]
            public class ProdutoPorSku
            {
                public Guid Id { get; set; }
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        AssertOutputCompiles(output);

        Assert.Contains(GeneratedSources(output), source =>
            source.Contains("FindProdutoPorSkuBySkuAsync") &&
            source.Contains("MapGet(\"produtos/por-sku/{sku}\""));
    }

    [Fact]
    public void Metadata_comum_tags_summary_e_filtros_sao_aplicados()
    {
        const string code =
            """
            using System;
            using RoyalCode.SmartCommands;
            using RoyalCode.Entities;

            namespace Tests.Phase5.Metadata;

            public sealed class AuditFilter : Microsoft.AspNetCore.Http.IEndpointFilter
            {
                public ValueTask<object?> InvokeAsync(
                    Microsoft.AspNetCore.Http.EndpointFilterInvocationContext context,
                    Microsoft.AspNetCore.Http.EndpointFilterDelegate next) => next(context);
            }

            """ + Entities +
            """
            [MapGroup("produtos")]
            [MapFindBy<Produto>("por-sku/{sku}", "produto-por-sku", nameof(Produto.Sku))]
            [WithTags("Produtos", "Catalogo")]
            [WithSummary("Produto por SKU")]
            [WithEndpointFilter<AuditFilter>]
            public class ProdutoPorSku
            {
                public Guid Id { get; set; }
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        AssertOutputCompiles(output);

        Assert.Contains(GeneratedSources(output), source =>
            source.Contains(".WithTags(\"Produtos\", \"Catalogo\")") &&
            source.Contains(".WithSummary(\"Produto por SKU\")") &&
            source.Contains(".AddEndpointFilter<global::Tests.Phase5.Metadata.AuditFilter>()"));
    }

    [Fact]
    public void Duas_chaves_distintas_da_mesma_entidade_no_mesmo_grupo_coexistem()
    {
        const string code =
            """
            using System;
            using RoyalCode.SmartCommands;
            using RoyalCode.Entities;

            namespace Tests.Phase5.TwoKeys;

            """ + Entities +
            """
            [MapGroup("produtos")]
            [MapFindBy<Produto>("por-sku/{sku}", "produto-por-sku", nameof(Produto.Sku))]
            public class ProdutoPorSku
            {
                public Guid Id { get; set; }
            }

            [MapGroup("produtos")]
            [MapFindBy<Produto>("por-nome/{nome}", "produto-por-nome", nameof(Produto.Nome))]
            public class ProdutoPorNome
            {
                public Guid Id { get; set; }
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        AssertOutputCompiles(output);

        var generated = GeneratedSources(output);
        // nomes de handler distintos por serem baseados no DTO
        Assert.Contains(generated, source =>
            source.Contains("FindProdutoPorSkuBySkuAsync") && source.Contains("FindProdutoPorNomeByNomeAsync"));
    }

    // ------------------------------------------------------------------
    // Diagnósticos RCCMD056 (sem fonte)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("nameof(Produto.Sku), nameof(Produto.Sku)", "{sku}/{sku}", "declared more than once")]
    [InlineData("\"NaoExiste\"", "{naoExiste}", "was not found on the entity 'Produto'")]
    [InlineData("nameof(Produto.Sku)", "por-sku", "has no matching route placeholder")]
    [InlineData("nameof(Produto.Sku)", "{sku}/{extra}", "does not match any declared property")]
    [InlineData("nameof(Produto.Sku)", "{sku:int}", "expects 'int' but the property type is 'string'")]
    [InlineData("nameof(Produto.Sku)", "{sku?}", "must not be optional")]
    [InlineData("nameof(Produto.Sku)", "{**sku}", "must not be a catch-all")]
    public void Uso_invalido_produz_RCCMD056(string properties, string routeTail, string reasonFragment)
    {
        var code =
            $$"""
            using System;
            using RoyalCode.SmartCommands;
            using RoyalCode.Entities;

            namespace Tests.Phase5.Invalid;

            """ + Entities +
            $$"""
            [MapGroup("produtos")]
            [MapFindBy<Produto>("{{routeTail}}", "produto-invalido", {{properties}})]
            public class ProdutoDetalhes
            {
                public Guid Id { get; set; }
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD056" &&
            d.GetMessage().Contains(reasonFragment, StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(GeneratedSources(output), source => source.Contains("OkMatch"));
    }

    [Fact]
    public void Sem_propriedades_declaradas_produz_RCCMD056()
    {
        const string code =
            """
            using System;
            using RoyalCode.SmartCommands;
            using RoyalCode.Entities;

            namespace Tests.Phase5.NoProps;

            """ + Entities +
            """
            [MapGroup("produtos")]
            [MapFindBy<Produto>("por-sku/{sku}", "produto-por-sku")]
            public class ProdutoDetalhes
            {
                public Guid Id { get; set; }
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD056" &&
            d.GetMessage().Contains("at least one entity property", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
    }

    [Fact]
    public void Propriedade_de_struct_sem_igualdade_produz_RCCMD056()
    {
        // um struct comum sem operator == não pode ser emitido em 'e.Prop == prop' (não compilaria)
        const string code =
            """
            using System;
            using RoyalCode.SmartCommands;
            using RoyalCode.Entities;

            namespace Tests.Phase5.StructNoEquality;

            public struct Codigo { public int Valor; }

            public class Produto : Entity<Guid>
            {
                public Codigo Codigo { get; set; }
            }

            [MapGroup("produtos")]
            [MapFindBy<Produto>("por-codigo/{codigo}", "produto-por-codigo", nameof(Produto.Codigo))]
            public class ProdutoDetalhes
            {
                public Guid Id { get; set; }
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD056" &&
            d.GetMessage().Contains("does not support the equality operator", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(GeneratedSources(output), source => source.Contains("OkMatch"));
    }

    [Fact]
    public void Propriedade_com_operador_de_igualdade_incompativel_produz_RCCMD056()
    {
        const string code =
            """
            using System;
            using RoyalCode.SmartCommands;
            using RoyalCode.Entities;

            namespace Tests.Phase5.IncompatibleEquality;

            public readonly struct Codigo
            {
                public static bool operator ==(Codigo left, int right) => false;
                public static bool operator !=(Codigo left, int right) => true;
                public override bool Equals(object? obj) => false;
                public override int GetHashCode() => 0;
            }

            public class Produto : Entity<Guid>
            {
                public Codigo Codigo { get; set; }
            }

            [MapGroup("produtos")]
            [MapFindBy<Produto>("por-codigo/{codigo}", "produto-por-codigo", nameof(Produto.Codigo))]
            public class ProdutoDetalhes { public Guid Id { get; set; } }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD056" &&
            d.GetMessage().Contains("does not support the equality operator", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(GeneratedSources(output), source => source.Contains("OkMatch"));
    }

    [Fact]
    public void Record_struct_e_tipo_referencia_vinculavel_sao_aceitos()
    {
        // record struct declara operator ==; um tipo referência com binding do ASP.NET (IParsable) é aceito
        // porque == sempre compila para referência — a validação de binding é responsabilidade do ASP.NET Core
        const string code =
            """
            using System;
            using System.Diagnostics.CodeAnalysis;
            using RoyalCode.SmartCommands;
            using RoyalCode.Entities;

            namespace Tests.Phase5.GoodTypes;

            public readonly record struct Codigo(int Valor);

            public sealed class Etiqueta : IParsable<Etiqueta>
            {
                public string Valor { get; init; } = "";
                public static Etiqueta Parse(string s, IFormatProvider? p) => new() { Valor = s };
                public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? p, [MaybeNullWhen(false)] out Etiqueta r)
                { r = new Etiqueta { Valor = s ?? "" }; return true; }
            }

            public class Produto : Entity<Guid>
            {
                public Codigo Codigo { get; set; }
                public Etiqueta Etiqueta { get; set; } = new();
            }

            [MapGroup("produtos")]
            [MapFindBy<Produto>("por-codigo/{codigo}", "produto-por-codigo", nameof(Produto.Codigo))]
            public class ProdutoPorCodigo { public Guid Id { get; set; } }

            [MapGroup("produtos")]
            [MapFindBy<Produto>("por-etiqueta/{etiqueta}", "produto-por-etiqueta", nameof(Produto.Etiqueta))]
            public class ProdutoPorEtiqueta { public Guid Id { get; set; } }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        AssertOutputCompiles(output);

        var generated = GeneratedSources(output);
        Assert.Contains(generated, source =>
            source.Contains("Codigo Codigo,") && source.Contains("e.Codigo == Codigo"));
        Assert.Contains(generated, source =>
            source.Contains("Etiqueta Etiqueta,") && source.Contains("e.Etiqueta == Etiqueta"));
    }

    [Fact]
    public void Getter_nao_publico_produz_RCCMD056()
    {
        // o getter private não é acessível ao handler gerado (outra classe/árvore)
        const string code =
            """
            using System;
            using RoyalCode.SmartCommands;
            using RoyalCode.Entities;

            namespace Tests.Phase5.PrivateGetter;

            public class Produto : Entity<Guid>
            {
                public string Sku { private get; set; } = "";
            }

            [MapGroup("produtos")]
            [MapFindBy<Produto>("por-sku/{sku}", "produto-por-sku", nameof(Produto.Sku))]
            public class ProdutoDetalhes
            {
                public Guid Id { get; set; }
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD056" &&
            d.GetMessage().Contains("getter is not public", StringComparison.Ordinal));
        Assert.DoesNotContain(GeneratedSources(output), source => source.Contains("OkMatch"));
    }

    [Fact]
    public void Propriedade_com_nome_keyword_e_emitida_escapada_com_arroba()
    {
        const string code =
            """
            using System;
            using RoyalCode.SmartCommands;
            using RoyalCode.Entities;

            namespace Tests.Phase5.Keyword;

            public class Produto : Entity<Guid>
            {
                public string @class { get; set; } = "";
            }

            [MapGroup("produtos")]
            [MapFindBy<Produto>("por-class/{class}", "produto-por-class", nameof(Produto.@class))]
            public class ProdutoPorClass
            {
                public Guid Id { get; set; }
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        AssertOutputCompiles(output);

        Assert.Contains(GeneratedSources(output), source =>
            source.Contains("string @class,") &&
            source.Contains("e.@class == @class") &&
            // o critério mantém o nome real da propriedade como string literal
            source.Contains("FindCriterion(\"class\", @class)"));
    }

    [Fact]
    public void Propriedade_com_keyword_contextual_e_emitida_escapada_com_arroba()
    {
        const string code =
            """
            using System;
            using RoyalCode.SmartCommands;
            using RoyalCode.Entities;

            namespace Tests.Phase5.ContextualKeyword;

            public class Produto : Entity<Guid>
            {
                public string @await { get; set; } = "";
            }

            [MapGroup("produtos")]
            [MapFindBy<Produto>("por-await/{await}", "produto-por-await", nameof(Produto.@await))]
            public class ProdutoPorAwait { public Guid Id { get; set; } }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        AssertOutputCompiles(output);
        Assert.Contains(GeneratedSources(output), source =>
            source.Contains("string @await,") &&
            source.Contains("e.@await == @await") &&
            source.Contains("FindCriterion(\"await\", @await)"));
    }

    [Fact]
    public void Entidade_aninhada_e_suportada()
    {
        const string code =
            """
            using System;
            using RoyalCode.SmartCommands;
            using RoyalCode.Entities;

            namespace Tests.Phase5.Nested;

            public static class Container
            {
                public class Produto : Entity<Guid>
                {
                    public string Sku { get; set; } = "";
                }
            }

            [MapGroup("produtos")]
            [MapFindBy<Container.Produto>("por-sku/{sku}", "produto-por-sku", nameof(Container.Produto.Sku))]
            public class ProdutoPorSku
            {
                public Guid Id { get; set; }
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        AssertOutputCompiles(output);
        Assert.Contains(GeneratedSources(output), source =>
            source.Contains("IRepositoryAccessor<Container.Produto> accessor") &&
            source.Contains("FindProdutoPorSkuBySkuAsync"));
    }

    [Fact]
    public void Entidade_generica_fechada_e_suportada()
    {
        const string code =
            """
            using System;
            using RoyalCode.SmartCommands;
            using RoyalCode.Entities;

            namespace Tests.Phase5.GenericEntity;

            public class Registro<T> : Entity<Guid>
            {
                public string Sku { get; set; } = "";
            }

            [MapGroup("registros")]
            [MapFindBy<Registro<int>>("por-sku/{sku}", "registro-por-sku", nameof(Registro<int>.Sku))]
            public class RegistroPorSku
            {
                public Guid Id { get; set; }
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        AssertOutputCompiles(output);
        Assert.Contains(GeneratedSources(output), source =>
            source.Contains("IRepositoryAccessor<Registro<int>> accessor") &&
            source.Contains("FindRegistroPorSkuBySkuAsync"));
    }

    [Fact]
    public void Chaves_cujos_nomes_concatenados_colidiriam_coexistem_por_serem_DTOs_distintos()
    {
        // [Sku][Nome] concatena para "SkuNome"; um único nome "SkuNome" concatena para o mesmo; como o nome
        // do handler é baseado no DTO, os dois endpoints coexistem sem RCCMD047 artificial
        const string code =
            """
            using System;
            using RoyalCode.SmartCommands;
            using RoyalCode.Entities;

            namespace Tests.Phase5.NoArtificialCollision;

            public class Produto : Entity<Guid>
            {
                public string Sku { get; set; } = "";
                public string Nome { get; set; } = "";
                public string SkuNome { get; set; } = "";
            }

            [MapGroup("produtos")]
            [MapFindBy<Produto>("a/{sku}/{nome}", "produto-a", nameof(Produto.Sku), nameof(Produto.Nome))]
            public class ProdutoA { public Guid Id { get; set; } }

            [MapGroup("produtos")]
            [MapFindBy<Produto>("b/{skuNome}", "produto-b", nameof(Produto.SkuNome))]
            public class ProdutoB { public Guid Id { get; set; } }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.DoesNotContain(diagnostics, d => d.Id == "RCCMD047");
        AssertOutputCompiles(output);

        var generated = GeneratedSources(output);
        Assert.Contains(generated, source =>
            source.Contains("FindProdutoABySkuNomeAsync") && source.Contains("FindProdutoBBySkuNomeAsync"));
    }

    [Theory]
    [InlineData("accessor")]
    [InlineData("findResult")]
    [InlineData("notfoundProblem")]
    public void Propriedade_com_nome_reservado_produz_RCCMD056(string propertyName)
    {
        var code =
            $$"""
            using System;
            using RoyalCode.SmartCommands;
            using RoyalCode.Entities;

            namespace Tests.Phase5.Reserved;

            public class Produto : Entity<Guid>
            {
                public string {{propertyName}} { get; set; } = "";
            }

            [MapGroup("produtos")]
            [MapFindBy<Produto>("{{{propertyName}}}", "produto-reservado", nameof(Produto.{{propertyName}}))]
            public class ProdutoDetalhes
            {
                public Guid Id { get; set; }
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD056" &&
            d.GetMessage().Contains("reserved by the generated handler", StringComparison.Ordinal));
        Assert.DoesNotContain(GeneratedSources(output), source => source.Contains("OkMatch"));
    }

    [Fact]
    public void WithResultStatus_em_MapFindBy_produz_RCCMD052()
    {
        const string code =
            """
            using System;
            using RoyalCode.SmartCommands;
            using RoyalCode.Entities;

            namespace Tests.Phase5.DenyStatus;

            """ + Entities +
            """
            [MapGroup("produtos")]
            [MapFindBy<Produto>("por-sku/{sku}", "produto-por-sku", nameof(Produto.Sku))]
            [WithResultStatus(HttpResultStatus.Ok)]
            public class ProdutoDetalhes
            {
                public Guid Id { get; set; }
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD052");
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
    }
}
