using System.Text;

namespace RoyalCode.SmartCommands.Generators.Commands;

internal class FindEditEntityCommand : GeneratorNode, IWithNamespaces
{
    private readonly EditTypeDescriptor descriptor;
    private readonly string accessorVarName;

    public FindEditEntityCommand(
        EditTypeDescriptor descriptor, 
        string accessorVarName)
    {
        this.descriptor = descriptor;
        this.accessorVarName = accessorVarName;
    }

    public IEnumerable<string> GetNamespaces()
    {
        if (descriptor.Parameter is not null)
            foreach (var ns in descriptor.Parameter.Type.Namespaces)
                yield return ns;
        foreach (var ns in descriptor.IdType.Namespaces)
            yield return ns;
    }

    /// <summary>
    /// <code>
    /// var personaEntry = this.accessor.FindEntityAsync{Persona, Guid}(model.PersonaId, ct);
    /// if (personaEntry.NotFound(out notFoundProblems))
    ///     return notFound;
    /// var persona = personaEntry.Entity;
    /// </code>
    /// </summary>
    /// <param name="sb"></param>
    /// <param name="indent"></param>
    public override void Write(StringBuilder sb, int indent = 0)
    {
        var parameter = descriptor.Parameter
            ?? throw new InvalidOperationException("An edit command must have an entity parameter before source generation.");

        bool entityVarDeclared = false;
        var idParamName = $"{parameter.Name}Id";

        // quando a propriedade do comando pode ser nula,
        // deve ser feito um if para verificar se se deve executar o find.
        if (descriptor.IdType.MayBeNull)
        {
            // a variável do parâmetro deve ser declarada antes do if
            sb.Indent(indent);
            sb.Append(parameter.Name).Append(' ').Append(parameter.Name).Append(" = null;").AppendLine();
            entityVarDeclared = true;

            // declaração do if
            sb.Indent(indent);
            sb.Append("if (").Append(idParamName).Append(" is not null)").AppendLine();
            sb.Indent(indent);
            sb.AppendLine("{");

            indent++;
        }

        sb.Indent(indent);
        sb.Append("var ").Append(parameter.Name).Append("Entry = ")
            .Append("await this.").Append(accessorVarName).Append(".FindEntityAsync<")
            .Append(parameter.Type.UnderlyingType).Append(", ")
            .Append(descriptor.IdType.UnderlyingType).Append(">(")
            .Append(idParamName);

        if (descriptor.IdType.IsNullable)
            sb.Append(".Value");

        sb.Append(", ct);")
            .AppendLine();

        sb.Indent(indent);
        sb.Append("if (").Append(parameter.Name).Append("Entry.NotFound(out notFoundProblem))").AppendLine();

        sb.IndentPlus(indent);
        sb.AppendLine("return notFoundProblem;");

        sb.Indent(indent);
        if (!entityVarDeclared)
            sb.Append("var ");
        sb.Append(parameter.Name).Append(" = ").Append(parameter.Name).AppendLine("Entry.Entity;");

        if (descriptor.IdType.MayBeNull)
        {
            indent--;
            sb.Indent(indent);
            sb.AppendLine("}");
        }

        sb.AppendLine();
    }
}
