namespace RoyalCode.SmartCommands.Generators.Generators;

/// <summary>
/// <para>
///     Um método de validação adicional do comando (DF13), descoberto no transform: nome, contrato assíncrono
///     (pelo tipo de retorno) e parâmetros já classificados (token, <c>[WithParameter]</c> ou dependência DI).
/// </para>
/// <para>
///     Classe de trabalho da emissão; o pipeline retém <c>CommandValidationModel</c> (symbol-free).
/// </para>
/// </summary>
internal sealed class CommandValidationInformation : IEquatable<CommandValidationInformation>
{
    public CommandValidationInformation(string methodName, bool isAwaitable, List<ParameterDescriptor> parameters)
    {
        MethodName = methodName;
        IsAwaitable = isAwaitable;
        Parameters = parameters;
    }

    public string MethodName { get; }

    /// <summary>Se retorna <c>Task&lt;Result&gt;</c>/<c>ValueTask&lt;Result&gt;</c> (sempre aguardado).</summary>
    public bool IsAwaitable { get; }

    public List<ParameterDescriptor> Parameters { get; }

    public bool Equals(CommandValidationInformation? other)
    {
        if (other is null)
            return false;

        return ReferenceEquals(this, other) ||
            MethodName == other.MethodName &&
            IsAwaitable == other.IsAwaitable &&
            Parameters.SequenceEqual(other.Parameters);
    }

    public override bool Equals(object? obj) => obj is CommandValidationInformation other && Equals(other);

    public override int GetHashCode()
    {
        var hashCode = -1521134295;
        hashCode = hashCode * -1521134295 + MethodName.GetHashCode();
        hashCode = hashCode * -1521134295 + IsAwaitable.GetHashCode();
        foreach (var parameter in Parameters)
            hashCode = hashCode * -1521134295 + parameter.GetHashCode();
        return hashCode;
    }
}
