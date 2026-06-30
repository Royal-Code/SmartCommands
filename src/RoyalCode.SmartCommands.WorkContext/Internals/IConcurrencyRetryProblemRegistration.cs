using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.WorkContext.Internals;

internal interface IConcurrencyRetryProblemRegistration
{
    Type CommandType { get; }

    string Operation { get; }

    Problem Create(IServiceProvider services, object command, ConcurrencyRetryProblemContext context);
}
