﻿using Microsoft.CodeAnalysis;
using System.Text;

namespace Coreum.NewCommands.Generators.Models;

public class LambdaGenerator : GeneratorNode
{
    private GeneratorNodeList? commands;

    public ArgumentsGenerator Parameters { get; } = new();

    public GeneratorNodeList Commands => commands ??= new();

    public bool Async { get; set; }

    public bool Block { get; set; }

    public override void Write(StringBuilder sb, int ident = 0)
    {
        if (Async)
            sb.Append("async ");

        Parameters.Write(sb, ident);

        sb.Append(" => ");

        if (commands is null)
        {
            sb.Append("{ }");
            return;
        }

        if (Block)
        {
            sb.AppendLine();
            sb.Ident(ident).AppendLine("{");
            commands.Write(sb, ident + 1);
            sb.Ident(ident).AppendLine("}");
        }
        else
        {
            commands.Write(sb, ident);
        }
    }
}