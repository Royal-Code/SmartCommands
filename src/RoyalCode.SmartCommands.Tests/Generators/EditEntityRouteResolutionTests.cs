using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace RoyalCode.SmartCommands.Tests.Generators;

/// <summary>
/// <para>
///     Fase 5 (DF4): resolução do parâmetro de rota que carrega o id da entidade editada, na ordem:
///     <c>RouteParameterName</c> explícito; única variável; <c>{parâmetroDaEntidade}Id</c>;
///     <c>parâmetroDaEntidade</c>; erro (RCCMD031) em qualquer outro caso. Constraints/opcionalidade
///     incompatíveis produzem RCCMD032.
/// </para>
/// </summary>
public class EditEntityRouteResolutionTests
{
    private const string Usings =
        """
        using RoyalCode.SmartCommands;
        using RoyalCode.SmartProblems;
        using Microsoft.AspNetCore.Routing;

        namespace Tests.EditResolution;

        public class Db : Microsoft.EntityFrameworkCore.DbContext { }
        public class Person { public int Id { get; set; } }

        """;

    private const string Host =
        """

        [MapApiHandlers]
        public static partial class Endpoints { }
        """;

    private static string EditCommand(string route, string attributeArguments = "", string idType = "int") =>
        Usings +
        $$"""
        [MapGroup("people")]
        [MapPut("{{route}}", "edit-person")]
        public class EditPerson
        {
            [Command, WithUnitOfWork<Db>, EditEntity<Person, {{idType}}>{{attributeArguments}}]
            public Result Execute(Person person) => Result.Ok();
        }
        """ + Host;

    private static string EndpointSource(Compilation output) =>
        output.SyntaxTrees.Skip(1).Select(tree => tree.ToString())
            .FirstOrDefault(source => source.Contains("MapPeopleApi")) ?? string.Empty;

