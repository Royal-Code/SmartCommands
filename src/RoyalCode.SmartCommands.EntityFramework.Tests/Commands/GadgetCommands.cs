using RoyalCode.SmartCommands.EntityFramework.Tests.Support;
using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.EntityFramework.Tests.Commands;

/// <summary>
/// Comando real processado pelo generator (referenciado como analyzer): cria um gadget
/// via unit of work do adapter EF (<c>DbContextAccessor&lt;TestDbContext&gt;</c>).
/// </summary>
[MapGroup("gadgets")]
[MapPost("/", "create-gadget")]
[MapCreatedRoute("/{0}", nameof(Gadget.Id))]
public partial class CriarGadget
{
    public string? Nome { get; set; }

    [Command, ProduceNewEntity, WithUnitOfWork<TestDbContext>]
    internal Gadget Criar() => new() { Id = Guid.NewGuid(), Nome = Nome ?? string.Empty };
}

/// <summary>
/// Comando de edição: o handler gerado carrega o gadget pelo accessor, o comando muta a entidade
/// e o <c>CompleteAsync</c> salva. <see cref="AntesDeSalvar"/> permite intercalar uma atualização
/// rival entre o find e o save para produzir um conflito otimista real.
/// </summary>
public partial class RenomearGadget
{
    public string? NovoNome { get; set; }

    /// <summary>Hook de teste executado entre o find e o save; não participa do payload HTTP.</summary>
    public Action? AntesDeSalvar { get; set; }

    [Command, WithUnitOfWork<TestDbContext>, EditEntity<Gadget, Guid>]
    internal Result Executar(Gadget gadget)
    {
        AntesDeSalvar?.Invoke();

        gadget.Nome = NovoNome ?? string.Empty;
        gadget.Versao++;
        return Result.Ok();
    }
}

/// <summary>
/// Comando com <c>[WithTransaction]</c> (DF21): exige transação para este comando mesmo quando
/// a opção global <c>BeginTransactions</c> está desligada.
/// </summary>
public partial class CriarGadgetTransacional
{
    public string? Nome { get; set; }

    [Command, ProduceNewEntity, WithUnitOfWork<TestDbContext>, WithTransaction]
    internal Gadget Criar() => new() { Id = Guid.NewGuid(), Nome = Nome ?? string.Empty };
}

/// <summary>
/// Host do registro de DI gerado (<c>AddGadgetHandlersServices</c>).
/// </summary>
[MapApiHandlers, AddHandlersServices("Gadget")]
public static partial class GadgetServices;
