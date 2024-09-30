using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Coreum.NewCommands;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
[Conditional("COMPILE_TIME_ONLY")]
public class EditEntityAttribute<TEntity, TId> : Attribute
    where TEntity : class
{ }