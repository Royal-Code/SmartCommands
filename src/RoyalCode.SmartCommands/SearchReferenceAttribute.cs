namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Used together with <see cref="MapSearchAttribute"/> to indicate that the search command queries
///     entities of type <typeparamref name="TEntity"/>, returning the entity itself as the search result.
/// </para>
/// <para>
///     Combine with <see cref="WithFilterAttribute"/> to apply custom filtering logic to the query.
/// </para>
/// </summary>
/// <typeparam name="TEntity">The type of the entity being searched.</typeparam>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class SearchReferenceAttribute<TEntity> : Attribute
    where TEntity : class
{ }

/// <summary>
/// <para>
///     Used together with <see cref="MapSearchAttribute"/> to indicate that the search command queries
///     entities of type <typeparamref name="TEntity"/>, projecting each result to <typeparamref name="TModel"/>.
/// </para>
/// <para>
///     Combine with <see cref="WithFilterAttribute"/> to apply custom filtering logic to the query.
/// </para>
/// </summary>
/// <typeparam name="TEntity">The type of the entity being searched.</typeparam>
/// <typeparam name="TModel">The type each entity is projected to in the search result.</typeparam>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class SearchReferenceAttribute<TEntity, TModel> : Attribute
    where TEntity : class
    where TModel : class
{ }