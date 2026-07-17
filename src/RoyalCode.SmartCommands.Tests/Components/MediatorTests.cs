namespace RoyalCode.SmartCommands.Tests.Components;

/// <summary>
/// Testes do runtime do <see cref="Mediator{TModel, TResult}"/> após a troca do enumerador mutável
/// pelo pipeline composto por delegates (Fase 8): ordem de registro, <c>next</c> determinístico
/// quando invocado mais de uma vez e ausência de estado compartilhado entre execuções.
/// </summary>
public class MediatorTests
{
    private sealed class RecordingDecorator : IDecorator<string, int>
    {
        private readonly string name;
        private readonly List<string> log;

        public RecordingDecorator(string name, List<string> log)
        {
            this.name = name;
            this.log = log;
        }

        public async Task<int> HandleAsync(string command, Func<Task<int>> next, CancellationToken ct)
        {
            log.Add($"{name}:antes");
            var result = await next();
            log.Add($"{name}:depois");
            return result;
        }
    }

    private sealed class CallsNextTwiceDecorator : IDecorator<string, int>
    {
        public async Task<int> HandleAsync(string command, Func<Task<int>> next, CancellationToken ct)
        {
            var first = await next();
            var second = await next();
            return first + second;
        }
    }

    [Fact]
    public async Task Decorators_executam_na_ordem_registrada_em_torno_do_handler_final()
    {
        var log = new List<string>();
        var decorators = new IDecorator<string, int>[]
        {
            new RecordingDecorator("d1", log),
            new RecordingDecorator("d2", log),
        };

        var mediator = new Mediator<string, int>(
            decorators,
            () =>
            {
                log.Add("final");
                return Task.FromResult(42);
            },
            "cmd",
            CancellationToken.None);

        var result = await mediator.NextAsync();

        Assert.Equal(42, result);
        Assert.Equal(["d1:antes", "d2:antes", "final", "d2:depois", "d1:depois"], log);
    }

    [Fact]
    public async Task Next_invocado_duas_vezes_reexecuta_o_restante_do_pipeline_de_forma_deterministica()
    {
        var log = new List<string>();
        var finalCount = 0;
        var decorators = new IDecorator<string, int>[]
        {
            new CallsNextTwiceDecorator(),
            new RecordingDecorator("interno", log),
        };

        var mediator = new Mediator<string, int>(
            decorators,
            () =>
            {
                finalCount++;
                return Task.FromResult(10);
            },
            "cmd",
            CancellationToken.None);

        var result = await mediator.NextAsync();

        // cada chamada de next executa o RESTANTE do pipeline (decorator interno + final),
        // sem posição compartilhada: o decorator interno roda nas duas passagens
        Assert.Equal(20, result);
        Assert.Equal(2, finalCount);
        Assert.Equal(["interno:antes", "interno:depois", "interno:antes", "interno:depois"], log);
    }

    [Fact]
    public async Task NextAsync_repetido_no_mediator_reexecuta_o_pipeline_completo()
    {
        var log = new List<string>();
        var mediator = new Mediator<string, int>(
            [new RecordingDecorator("d1", log)],
            () => Task.FromResult(7),
            "cmd",
            CancellationToken.None);

        var first = await mediator.NextAsync();
        var second = await mediator.NextAsync();

        // sem enumerador retido: a segunda execução não "pula" os decorators
        Assert.Equal(7, first);
        Assert.Equal(7, second);
        Assert.Equal(["d1:antes", "d1:depois", "d1:antes", "d1:depois"], log);
    }

    [Fact]
    public async Task Sem_decorators_o_handler_final_e_invocado_diretamente()
    {
        var mediator = new Mediator<string, int>(
            [],
            () => Task.FromResult(1),
            "cmd",
            CancellationToken.None);

        Assert.Equal(1, await mediator.NextAsync());
    }

    [Fact]
    public async Task Instancias_separadas_nao_compartilham_estado()
    {
        var log = new List<string>();
        var decorators = new IDecorator<string, int>[] { new RecordingDecorator("d1", log) };

        // padrão do handler gerado: um mediator novo por HandleAsync
        var m1 = new Mediator<string, int>(decorators, () => Task.FromResult(1), "cmd1", CancellationToken.None);
        var m2 = new Mediator<string, int>(decorators, () => Task.FromResult(2), "cmd2", CancellationToken.None);

        Assert.Equal(1, await m1.NextAsync());
        Assert.Equal(2, await m2.NextAsync());
        Assert.Equal(["d1:antes", "d1:depois", "d1:antes", "d1:depois"], log);
    }
}
