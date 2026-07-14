using System.Diagnostics;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Opts the command into a decorator pipeline: the generated handler resolves the registered
///     <see cref="IDecorator{TModel, TResult}"/> services for the command and result types and executes them,
///     via <see cref="Mediator{TModel, TResult}"/>, before invoking the command method.
/// </para>
/// <para>
///     The command method must return a value (it cannot be <see langword="void"/> or a value-less <c>Task</c>).
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
[Conditional("COMPILE_TIME_ONLY")]
public class WithDecoratorsAttribute : Attribute { }