# Adapter Entity Framework (`RoyalCode.SmartCommands.EntityFramework`)

Este documento descreve o contrato de confiabilidade do adapter EF (DF14): o que o
`DbContextAccessor<TContext>` devolve como `Result`, o que ele relança como exceção e como
o `RepositoryAdapter<TEntity, TContext>` expõe o contexto tipado a subclasses.

## 1. Componentes

- `DbContextAccessor<TContext>`: implementa `IUnitOfWorkAccessor<TContext>` e
  `IRepositoriesAccessor<TContext>` sobre um `DbContext`. É o accessor consumido pelos handlers
  gerados quando o comando usa `WithUnitOfWork<TContext>` com um `DbContext` concreto.
- `DbContextAdapterOptions`: `BeginTransactions` (padrão `false`) determina se `BeginAsync`
  inicia uma transação explícita e se `CompleteAsync` commita/faz rollback dela. Um comando pode
  exigir transação individualmente com `[WithTransaction]` (DF21): o handler gerado chama
  `BeginAsync(requireTransaction: true, ct)` e a transação é criada mesmo com a opção desligada.
- `RepositoryAdapter<TEntity, TContext>`: base para repositórios com projeção; expõe
  `protected TContext Context` para que subclasses implementem
  `FindEntityAsync<TDto, TId>` com o contexto tipado, sem guardar uma segunda referência.
- `AddUnitOfWorkAccessor<TContext>()` / `AddUnitOfWorkAccessor<TContextBase, TContextImpl>()`:
  registram o accessor no DI (scoped).

## 2. Contrato de exceções do `CompleteAsync` (DF14)

`CompleteAsync` salva as alterações e, quando a transação foi iniciada pelo adapter
(`BeginTransactions = true`), commita. O `Result` retornado representa **somente sucesso ou
problemas explicitamente conhecidos**; todo o resto atravessa a borda como exceção.

| Situação | Comportamento |
|---|---|
| Save + commit bem-sucedidos | `Result.Ok()`; a transação é commitada **uma única vez** e descartada pelo adapter |
| `DbUpdateConcurrencyException` (conflito otimista) | **Problema conhecido**: rollback (se o adapter iniciou a transação) e `Result` com problema `InvalidState` (409) de detalhe genérico — a mensagem do provider/EF não vaza ao chamador |
| `OperationCanceledException` | Rollback com **token próprio** (não cancelado) e **relançamento**: cancelamento permanece cancelamento, nunca vira `Result`, problema ou 500 |
| Qualquer outra exceção (`DbUpdateException`, exceções de provider, mapeamento etc.) | Rollback com token próprio e **relançamento da exceção original** (mesma instância, stack preservado) |
| Falha primária **e** falha na limpeza da transação | `AggregateException` com as duas: a primeira inner é a falha do save/commit, a segunda é a falha do rollback ou do descarte |

Regras adicionais:

- O adapter rastreia a **instância** de transação criada por `BeginAsync` e só commita/reverte
  essa instância enquanto ela for a `CurrentTransaction` do contexto. Transações abertas pelo
  usuário diretamente no contexto pertencem a ele e nunca são commitadas/revertidas pelo
  adapter, mesmo com `BeginTransactions = true`.
- O rollback usa `CancellationToken.None`: o token do handler pode já estar cancelado e a
  limpeza não pode ser abortada antes de tentar.
- Depois de commit ou rollback bem-sucedido, o adapter descarta explicitamente a transação que
  criou. O descarte também faz parte da limpeza: se falhar enquanto outra exceção já está em
  propagação, ambas são preservadas em `AggregateException`.
- A regra da falha dupla **sobrepõe** as linhas de concorrência e cancelamento: se o rollback
  falhar, o chamador recebe `AggregateException` (com a `DbUpdateConcurrencyException` ou a
  `OperationCanceledException` como primeira inner) em vez do problema conhecido/cancelamento.
- Se `CommitAsync` falhar **depois** do commit físico ter sido efetivado no banco (ex.: queda de
  conexão pós-commit), o estado é inerentemente ambíguo: o adapter ainda tentará o rollback e a
  falha resultante pode chegar como `AggregateException` mesmo com os dados persistidos. Trate
  falha de commit como estado indeterminado, não como garantia de rollback.
- O detalhe do conflito otimista é a constante
  `DbContextAccessor<TContext>.ConcurrencyConflictDetail`, alinhada ao texto usado pelo retry
  de concorrência do pacote WorkContext.

## 3. Uso fora e dentro de HTTP

O adapter não presume HTTP e não converte exceções em respostas:

- **Fora de HTTP** (worker, console, testes): o chamador do handler recebe `Result` para
  sucesso/problemas conhecidos e exceções para falhas inesperadas ou cancelamento — pode
  decidir a própria política (retry, log, abortar).
- **Dentro de HTTP**: problemas conhecidos fluem pelo `Result` do handler (tipicamente 409
  para conflito otimista); exceções inesperadas atravessam o handler gerado e devem ser
  tratadas pela borda do app (`UseExceptionHandler` + `AddProblemDetails`, `IExceptionHandler`
  ou middleware próprio). Não há tratamento duplicado dentro do adapter, e a mensagem da
  exceção de persistência não deve ser incluída na resposta.

## 4. Sem retry de concorrência neste adapter

`WithRetryOnConcurrency` é suportado apenas com `WithWorkContext` (o laço de retry captura a
`ConcurrencyException` da abstração de unit of work do WorkContext). No adapter EF puro, o
conflito otimista é terminal e retorna o problema conhecido descrito acima; cabe ao chamador
reexecutar o comando, se fizer sentido.

## 5. Testes

`RoyalCode.SmartCommands.EntityFramework.Tests` cobre o contrato com SQLite in-memory real
(sem mocks de `DbContext`) e interceptors de falha para save/commit/rollback e observação da
transação física descartada:

- `DbContextAccessorTests`: sucesso/commit único, falha inesperada relançada após rollback,
  falha no commit, falha no rollback (`AggregateException` com as duas), cancelamento
  preservado com cleanup em token próprio e conflito otimista real (dois contextos).
- `RepositoryAdapterTests`: find e projeção usando o `Context` tipado herdado.
- `GeneratedHandlerTests`: o generator roda como analyzer sobre o projeto de teste; o handler
  gerado real é consumido direto do DI (uso fora de HTTP).
- `HttpBoundaryTests`: Minimal API **gerada** por `MapApiHandlers`, com TestServer; 201 para sucesso, 409 pelo `Result` para
  conflito e 500 `ProblemDetails` pelo `UseExceptionHandler` para exceção inesperada, sem
  vazar o detalhe interno.
