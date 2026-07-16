using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Vs;

/// <summary>Sonda para observar as chamadas dos validators e do comando em runtime (DF13).</summary>
public sealed class ValidationProbe
{
    public List<string> Steps { get; } = [];

    public void Registrar(string step) => Steps.Add(step);
}

/// <summary>
/// Comando com validações adicionais (DF13): ordem por <c>Order</c>, short-circuit no primeiro problema,
/// validator assíncrono com <c>CancellationToken</c> e dependência de DI compartilhada com o comando.
/// </summary>
public class ValidatedCommand
{
    public string? Nome { get; set; }

    public bool FailFirst { get; set; }

    public bool FailSecond { get; set; }

    public bool Executed { get; private set; }

    [CommandValidation(Order = 5)]
    internal Result Primeiro(ValidationProbe probe)
    {
        probe.Registrar("first");
        return FailFirst
            ? Problems.InvalidParameter("first failed")
            : Result.Ok();
    }

    [CommandValidation]
    internal async Task<Result> SegundoAsync(ValidationProbe probe, CancellationToken ct)
    {
        await Task.Yield();
        ct.ThrowIfCancellationRequested();
        probe.Registrar("second");
        return FailSecond
            ? Problems.InvalidParameter("second failed")
            : Result.Ok();
    }

    [Command]
    internal Result Executar(ValidationProbe probe)
    {
        Executed = true;
        probe.Registrar("execute");
        return Result.Ok();
    }
}

// espelho manual do handler gerado (o generator não roda sobre este assembly);
// a paridade com a saída real é garantida pelo teste de comparação em Tests.cs
public interface IValidatedCommandHandler
{
    public Task<Result> HandleAsync(ValidatedCommand command, CancellationToken ct);
}

public class ValidatedCommandHandler : IValidatedCommandHandler
{
    private readonly ValidationProbe probe;

    public ValidatedCommandHandler(ValidationProbe probe)
    {
        this.probe = probe;
    }

    public async Task<Result> HandleAsync(ValidatedCommand command, CancellationToken ct)
    {
        var validationResult1 = command.Primeiro(probe);
        if (validationResult1.HasProblems(out var validationProblems1))
            return validationProblems1;

        var validationResult2 = await command.SegundoAsync(probe, ct);
        if (validationResult2.HasProblems(out var validationProblems2))
            return validationProblems2;

        return command.Executar(probe);
    }
}

/// <summary>Fixtures para comparar a saída real do generator com o espelho manual acima.</summary>
public static class ValidatedCommandCode
{
    public const string Command =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.Vs;

public sealed class ValidationProbe
{
    public System.Collections.Generic.List<string> Steps { get; } = [];

    public void Registrar(string step) => Steps.Add(step);
}

public class ValidatedCommand
{
    public string? Nome { get; set; }

    public bool FailFirst { get; set; }

    public bool FailSecond { get; set; }

    public bool Executed { get; private set; }

    [CommandValidation(Order = 5)]
    internal Result Primeiro(ValidationProbe probe)
    {
        probe.Registrar("first");
        return FailFirst
            ? Problems.InvalidParameter("first failed")
            : Result.Ok();
    }

    [CommandValidation]
    internal async Task<Result> SegundoAsync(ValidationProbe probe, CancellationToken ct)
    {
        await Task.Yield();
        ct.ThrowIfCancellationRequested();
        probe.Registrar("second");
        return FailSecond
            ? Problems.InvalidParameter("second failed")
            : Result.Ok();
    }

    [Command]
    internal Result Executar(ValidationProbe probe)
    {
        Executed = true;
        probe.Registrar("execute");
        return Result.Ok();
    }
}
""";

    public const string Interface =
"""
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.Vs;

public interface IValidatedCommandHandler
{
    public Task<Result> HandleAsync(ValidatedCommand command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using RoyalCode.SmartProblems;
using Tests.Scenarios.Vs;

namespace Tests.Scenarios.Vs.Internals;

public class ValidatedCommandHandler : IValidatedCommandHandler
{
    private readonly ValidationProbe probe;

    public ValidatedCommandHandler(ValidationProbe probe)
    {
        this.probe = probe;
    }

    public async Task<Result> HandleAsync(ValidatedCommand command, CancellationToken ct)
    {
        var validationResult1 = command.Primeiro(probe);
        if (validationResult1.HasProblems(out var validationProblems1))
            return validationProblems1;

        var validationResult2 = await command.SegundoAsync(probe, ct);
        if (validationResult2.HasProblems(out var validationProblems2))
            return validationProblems2;

        return command.Executar(probe);
    }
}

""";
}
