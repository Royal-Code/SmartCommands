using RoyalCode.Entities;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Gs;

public class Some : IEntity<int>
{
    public int Id { get; set; }
    public int Value { get; set; }
}