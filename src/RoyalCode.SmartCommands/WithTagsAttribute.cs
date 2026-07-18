namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Adds OpenAPI tags to the generated Minimal API endpoint, preserving the declared order — the generator
///     emits <c>.WithTags(...)</c> on the endpoint. Can be used on command classes mapped by the HTTP verbs,
///     on <see cref="MapFindAttribute"/> classes and on <see cref="MapSearchAttribute"/> classes.
/// </para>
/// <para>
///     At least one tag is required and tags must not be empty or whitespace (RCCMD041).
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class WithTagsAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WithTagsAttribute"/> class.
    /// </summary>
    /// <param name="tags">One or more non-empty tags, emitted in the declared order.</param>
    public WithTagsAttribute(params string[] tags) { }
}
