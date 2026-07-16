using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace RoyalCode.SmartCommands.Tests.Generators;

/// <summary>
/// <para>
///     Fase 6 (DF13): métodos <c>[CommandValidation]</c> executam após <c>HasProblems</c> e antes de
///     UoW/retry (uma vez, fora do laço), ordenados por <c>Order</c> (padrão 10) com desempate
///     determinístico; o primeiro <c>Result</c> com problemas encerra o handler. Parâmetros usam o mesmo
///     modelo do comando (ct, DI, <c>[WithParameter]</c>); entidades/contextos/acessores são rejeitados.
/// </para>
/// </summary>
public class CommandValidationTests
{
    private const string Usings =
        """
        using System.Threading;
        using System.Threading.Tasks;
        using RoyalCode.SmartCommands;
        using RoyalCode.SmartProblems;
        using Microsoft.AspNetCore.Routing;

        namespace Tests.Validations;

        """;

    private static string HandlerSource(Compilation output, string handlerName) =>
        output.SyntaxTrees.Skip(1).Select(tree => tree.ToString())
            .FirstOrDefault(source => source.Contains($"class {handlerName}")) ?? string.Empty;

    private static void AssertNoErrors(Compilation output, System.Collections.Immutable.ImmutableArray<Diagnostic> diagnostics)
    {
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        var errors = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.True(errors.Length == 0,
            "A saída gerada deve compilar. Erros: " + string.Join(Environment.NewLine, errors.Select(e => e.ToString())));
    }

    [Fact]
    public void Validator_sincrono_roda_apos_HasProblems_e_antes_do_Begin_da_UoW()
    {
        const string code = Usings +
            """
            public class Db : Microsoft.EntityFrameworkCore.DbContext { }

            public partial class DoChecked
            {
                public string? Nome { get; set; }

                public bool HasProblems(out Problems? problems)
                {
                    problems = null;
                    return false;
                }

                [CommandValidation]
                internal Result ValidarNome() => Result.Ok();

                [Command, WithValidateModel, WithUnitOfWork<Db>]
                public Result Executar(Db db) => Result.Ok();
            }
            """;

        Util.Compile(code, out var output, out var diagnostics);
        AssertNoErrors(output, diagnostics);

        var handler = HandlerSource(output, "DoCheckedHandler");
        var hasProblemsIndex = handler.IndexOf("command.HasProblems(out var validationProblems)", StringComparison.Ordinal);
        var validatorIndex = handler.IndexOf("var validationResult1 = command.ValidarNome()", StringComparison.Ordinal);
        var shortCircuitIndex = handler.IndexOf("if (validationResult1.HasProblems(out var validationProblems1))", StringComparison.Ordinal);
        var beginIndex = handler.IndexOf("BeginAsync", StringComparison.Ordinal);

        Assert.True(hasProblemsIndex >= 0 && validatorIndex > hasProblemsIndex && shortCircuitIndex > validatorIndex && beginIndex > shortCircuitIndex,
            $"Ordem inesperada: HasProblems={hasProblemsIndex}, validator={validatorIndex}, guard={shortCircuitIndex}, begin={beginIndex}.{Environment.NewLine}{handler}");
        Assert.Contains("return validationProblems1;", handler);
    }

    [Fact]
    public void Validator_assincrono_e_aguardado_e_torna_o_handler_assincrono()
    {
        const string code = Usings +
            """
            public class DoAsyncChecked
            {
                public string? Nome { get; set; }

                [CommandValidation]
                internal async Task<Result> ValidarAsync(CancellationToken ct)
                {
                    await Task.CompletedTask;
                    return Result.Ok();
                }

                [Command]
                public Result Executar() => Result.Ok();
            }
            """;

        Util.Compile(code, out var output, out var diagnostics);
        AssertNoErrors(output, diagnostics);

        var handler = HandlerSource(output, "DoAsyncCheckedHandler");
        Assert.Contains("async Task<Result> HandleAsync", handler);
        Assert.Contains("var validationResult1 = await command.ValidarAsync(ct)", handler);
    }

    [Fact]
    public void ValueTask_de_Result_e_aceito_como_retorno()
    {
        const string code = Usings +
            """
            public class DoValueTaskChecked
            {
                [CommandValidation]
                internal ValueTask<Result> ValidarAsync() => ValueTask.FromResult(Result.Ok());

                [Command]
                public Result Executar() => Result.Ok();
            }
            """;

        Util.Compile(code, out var output, out var diagnostics);
        AssertNoErrors(output, diagnostics);

        var handler = HandlerSource(output, "DoValueTaskCheckedHandler");
        Assert.Contains("await command.ValidarAsync()", handler);
    }

