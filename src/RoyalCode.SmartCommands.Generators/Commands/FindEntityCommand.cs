using System.Text;

namespace RoyalCode.SmartCommands.Generators.Commands;

public class FindEntityCommand : GeneratorNode, IWithNamespaces
{
    private readonly ParameterDescriptor parameter;
    private readonly PropertyDescriptor property;
    private readonly string accessorVarName;
    private readonly string modelVarName;

    public FindEntityCommand(
        ParameterDescriptor parameter, 
        PropertyDescriptor property, 
        string accessorVarName,
        string modelVarName)
    {
        this.parameter = parameter;
        this.property = property;
        this.accessorVarName = accessorVarName;
        this.modelVarName = modelVarName;
    }

    public IEnumerable<string> GetNamespaces()
    {
        foreach (var ns in parameter.Type.Namespaces)
            yield return ns;
        foreach (var ns in property.Type.Namespaces)
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
        bool entityVarDeclared = false;

        // quando a propriedade do comando pode ser nula,
        // deve ser feito um if para verificar se se deve executar o find.
        if (property.Type.MayBeNull)
        {
            // a variável do parâmetro deve ser declarada antes do if
            sb.Indent(indent);
            sb.Append(parameter.Type.Name).Append(' ').Append(parameter.Name).Append(" = null;").AppendLine();
            entityVarDeclared = true;

            // declaração do if
            sb.Indent(indent);
            sb.Append("if (").Append(modelVarName).Append('.').Append(property.Name).Append(" is not null)").AppendLine();
            sb.Indent(indent);
            sb.AppendLine("{");

            indent++;
        }

        sb.Indent(indent);
        sb.Append("var ").Append(parameter.Name).Append("Entry = ")
            .Append("await this.").Append(accessorVarName).Append(".FindEntityAsync<")
            .Append(parameter.Type.UnderlyingType).Append(", ")
            .Append(property.Type.UnderlyingType).Append(">(")
            .Append(modelVarName).Append('.').Append(property.Name);

        if (property.Type.IsNullable)
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

        if (property.Type.MayBeNull)
        {
            indent--;
            sb.Indent(indent);
            sb.AppendLine("}");
        }

        sb.AppendLine();
    }
}