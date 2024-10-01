using RoyalCode.Entities;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Fs;

public class Some : IEntity<int>
{
    public int Id { get; set; }
    public string? Name { get; set; }
}