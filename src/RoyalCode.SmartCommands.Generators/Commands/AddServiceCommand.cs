using System.Text;

namespace RoyalCode.SmartCommands.Generators.Commands;

public class AddServiceCommand : GeneratorNode
{
    private readonly ServiceTypeDescriptor serviceTypeDescriptor;
    private readonly string servicesVarName;

    public AddServiceCommand(ServiceTypeDescriptor serviceTypeDescriptor, string servicesVarName)
    {
        this.serviceTypeDescriptor = serviceTypeDescriptor;
        this.servicesVarName = servicesVarName;
    }

    public override void Write(StringBuilder sb, int ident = 0)
    {
        sb.Ident(ident);

        sb.Append(servicesVarName).Append(".AddTransient<")
            .Append(serviceTypeDescriptor.InterfaceType.Name).Append(", ")
            .Append(serviceTypeDescriptor.HandlerType.Name).Append(">();");

        sb.AppendLine();
    }
}
