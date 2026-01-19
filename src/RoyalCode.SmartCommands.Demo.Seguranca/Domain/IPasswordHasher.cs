namespace RoyalCode.SmartCommands.Demo.Seguranca.Domain;

public interface IPasswordHasher
{
    string Hash(string plainTextPassword);
    bool Verify(string plainTextPassword, string passwordHash);
}
