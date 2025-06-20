
namespace RoyalCode.SmartCommands.Demo.Commands.Lojas;

public partial class CriarLojaResponse(int id, string nome)
{
    public int Id { get; } = id;

    public string Nome { get; } = nome;
}