    [Fact]
    public void Ordem_por_Order_com_desempate_deterministico_por_assinatura()
    {
        // C tem Order=5 (primeiro); A e B têm Order padrão 10 — desempate alfabético pela assinatura
        const string code = Usings +
            """
            public class DoOrdered
            {
                [CommandValidation]
                internal Result Beta() => Result.Ok();

                [CommandValidation]
                internal Result Alpha() => Result.Ok();

                [CommandValidation(Order = 5)]
                internal Result Gamma() => Result.Ok();

                [Command]
                public Result Executar() => Result.Ok();
            }
            """;

        Util.Compile(code, out var output, out var diagnostics);
        AssertNoErrors(output, diagnostics);

        var handler = HandlerSource(output, "DoOrderedHandler");
        var gammaIndex = handler.IndexOf("command.Gamma()", StringComparison.Ordinal);
        var alphaIndex = handler.IndexOf("command.Alpha()", StringComparison.Ordinal);
        var betaIndex = handler.IndexOf("command.Beta()", StringComparison.Ordinal);

        Assert.True(gammaIndex >= 0 && alphaIndex > gammaIndex && betaIndex > alphaIndex,
            $"Ordem inesperada: Gamma={gammaIndex}, Alpha={alphaIndex}, Beta={betaIndex}.{Environment.NewLine}{handler}");
    }

    [Fact]
    public void Dependencias_DI_sao_mescladas_sem_duplicar_campos_do_construtor()
    {
        const string code = Usings +
            """
            public interface IClock { }
            public interface IAudit { }

            public class DoMerged
            {
                [CommandValidation]
                internal Result Validar(IClock clock, IAudit audit) => Result.Ok();

                [Command]
                public Result Executar(IClock clock) => Result.Ok();
            }
            """;

        Util.Compile(code, out var output, out var diagnostics);
        AssertNoErrors(output, diagnostics);

        var handler = HandlerSource(output, "DoMergedHandler");

        // um único campo/parâmetro para 'clock'; 'audit' vem só do validator
        Assert.Equal(1, CountOccurrences(handler, "private readonly IClock clock;"));
        Assert.Equal(1, CountOccurrences(handler, "private readonly IAudit audit;"));
        Assert.Contains("DoMergedHandler(IClock clock, IAudit audit)", handler);
        Assert.Contains("command.Validar(clock, audit)", handler);
    }

    [Fact]
    public void WithParameter_do_validator_entra_no_handler_no_delegate_e_no_invoke()
    {
        const string code = Usings +
            """
            [MapPost("/", "do-external")]
            public class DoExternal
            {
                public string? Nome { get; set; }

                [CommandValidation]
                internal Result Validar([WithParameter] string origem) => Result.Ok();

                [Command]
                public Result Executar() => Result.Ok();
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);
        AssertNoErrors(output, diagnostics);

        var sources = output.SyntaxTrees.Skip(1).Select(tree => tree.ToString()).ToArray();
        var handlerInterface = sources.First(source => source.Contains("IDoExternalHandler"));
        Assert.Contains("Handle(DoExternal command, string origem)", handlerInterface);

        var endpoint = sources.First(source => source.Contains("MapEndpointsApi"));
        Assert.Contains("string origem", endpoint);
        Assert.Contains("handler.Handle(command, origem)", endpoint);
    }

    [Fact]
    public void Validators_rodam_uma_vez_fora_do_laco_de_retry()
    {
        const string code = Usings +
            """
            using RoyalCode.WorkContext;

            public class DoRetryChecked
            {
                public string? Nome { get; set; }

                [CommandValidation]
                internal Result Validar() => Result.Ok();

                [Command, WithWorkContext, WithRetryOnConcurrency]
                public async Task<Result> Executar(IWorkContext context, CancellationToken ct)
                {
                    await Task.CompletedTask;
                    return Result.Ok();
                }
            }
            """;

        Util.Compile(code, out var output, out var diagnostics);
        AssertNoErrors(output, diagnostics);

        var handler = HandlerSource(output, "DoRetryCheckedHandler");
        var validatorIndex = handler.IndexOf("validationResult1", StringComparison.Ordinal);
        var retryIndex = handler.IndexOf("RetryOnConcurrencyAsync", StringComparison.Ordinal);

        Assert.True(validatorIndex >= 0 && retryIndex > validatorIndex,
            $"O validator deve executar antes (fora) do laço de retry: validator={validatorIndex}, retry={retryIndex}.{Environment.NewLine}{handler}");
    }

    [Fact]
    public void Validators_rodam_antes_da_criacao_do_mediator_de_decorators()
    {
        const string code = Usings +
            """
            public class Some { }

            public class DoDecorated
            {
                [CommandValidation]
                internal Result Validar() => Result.Ok();

                [Command, WithDecorators]
                public Some Executar() => new Some();
            }
            """;

        Util.Compile(code, out var output, out var diagnostics);
        AssertNoErrors(output, diagnostics);

        var handler = HandlerSource(output, "DoDecoratedHandler");
        var validatorIndex = handler.IndexOf("validationResult1", StringComparison.Ordinal);
        var mediatorIndex = handler.IndexOf("Mediator<", StringComparison.Ordinal);

        Assert.True(validatorIndex >= 0 && mediatorIndex > validatorIndex,
            $"O validator deve executar antes dos decorators: validator={validatorIndex}, mediator={mediatorIndex}.{Environment.NewLine}{handler}");
    }

    [Fact]
    public void ProduceProblems_dos_validators_e_agregado_sem_duplicatas()
    {
        const string code = Usings +
            """
            [MapPost("/", "do-problems")]
            public class DoProblems
            {
                public string? Nome { get; set; }

                [CommandValidation, ProduceProblems(ProblemCategory.InvalidParameter, ProblemCategory.NotFound)]
                internal Result Validar() => Result.Ok();

                [Command, ProduceProblems(ProblemCategory.InvalidParameter)]
                public Result Executar() => Result.Ok();
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);
        AssertNoErrors(output, diagnostics);

        var endpoint = output.SyntaxTrees.Skip(1).Select(tree => tree.ToString())
            .First(source => source.Contains("MapEndpointsApi"));

        Assert.Contains("ProblemCategory.NotFound", endpoint);
        Assert.Equal(1, CountOccurrences(endpoint, "ProblemCategory.InvalidParameter"));
    }

