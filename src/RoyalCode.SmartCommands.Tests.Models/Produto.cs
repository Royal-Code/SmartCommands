using RoyalCode.Entities;

namespace RoyalCode.SmartCommands.Tests.Models;

public class Produto : Entity<Guid>
{
    public Produto(string nome)
    {
        Nome = nome;
        Ativo = true;
    }

    public string Nome { get; set; }

    public bool Ativo { get; set; }

}
