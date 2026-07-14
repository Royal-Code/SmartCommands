using static RoyalCode.SmartCommands.Generators.Generators.CommandHandlerInformation;

namespace RoyalCode.SmartCommands.Generators.Generators;

internal class AddServiceDescriptor
{
    public AddServiceDescriptor(ServiceTypeDescriptor serviceTypeDescriptor, ContextAccessorModes contextAccessorMode)
    {
        ServiceTypeDescriptor = serviceTypeDescriptor;
        ContextAccessorMode = contextAccessorMode;
    }

    public ServiceTypeDescriptor ServiceTypeDescriptor { get; }

    public ContextAccessorModes ContextAccessorMode { get; }
}