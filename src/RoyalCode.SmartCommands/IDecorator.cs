﻿namespace Coreum.NewCommands;

public interface IDecorator<TModel, TResult>
{
    Task<TResult> HandleAsync(TModel command, Func<Task<TResult>> next, CancellationToken ct);
}