using RoyalCode.Entities;

namespace RoyalCode.SmartCommands.Demo.Domain;

/// <summary>
/// Ente proprio da demo (nao reutiliza o Loja de Tests.Models).
/// </summary>
public class Loja : Entity<int>
{
    public Loja(string nome, string endereco)
    {
        Nome = nome;
        Endereco = endereco;
    }

#nullable disable
    /// <summary>
    /// Construtor de materializacao do Entity Framework.
    /// </summary>
    protected Loja() { }
#nullable enable

    public string Nome { get; private set; }

    public string Endereco { get; private set; }

    public override string ToString() => $"{Nome} - {Endereco}";
}
