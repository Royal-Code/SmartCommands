using RoyalCode.Entities;

namespace Coreum.NewCommands.Tests.Scenarios.Ds;

public class Some : IEntity<int>
{
    public int Id { get; set; }
    public string? Name { get; set; }
}