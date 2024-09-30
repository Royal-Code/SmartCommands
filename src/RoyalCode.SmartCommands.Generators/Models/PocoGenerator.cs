using System.Text;
using Coreum.NewCommands.Generators.Models.Descriptors;
using Microsoft.CodeAnalysis;

namespace Coreum.NewCommands.Generators.Models;

public class PocoGenerator: IGenerator
{
    private readonly UsingsGenerator usings = new();
    private readonly GeneratorNodeList properties = new();
    private readonly ParametersGenerator primaryConstructor = new();
    private ModifiersGenerator? modifiers;

    public PocoGenerator(string name, string ns, string typeType = "class")
    {
        Name = name;
        Namespace = ns;
        TypeType = typeType;
    }

    public UsingsGenerator Usings => usings;

    public string Namespace { get; set; }

    public ModifiersGenerator Modifiers => modifiers ??= new();

    public string TypeType { get; }

    public string Name { get; set; }

    /// <summary>
    /// Nome do arquivo que será gerado, opcional, será utilizado o padrão: <c>"{Name}.g.cs"</c>.
    /// </summary>
    public string? FileName { get; set; }

    public void AddProperty(PropertyDescriptor propertyDescriptor)
    {
        // primeiro, gera o parâmetro para o construtor primário
        string paramName = propertyDescriptor.Name.ToLowerCamelCase();
        var paramDescriptor = new ParameterDescriptor(propertyDescriptor.Type, paramName);
        var parameter = new ParameterGenerator(paramDescriptor);
        primaryConstructor.Add(parameter);

        // depois, gera a propriedade
        var property = new PropertyGenerator(propertyDescriptor.Type.Name, propertyDescriptor.Name)
        {
            CanSet = false,
            Value = new StringValueNode(paramName)
        };

        properties.Add(property);

        // adiciona o using
        Usings.AddNamespaces(propertyDescriptor.Type.Namespaces);
    }

    public void Generate(SourceProductionContext spc)
    {
        var builder = new StringBuilder();

        Write(builder);

        var source = builder.ToString();

        spc.AddSource(FileName ?? $"{Name}.g.cs", source);
    }

    public bool HasErrors(SourceProductionContext spc, SyntaxToken mainToken) => false;

    protected void Write(StringBuilder sb)
    {
        usings.ExcludeNamespace(Namespace);
        usings.Write(sb);

        sb.AppendLine();
        sb.Append("namespace ").Append(Namespace).AppendLine(";");
        sb.AppendLine();

        modifiers?.Write(sb);
        sb.Append(TypeType).Append(" ").Append(Name);

        primaryConstructor.Write(sb);

        sb.AppendLine();
        sb.Append("{");

        properties.Write(sb, 1);

        sb.AppendLine("}");
    }
}