    [Theory]
    [InlineData("internal static Result Validar() => Result.Ok();", "instance method")]
    [InlineData("internal Result Validar<T>() => Result.Ok();", "generic")]
    [InlineData("private Result Validar() => Result.Ok();", "accessible")]
    [InlineData("internal Result Validar(ref int valor) => Result.Ok();", "ref/out/in/params")]
    [InlineData("internal void Validar() { }", "must return Result")]
    [InlineData("internal async void Validar() { await Task.CompletedTask; }", "must return Result")]
    [InlineData("internal Result<string> Validar() => \"x\";", "must return Result")]
    [InlineData("internal Task Validar() => Task.CompletedTask;", "must return Result")]
    [InlineData("internal int Validar() => 0;", "must return Result")]
    public void Declaracao_invalida_produz_RCCMD038_e_nenhuma_fonte(string validator, string messageFragment)
    {
        var code = Usings +
            $$"""
            public class DoInvalid
            {
                [CommandValidation]
                {{validator}}

                [Command]
                public Result Executar() => Result.Ok();
            }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        var invalid = diagnostics.Where(d => d.Id == "RCCMD038").ToArray();
        Assert.Single(invalid);
        Assert.Contains(messageFragment, invalid[0].GetMessage(), StringComparison.Ordinal);
        Assert.NotEqual(Location.None, invalid[0].Location);
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(output.SyntaxTrees.Skip(1), tree => tree.ToString().Contains("DoInvalidHandler"));
    }

    [Fact]
    public void Metodo_do_comando_nao_pode_ser_validator()
    {
        const string code = Usings +
            """
            public class DoBoth
            {
                [Command, CommandValidation]
                public Result Executar() => Result.Ok();
            }
            """;

        Util.Compile(code, out _, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD038" &&
            d.GetMessage().Contains("cannot also be", StringComparison.Ordinal));
    }

    [Fact]
    public void Contexto_da_UoW_no_validator_produz_RCCMD039()
    {
        const string code = Usings +
            """
            public class Db : Microsoft.EntityFrameworkCore.DbContext { }

            public class DoForbidden
            {
                [CommandValidation]
                internal Result Validar(Db db) => Result.Ok();

                [Command, WithUnitOfWork<Db>]
                public Result Executar(Db db) => Result.Ok();
            }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        var forbidden = diagnostics.Where(d => d.Id == "RCCMD039").ToArray();
        Assert.Single(forbidden);
        Assert.Contains("db", forbidden[0].GetMessage(), StringComparison.Ordinal);
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(output.SyntaxTrees.Skip(1), tree => tree.ToString().Contains("DoForbiddenHandler"));
    }

