using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.WorkContext.Internals;

internal sealed class ConcurrencyRetryProblemRegistration<TCommand> : IConcurrencyRetryProblemRegistration
{
    private readonly Func<IServiceProvider, TCommand, ConcurrencyRetryProblemContext, Problem> factory;

    public ConcurrencyRetryProblemRegistration(
        string operation,
        Func<IServiceProvider, TCommand, ConcurrencyRetryProblemContext, Problem> factory)
    {
        Operation = operation;
        this.factory = factory;
    }

    public Type CommandType => typeof(TCommand);

    public string Operation { get; }

    public Problem Create(IServiceProvider services, object command, ConcurrencyRetryProblemContext context)
    {
        return factory(services, (TCommand)command, context);
    }
}
