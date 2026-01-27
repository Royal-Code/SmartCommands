using RoyalCode.SmartCommands.Demo.Seguranca.Domain.ValueObjects;

namespace RoyalCode.SmartCommands.Demo.Seguranca.Domain;

public interface IPasswordHasher
{
    Senha Hash(string plainTextPassword);

    bool Verify(string plainTextPassword, Senha senhaAtual);
}
