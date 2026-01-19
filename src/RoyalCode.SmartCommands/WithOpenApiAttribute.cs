using System.Diagnostics;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Used with <see cref="MapApiHandlersAttribute"/> to indicate that the generated endpoints should include OpenAPI documentation.
/// </para>
/// <para>
///     This attribute is applied to classes marked with <see cref="MapApiHandlersAttribute"/>.
/// </para>
/// <para>
///     After .Net 10 the OpenAPI documentation is automatically generated for minimal APIs, 
///     so this attribute is not necessary in most scenarios.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
[Conditional("COMPILE_TIME_ONLY")]
#if NET10_0_OR_GREATER
[Obsolete("After .Net 10 the OpenAPI documentation is automatically generated for minimal APIs, so this attribute is not necessary in most scenarios.")]
#endif
public class WithOpenApiAttribute : Attribute { }
