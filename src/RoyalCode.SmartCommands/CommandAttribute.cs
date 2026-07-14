using System.Diagnostics;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Marks the method that contains the business logic of the command. The source generator uses this method
///     to generate the handler interface and implementation for the command class.
/// </para>
/// <para>
///     Combine with attributes such as <see cref="WithUnitOfWorkAttribute{T}"/>, <see cref="WithFindEntitiesAttribute{T}"/>,
///     <see cref="WithDecoratorsAttribute"/>, and <see cref="WithValidateModelAttribute"/> to customize the generated handler.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
[Conditional("COMPILE_TIME_ONLY")]
public class CommandAttribute : Attribute { }
