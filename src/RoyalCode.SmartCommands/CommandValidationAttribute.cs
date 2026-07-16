using System.Diagnostics;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Marks an instance method of the command class as an additional validation (DF13). The generated handler
///     invokes the validators after <c>HasProblems</c> (when <see cref="WithValidateModelAttribute"/> is used)
///     and before opening the unit of work or entering the concurrency retry loop — each validator runs once,
///     even when the command is retried.
/// </para>
/// <para>
///     A validator must return <c>Result</c>, <c>Task&lt;Result&gt;</c> or <c>ValueTask&lt;Result&gt;</c>;
///     asynchronous validators are always awaited. The first failed <c>Result</c> short-circuits the handler.
///     Parameters are resolved with the same model as the command method: <c>CancellationToken</c> (only on
///     asynchronous validators), services from dependency injection, and external values via
///     <see cref="WithParameterAttribute"/>. Entities, unit-of-work contexts and accessors are not available
///     at this stage (the validation runs before any loading).
/// </para>
/// <para>
///     Validators execute ordered by <see cref="Order"/> (default <c>10</c>). Validators with the same order
///     have no user-observable precedence between them; the generator only guarantees a deterministic output.
/// </para>
/// <para>Example:</para>
/// <para>
/// <code>
/// public class CriarProduto
/// {
///     public string? Sku { get; set; }
///
///     [CommandValidation]
///     internal async Task{Result} ValidarSkuAsync(ISkuService skus, CancellationToken ct)
///         => await skus.ValidarAsync(Sku, ct);
///
///     [Command, WithUnitOfWork{AppDbContext}]
///     internal Produto Executar() => new Produto { Sku = Sku! };
/// }
/// </code>
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
[Conditional("COMPILE_TIME_ONLY")]
public class CommandValidationAttribute : Attribute
{
    /// <summary>
    /// The execution order of the validator, default <c>10</c>. Lower values run first; validators with the
    /// same order have no user-observable precedence between them.
    /// </summary>
    public int Order { get; set; } = 10;
}
