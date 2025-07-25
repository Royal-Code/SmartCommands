namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Attribute used to reference an entity and its ID type.
/// </para>
/// <para>
///     Used together with <see cref="MapFindAttribute"/>.
/// </para>
/// </summary>
/// <typeparam name="TEntity"></typeparam>
/// <typeparam name="TId"></typeparam>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class EntityReferenceAttribute<TEntity, TId> : Attribute
    where TEntity : class
{ }