    [Fact]
    public void Sem_variavel_de_rota_o_id_e_vinculado_por_inferencia_sem_diagnostico()
    {
        Util.Compile(EditCommand("/"), out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        var endpoint = EndpointSource(output);
        Assert.Contains("int personId", endpoint);
        Assert.DoesNotContain("FromRoute", endpoint);
    }

    [Fact]
    public void Unica_variavel_e_usada_automaticamente_mesmo_sem_nome_convencional()
    {
        Util.Compile(EditCommand("/{codigo}"), out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("[FromRoute(Name = \"codigo\")]", EndpointSource(output));
    }

    [Fact]
    public void Varias_variaveis_casam_com_o_parametro_da_entidade_mais_sufixo_Id()
    {
        Util.Compile(EditCommand("/{groupId}/people/{personId}"), out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("[FromRoute(Name = \"personId\")]", EndpointSource(output));
    }

    [Fact]
    public void Varias_variaveis_casam_com_o_nome_do_parametro_da_entidade()
    {
        Util.Compile(EditCommand("/{groupId}/people/{person}"), out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("[FromRoute(Name = \"person\")]", EndpointSource(output));
    }

    [Fact]
    public void RouteParameterName_explicito_tem_prioridade_sobre_as_convencoes()
    {
        Util.Compile(
            EditCommand("/{personId}/itens/{codigo}", "(RouteParameterName = \"codigo\")"),
            out var output,
            out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("[FromRoute(Name = \"codigo\")]", EndpointSource(output));
    }

    [Fact]
    public void RouteParameterName_inexistente_no_template_produz_RCCMD031_e_bloqueia_a_fonte()
    {
        Util.Compile(
            EditCommand("/{a}/{b}", "(RouteParameterName = \"nope\")"),
            out var output,
            out var diagnostics);

        var resolutionErrors = diagnostics.Where(d => d.Id == "RCCMD031").ToArray();
        Assert.Single(resolutionErrors);
        Assert.Equal(DiagnosticSeverity.Error, resolutionErrors[0].Severity);
        Assert.NotEqual(Location.None, resolutionErrors[0].Location);
        Assert.Contains("nope", resolutionErrors[0].GetMessage(), StringComparison.Ordinal);
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(output.SyntaxTrees.Skip(1), tree => tree.ToString().Contains("EditPersonHandler"));
    }

    [Fact]
    public void Ambiguidade_produz_RCCMD031_e_nenhuma_fonte_de_endpoint()
    {
        Util.Compile(EditCommand("/{a}/{b}"), out var output, out var diagnostics);

        var resolutionErrors = diagnostics.Where(d => d.Id == "RCCMD031").ToArray();
        Assert.Single(resolutionErrors);
        Assert.Contains("RouteParameterName", resolutionErrors[0].GetMessage(), StringComparison.Ordinal);
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(output.SyntaxTrees.Skip(1), tree => tree.ToString().Contains("edit-person"));
    }

    [Fact]
    public void Constraint_de_tipo_incompativel_produz_RCCMD032()
    {
        Util.Compile(EditCommand("/{id:guid}"), out _, out var diagnostics);

        var incompatible = diagnostics.Where(d => d.Id == "RCCMD032").ToArray();
        Assert.Single(incompatible);
        Assert.Contains("guid", incompatible[0].GetMessage(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
    }

    [Fact]
    public void Constraint_de_tipo_compativel_nao_produz_diagnostico()
    {
        Util.Compile(EditCommand("/{id:int}"), out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("[FromRoute(Name = \"id\")]", EndpointSource(output));
    }

    [Fact]
    public void Variavel_opcional_produz_RCCMD032()
    {
        Util.Compile(EditCommand("/{id?}"), out _, out var diagnostics);

        var incompatible = diagnostics.Where(d => d.Id == "RCCMD032").ToArray();
        Assert.Single(incompatible);
        Assert.Contains("optional", incompatible[0].GetMessage(), StringComparison.Ordinal);
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
    }

    [Fact]
    public void Variavel_do_grupo_participa_da_resolucao()
    {
        // o template de binding em runtime é grupo + rota; uma única variável no grupo é resolvida
        var code = Usings +
            """
            [MapGroup("stores/{personId}")]
            [MapPut("/promote", "promote-person")]
            public class EditPerson
            {
                [Command, WithUnitOfWork<Db>, EditEntity<Person, int>]
                public Result Execute(Person person) => Result.Ok();
            }
            """ + Host;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains(
            output.SyntaxTrees.Skip(1).Select(tree => tree.ToString()),
            source => source.Contains("[FromRoute(Name = \"personId\")]"));
    }

    [Fact]
    public void RouteParameterName_explicito_pode_apontar_para_variavel_do_grupo()
    {
        var code = Usings +
            """
            [MapGroup("stores/{ownerId}")]
            [MapPut("/{a}/{b}", "edit-owner")]
            public class EditPerson
            {
                [Command, WithUnitOfWork<Db>, EditEntity<Person, int>(RouteParameterName = "ownerId")]
                public Result Execute(Person person) => Result.Ok();
            }
            """ + Host;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains(
            output.SyntaxTrees.Skip(1).Select(tree => tree.ToString()),
            source => source.Contains("[FromRoute(Name = \"ownerId\")]"));
    }

    [Fact]
    public void Variavel_catch_all_produz_RCCMD032()
    {
        Util.Compile(EditCommand("/{*id}"), out _, out var diagnostics);

        var incompatible = diagnostics.Where(d => d.Id == "RCCMD032").ToArray();
        Assert.Single(incompatible);
        Assert.Contains("catch-all", incompatible[0].GetMessage(), StringComparison.Ordinal);
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
    }

    [Fact]
    public void Id_anulavel_com_constraint_do_tipo_base_nao_produz_falso_positivo()
    {
        Util.Compile(EditCommand("/{id:int}", idType: "int?"), out _, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Id == "RCCMD032");
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
    }

    [Fact]
    public void Nome_do_id_do_handler_e_reservado_mesmo_sem_mapeamento()
    {
        // o handler de EditEntity sempre declara '{entidade}Id' na assinatura; um [WithParameter] homônimo
        // colidiria no HandleAsync gerado mesmo quando o comando não é mapeado
        var code = Usings +
            """
            public class EditPerson
            {
                [Command, WithUnitOfWork<Db>, EditEntity<Person, int>]
                public Result Execute(Person person, [WithParameter] string personId) => Result.Ok();
            }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        var reserved = diagnostics.Where(d => d.Id == "RCCMD029").ToArray();
        Assert.Single(reserved);
        Assert.Contains("personId", reserved[0].GetMessage(), StringComparison.Ordinal);
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(output.SyntaxTrees.Skip(1), tree => tree.ToString().Contains("EditPersonHandler"));
    }

    [Fact]
    public void Ordem_do_delegate_e_id_de_edicao_command_WithParameters_e_ct()
    {
        // T7: ID de edit, command, todos os WithParameter, ct — no delegate e no handler
        var code = Usings +
            """
            [MapGroup("people")]
            [MapPut("/{id}", "edit-person")]
            public class EditPerson
            {
                public string? Nome { get; set; }

                [Command, WithUnitOfWork<Db>, EditEntity<Person, int>]
                public Result Execute(Person person, [WithParameter] string extra) => Result.Ok();
            }
            """ + Host;

        Util.Compile(code, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        var endpoint = EndpointSource(output);
        var idIndex = endpoint.IndexOf("int personId", StringComparison.Ordinal);
        var commandIndex = endpoint.IndexOf("EditPerson? command", StringComparison.Ordinal);
        var extraIndex = endpoint.IndexOf("string extra", StringComparison.Ordinal);
        var ctIndex = endpoint.IndexOf("CancellationToken ct", StringComparison.Ordinal);

        Assert.True(idIndex >= 0 && commandIndex > idIndex && extraIndex > commandIndex && ctIndex > extraIndex,
            $"Ordem inesperada no delegate: id={idIndex}, command={commandIndex}, extra={extraIndex}, ct={ctIndex}.{Environment.NewLine}{endpoint}");

        // a interface do handler segue a mesma ordem
        var handlerInterface = output.SyntaxTrees.Skip(1).Select(tree => tree.ToString())
            .First(source => source.Contains("IEditPersonHandler"));
        Assert.Contains("HandleAsync(int personId, EditPerson command, string extra, CancellationToken ct)", handlerInterface);
    }
}
