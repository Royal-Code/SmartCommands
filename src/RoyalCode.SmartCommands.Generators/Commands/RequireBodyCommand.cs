using System.Text;

namespace RoyalCode.SmartCommands.Generators.Commands;

/// <summary>
/// <para>
///     Guards a generated minimal-API endpoint against a missing request body: when the command bound from the
///     body is <c>null</c> (e.g. the request was sent without a body), returns a <c>400 InvalidParameter</c> problem
///     instead of dereferencing null further down the handler.
/// </para>
/// </summary>
internal class RequireBodyCommand : GeneratorNode, IWithNamespaces
{
    private readonly string commandVarName;
    private readonly string detail;

    public RequireBodyCommand(string commandVarName, string detail)
    {
        this.commandVarName = commandVarName;
        this.detail = detail;
    }

    public IEnumerable<string> GetNamespaces()
    {
        yield return "RoyalCode.SmartProblems";
    }

    public override void Write(StringBuilder sb, int indent = 0)
    {
        sb.Indent(indent).Append("if (").Append(commandVarName).AppendLine(" is null)");
        sb.Indent(indent + 1).Append("return Problems.InvalidParameter(\"").Append(detail).AppendLine("\");");
        sb.AppendLine();
    }
}