    [Fact]
    public void Mesmo_nome_com_tipos_diferentes_produz_RCCMD040()
    {
        const string code = Usings +
            """
            public class DoConflict
            {
                [CommandValidation]
                internal Result Validar([WithParameter] int chave) => Result.Ok();

                [Command]
                public Result Executar([WithParameter] string chave) => Result.Ok();
            }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        var conflicts = diagnostics.Where(d => d.Id == "RCCMD040").ToArray();
        Assert.Single(conflicts);
        Assert.Contains("chave", conflicts[0].GetMessage(), StringComparison.Ordinal);
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(output.SyntaxTrees.Skip(1), tree => tree.ToString().Contains("DoConflictHandler"));
    }

    [Fact]
    public void CancellationToken_em_validator_sincrono_produz_RCCMD008()
    {
        const string code = Usings +
            """
            public class DoSyncToken
            {
                [CommandValidation]
                internal Result Validar(CancellationToken ct) => Result.Ok();

                [Command]
                public Result Executar() => Result.Ok();
            }
            """;

        Util.Compile(code, out _, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD008" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Parametro_do_validator_com_nome_reservado_produz_RCCMD029()
    {
        const string code = Usings +
            """
            public interface IServico { }

            public class DoReserved
            {
                [CommandValidation]
                internal Result Validar(IServico command) => Result.Ok();

                [Command]
                public Result Executar() => Result.Ok();
            }
            """;

        Util.Compile(code, out _, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "RCCMD029" &&
            d.GetMessage().Contains("command", StringComparison.Ordinal));
    }

    [Fact]
    public void Validator_em_declaracao_parcial_de_outro_arquivo_nao_derruba_o_generator()
    {
        // regressão da Fase 4: sintaxe em outra árvore exige o semantic model da árvore do parâmetro
        const string fileA = Usings +
            """
            public interface IServico { }

            public partial class DoPartialChecked
            {
                [Command]
                public Result Executar() => Result.Ok();
            }
            """;

        const string fileB =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Validations;

            public partial class DoPartialChecked
            {
                [CommandValidation]
                internal Result Validar(IServico servico) => Result.Ok();
            }
            """;

        var compilation = Util.CreateCompilation(fileA, fileB);
        var driver = Microsoft.CodeAnalysis.CSharp.CSharpGeneratorDriver.Create(
            new RoyalCode.SmartCommands.Generators.Generators.IncrementalGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        var handler = output.SyntaxTrees.Skip(2).Select(tree => tree.ToString())
            .FirstOrDefault(source => source.Contains("class DoPartialCheckedHandler")) ?? string.Empty;
        Assert.Contains("command.Validar(servico)", handler);
    }

    [Fact]
    public void WithParameter_compartilhado_entre_comando_e_validator_e_deduplicado_no_delegate()
    {
        const string code = Usings +
            """
            [MapPost("/", "do-shared")]
            public class DoShared
            {
                public string? Nome { get; set; }

                [CommandValidation]
                internal Result Validar([WithParameter] string origem) => Result.Ok();

                [Command]
                public Result Executar([WithParameter] string origem) => Result.Ok();
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);
        AssertNoErrors(output, diagnostics);

        var sources = output.SyntaxTrees.Skip(1).Select(tree => tree.ToString()).ToArray();
        var handlerInterface = sources.First(source => source.Contains("IDoSharedHandler"));
        Assert.Equal(1, CountOccurrences(handlerInterface, "string origem"));

        var endpoint = sources.First(source => source.Contains("MapEndpointsApi"));
        Assert.Equal(1, CountOccurrences(endpoint, "string origem"));
        Assert.Contains("handler.Handle(command, origem)", endpoint);
    }

    [Fact]
    public void Binding_explicito_do_WithParameter_do_validator_e_copiado_ao_delegate()
    {
        const string code = Usings +
            """
            [MapPost("/", "do-bound-validator")]
            public class DoBoundValidator
            {
                public string? Nome { get; set; }

                [CommandValidation]
                internal Result Validar([WithParameter, Microsoft.AspNetCore.Mvc.FromHeader(Name = "x-key")] string chave) => Result.Ok();

                [Command]
                public Result Executar() => Result.Ok();
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);
        AssertNoErrors(output, diagnostics);

        var sources = output.SyntaxTrees.Skip(1).Select(tree => tree.ToString()).ToArray();
        var endpoint = sources.First(source => source.Contains("MapEndpointsApi"));
        Assert.Contains("[FromHeader(Name = \"x-key\")]", endpoint);

        // DF3: o binding não vai para a interface do handler
        var handlerInterface = sources.First(source => source.Contains("IDoBoundValidatorHandler"));
        Assert.DoesNotContain("FromHeader", handlerInterface);
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
