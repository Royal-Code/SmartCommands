using RoyalCode.Entities;

namespace Coreum.NewCommands.Tests.Scenarios.Gs;

public class Some : IEntity<int>
{
    public int Id { get; set; }
    public int Value { get; set; }
}