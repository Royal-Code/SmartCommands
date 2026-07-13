using System.Text;

namespace RoyalCode.SmartCommands.Generators.Commands;

public class CompleteUnitOfWorkCommand : GeneratorNode
{
    private readonly GeneratorNode methodInvoke;
    private readonly bool invokeIsAsync;
    private readonly TypeDescriptor commandReturnType;
    private readonly string accessorVarName;
    private readonly string commandResultVarName;
    private readonly bool produceNewEntity;
    private readonly bool hasDecorators;

    public CompleteUnitOfWorkCommand(
        GeneratorNode methodInvoke,
        bool invokeIsAsync,
        TypeDescriptor commandReturnType,
        string accessorVarName,
        string commandResultVarName,
        bool produceNewEntity = false,
        bool hasDecorators = false)
    {
        this.methodInvoke = methodInvoke;
        this.invokeIsAsync = invokeIsAsync;
        this.commandReturnType = commandReturnType;
        this.accessorVarName = accessorVarName;
        this.commandResultVarName = commandResultVarName;
        this.produceNewEntity = produceNewEntity;
        this.hasDecorators = hasDecorators;
    }

    public override void Write(StringBuilder sb, int indent = 0)
    {
        // se não tem decorators e se for void, executa o método e completa o UnitOfWork
        if (!hasDecorators && (commandReturnType.IsVoid || commandReturnType.IsVoidTask))
        {
            new Command(methodInvoke) 
            { 
                Await = commandReturnType.IsVoidTask,
                NewLine = true,
            }.Write(sb, indent);
            
            var invokeCompleteAsync = new MethodInvokeGenerator($"this.{accessorVarName}", "CompleteAsync", "ct")
            {
                Await = true
            };

            new ReturnCommand(invokeCompleteAsync).Write(sb, indent);

            return;
        }

        var commandReturnResult = commandReturnType.Name.StartsWith("Result") ||
                                  commandReturnType.Name.StartsWith("Task<Result");

        AssignValueCommand? assignValueCommand = null;
        MethodInvokeGenerator? invokeAddEntityAsync = null;
        GeneratorNode final;

        // se não retorna result, então deve ser criada uma variável para armazenar o resultado
        // e passar por MapAsync no retorno do CompleteAsync
        if (!commandReturnResult)
        {
            assignValueCommand = new AssignValueCommand($"var {commandResultVarName}", methodInvoke);

            if (produceNewEntity)
            {
                invokeAddEntityAsync = new MethodInvokeGenerator($"this.{accessorVarName}", "AddEntityAsync");
                invokeAddEntityAsync.AddArgument(commandResultVarName);
                invokeAddEntityAsync.AddArgument("ct");
                invokeAddEntityAsync.Await = true;
            }

            var invokeCompleteAsync = new MethodInvokeGenerator($"this.{accessorVarName}", "CompleteAsync", "ct")
            {
                Await = true
            };

            final = new MethodInvokeGenerator(
                invokeCompleteAsync, "MapAsync", commandResultVarName);
        }
        else
        {
            ValueNode identifier = methodInvoke;
            if (produceNewEntity)
            {
                var addEntityAsync = new MethodInvokeGenerator(methodInvoke, "ContinueAsync");
                addEntityAsync.AddArgument($"this.{accessorVarName}");
                addEntityAsync.AddArgument($"static async (e, a, ct) => await a.AddEntityAsync(e, ct)");
                addEntityAsync.AddArgument("ct");
                addEntityAsync.LineIdent = true;
                identifier = addEntityAsync;
            }

            // verifica parâmetro da expressão lambda,
            // se retorna um Result<T> deve ser (_, a, ct)
            // senão deve ser (a, ct)
            var lambdaParam = commandReturnType.Name.StartsWith("Task<Result<") ||
                              commandReturnType.Name.StartsWith("Result<")
                ? "_, a, ct"
                : "a, ct";

            var invokeContinueAsync = new MethodInvokeGenerator(identifier, "ContinueAsync");
            invokeContinueAsync.AddArgument($"this.{accessorVarName}");
            invokeContinueAsync.AddArgument($"static async ({lambdaParam}) => await a.CompleteAsync(ct)");
            invokeContinueAsync.AddArgument("ct");
            invokeContinueAsync.LineIdent = produceNewEntity;

            if (!invokeIsAsync)
                invokeContinueAsync.Await = true;

            final = invokeContinueAsync;
        }

        if (assignValueCommand is not null)
        {
            assignValueCommand.Write(sb, indent);
            sb.AppendLine();

            if (invokeAddEntityAsync is not null)
            {
                // invoke não é comando, então não gera indent, nem new line, nem ';'
                // então é necessário escrever aqui
                sb.Indent(indent);
                invokeAddEntityAsync.Write(sb, indent);
                sb.AppendLine(";").AppendLine();
            }
        }

        new ReturnCommand(final).Write(sb, indent);
    }
}