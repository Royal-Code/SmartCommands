using FluentAssertions;
using Microsoft.CodeAnalysis;

namespace Coreum.NewCommands.Tests.Generators;

public class GenerateWasValidatedTests
{
    [Fact]
    public void MyCommand_Partial_Must_Generate_WasValidated()
    {
        Util.Compile(Code.MyCommand, out var output, out var diagnostics);

        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedInterface = output.SyntaxTrees.Skip(1).FirstOrDefault()?.ToString();
        generatedInterface.Should().Be(Code.MyCommandInterface);

        var generatedHandler = output.SyntaxTrees.Skip(2).FirstOrDefault()?.ToString();
        generatedHandler.Should().Be(Code.MyCommandHandler);

        var generatedPartial = output.SyntaxTrees.Skip(3).FirstOrDefault()?.ToString();
        generatedPartial.Should().Be(Code.MyCommandWasValidated);
    }
}

file static class Code
{
    public const string MyCommand =
"""
using Coreum.NewCommands;
using Coreum.NewCommands.Tests.Models;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using RoyalCode.WorkContext.Abstractions;
using System.Diagnostics.CodeAnalysis;

namespace NewCommands.Tests;

public partial class MyCommand
{
    public string? Nome { get; set; }

    [MemberNotNullWhen(false, nameof(Nome))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<MyCommand>()
            .NotEmpty(Nome)
            .HasProblems(out problems);
    }

    [Command, WithDecorators, WithValidateModel]
    internal async Task<Result> ExecuteAsync(IWorkContext context, CancellationToken ct)
    {
        WasValidated();

        var entity = new Produto(Nome);
        context.Add(entity);
        return await context.SaveAsync(ct);
    }
}
""";

    public const string MyCommandInterface =
"""
using RoyalCode.SmartProblems;

namespace NewCommands.Tests;

public interface IMyCommandHandler
{
    public Task<Result> HandleAsync(MyCommand command, CancellationToken ct);
}

""";

    public const string MyCommandHandler =
"""
using Coreum.NewCommands;
using NewCommands.Tests;
using RoyalCode.SmartProblems;
using RoyalCode.WorkContext.Abstractions;

namespace NewCommands.Tests.Internals;

public class MyCommandHandler : IMyCommandHandler
{
    private readonly IEnumerable<IDecorator<MyCommand, Result>> decorators;
    private readonly IWorkContext context;

    public MyCommandHandler(IEnumerable<IDecorator<MyCommand, Result>> decorators, IWorkContext context)
    {
        this.decorators = decorators;
        this.context = context;
    }

    public async Task<Result> HandleAsync(MyCommand command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        var decoratorsMediator = new Mediator<MyCommand, Result>(
            this.decorators,
            async () => await command.ExecuteAsync(context, ct),
            command,
            ct);

        return await decoratorsMediator.NextAsync();
    }
}

""";

    public const string MyCommandWasValidated =
"""

#nullable disable
#pragma warning disable

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace NewCommands.Tests;

public partial class MyCommand
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [MemberNotNull(nameof(Nome))]
    internal protected void WasValidated() { }
}

""";
}