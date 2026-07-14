using System.Diagnostics;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Opts the command into model validation before the command method runs. The command class must declare a
///     <c>HasProblems</c> validation method following the pattern expected by the source generator; the generated
///     handler calls it and short-circuits, returning the resulting problems, when validation fails.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
[Conditional("COMPILE_TIME_ONLY")]
public class WithValidateModelAttribute : Attribute { }