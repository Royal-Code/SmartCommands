using System.Text;

namespace RoyalCode.SmartCommands.Generators.Commands;

/// <summary>
/// <para>
///     Writes the call to the WorkContext optimistic-concurrency retry primitive, wrapping the unit-of-work body
///     (<c>{ Begin → find entities → Execute → Complete }</c>) in an async lambda passed to
///     <c>RetryOnConcurrencyAsync</c>.
/// </para>
/// <para>
///     The model validation is intentionally kept outside of this node (and therefore outside the retry loop).
/// </para>
/// </summary>
public class RetryOnConcurrencyCommand : GeneratorNode, IWithNamespaces
{
    private const string OptionsNamespace = "RoyalCode.SmartCommands.WorkContext.Options";

    private readonly GeneratorNode body;
    private readonly string accessorVarName;
    private readonly string optionsArgument;
    private readonly string? onExhaustedArgument;

    public RetryOnConcurrencyCommand(
        GeneratorNode body,
        string accessorVarName,
        string optionsArgument,
        string? onExhaustedArgument = null)
    {
        this.body = body;
        this.accessorVarName = accessorVarName;
        this.optionsArgument = optionsArgument;
        this.onExhaustedArgument = onExhaustedArgument;
    }

    public IEnumerable<string> GetNamespaces()
    {
        yield return OptionsNamespace;

        if (body is IWithNamespaces withNamespaces)
            foreach (var ns in withNamespaces.GetNamespaces())
                yield return ns;
    }

    public override void Write(StringBuilder sb, int indent = 0)
    {
        sb.Indent(indent).Append("return await this.").Append(accessorVarName)
            .AppendLine(".Context.RetryOnConcurrencyAsync(");
        sb.Indent(indent + 1).AppendLine("async () =>");
        sb.Indent(indent + 1).AppendLine("{");
        body.Write(sb, indent + 2);
        sb.Indent(indent + 1).AppendLine("},");
        sb.Indent(indent + 1).Append(optionsArgument).AppendLine(",");
        if (onExhaustedArgument is not null)
            sb.Indent(indent + 1).Append("onExhausted: () => ").Append(onExhaustedArgument).AppendLine(",");
        sb.Indent(indent + 1).AppendLine("ct: ct);");
    }
}
