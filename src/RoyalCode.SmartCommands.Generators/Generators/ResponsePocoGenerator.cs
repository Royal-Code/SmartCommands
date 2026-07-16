using System.Text;
using Microsoft.CodeAnalysis;

namespace RoyalCode.SmartCommands.Generators.Generators;

/// <summary>
/// <see cref="PocoGenerator"/> que emite o cabeçalho padrão de arquivo gerado
/// (<c>// &lt;auto-generated/&gt;</c> + <c>#nullable enable</c>) antes do corpo — usado para os POCOs de
/// resposta (<c>MapResponseValues</c>), que também são fonte gerada.
/// </summary>
internal sealed class ResponsePocoGenerator : PocoGenerator
{
    public ResponsePocoGenerator(string name, string ns)
        : base(name, ns)
    {
    }

    public new void Generate(SourceProductionContext spc)
    {
        var builder = new StringBuilder();

        GeneratedFileHeader.WriteTo(builder);
        Write(builder);

        spc.AddSource(FileName ?? $"{Name}.g.cs", builder.ToString());
    }
}
