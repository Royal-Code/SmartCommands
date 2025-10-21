namespace RoyalCode.SmartCommands;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class SearchReferenceAttribute<TEntity> : Attribute
    where TEntity : class
{ }

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class SearchReferenceAttribute<TEntity, TModel> : Attribute
    where TEntity : class
    where TModel : class
{ }