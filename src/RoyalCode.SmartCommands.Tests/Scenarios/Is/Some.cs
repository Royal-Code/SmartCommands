using RoyalCode.Entities;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Is;

public class Some : IEntity<int>
{
    public int Id { get; set; }
    public int Value { get; set; }
    public string Name { get; set; }
    public bool Active { get; set; }
}