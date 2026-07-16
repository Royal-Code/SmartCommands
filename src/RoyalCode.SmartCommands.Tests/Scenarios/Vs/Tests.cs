using Microsoft.CodeAnalysis;
using Xunit;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Vs;

/// <summary>
/// <para>
///     Runtime das validações adicionais (DF13) sobre o espelho manual do handler gerado: ordem,
///     short-circuit, cancelamento e dependência compartilhada. O primeiro teste garante a paridade do
///     espelho com a saída real do generator.
/// </para>
/// </summary>
public class Tests
{
    [Fact]
    public void O_handler_gerado_corresponde_ao_espelho_manual()
    {
        Util.Compile(ValidatedCommandCode.Command, out var output, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedInterface = output.SyntaxTrees.Skip(1).FirstOrDefault()?.ToString();
        Assert.Equal(Util.GeneratedCode(ValidatedCommandCode.Interface), generatedInterface);

        var generatedHandler = output.SyntaxTrees.Skip(2).FirstOrDefault()?.ToString();
        Assert.Equal(Util.GeneratedCode(ValidatedCommandCode.Handler), generatedHandler);
    }

    [Fact]
    public async Task Sucesso_executa_validators_em_ordem_e_depois_o_comando()
    {
        var probe = new ValidationProbe();
        var handler = new ValidatedCommandHandler(probe);
        var command = new ValidatedCommand { Nome = "ok" };

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.HasProblems(out _));
        Assert.True(command.Executed);
        Assert.Equal(["first", "second", "execute"], probe.Steps);
    }

    [Fact]
    public async Task Falha_no_primeiro_validator_interrompe_antes_do_segundo_e_do_comando()
    {
        var probe = new ValidationProbe();
        var handler = new ValidatedCommandHandler(probe);
        var command = new ValidatedCommand { FailFirst = true };

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.HasProblems(out var problems));
        Assert.Contains("first failed", problems!.First().Detail);
        Assert.False(command.Executed);
        Assert.Equal(["first"], probe.Steps);
    }

    [Fact]
    public async Task Falha_no_segundo_validator_interrompe_antes_do_comando()
    {
        var probe = new ValidationProbe();
        var handler = new ValidatedCommandHandler(probe);
        var command = new ValidatedCommand { FailSecond = true };

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.HasProblems(out var problems));
        Assert.Contains("second failed", problems!.First().Detail);
        Assert.False(command.Executed);
        Assert.Equal(["first", "second"], probe.Steps);
    }

    [Fact]
    public async Task Cancelamento_e_observado_pelo_validator_assincrono()
    {
        var probe = new ValidationProbe();
        var handler = new ValidatedCommandHandler(probe);
        var command = new ValidatedCommand();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => handler.HandleAsync(command, cts.Token));

        Assert.False(command.Executed);
    }
}
