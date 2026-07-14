using System.Diagnostics;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Marks the method, in a class decorated with <see cref="SearchReferenceAttribute{TEntity}"/> or
///     <see cref="SearchReferenceAttribute{TEntity, TModel}"/>, that applies custom filtering logic to the
///     search query. The generated search handler invokes this method to build the filtered query.
/// </para>
/// <para>
///     Only one method per search command class can be marked with this attribute.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
[Conditional("COMPILE_TIME_ONLY")]
public class WithFilterAttribute : Attribute { }
