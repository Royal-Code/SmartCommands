# Diagnósticos do SmartCommands (RCCMD)

Este documento descreve os diagnósticos emitidos pelo analyzer/generator do SmartCommands
(`RoyalCode.SmartCommands.Generators`). Cada regra tem um identificador estável `RCCMDxxx`, uma
severidade e uma orientação de correção. Entradas inválidas produzem o diagnóstico correspondente e
**não geram fonte relacionada**, em vez de causar erro de compilação do generator.

| Regra | Severidade | Resumo |
|---|---|---|
| [RCCMD000](#rccmd000) | Error | Uso inválido de `[Command]` (mensagem detalha o motivo) |
| [RCCMD001](#rccmd001) | Error | O método `[Command]` não pode retornar `Task`/`void` quando exige valor |
| [RCCMD002](#rccmd002) | Error | `WithValidateModel` exige um método `HasProblems` |
| [RCCMD003](#rccmd003) | Error | `HasProblems` deve retornar `bool` |
| [RCCMD004](#rccmd004) | Error | `HasProblems` deve ter um `out Problems` |
| [RCCMD005](#rccmd005) | Error | Parâmetro de entidade sem propriedade de Id correspondente |
| [RCCMD006](#rccmd006) | Error | `ProduceNewEntity` exige `WithUnitOfWork` |
| [RCCMD007](#rccmd007) | Error | `ProduceNewEntity` com `Result` exige `Result<T>` |
| [RCCMD008](#rccmd008) | Error | `CancellationToken` só em método assíncrono |
| [RCCMD009](#rccmd009) | Error | `EditEntity` exige `WithUnitOfWork` |
| [RCCMD010](#rccmd010) | Error | `EditEntity` exige o primeiro parâmetro do tipo da entidade |
| [RCCMD011](#rccmd011) | Error | `WithParameter` não pode marcar entidade/coleção/contexto |
| [RCCMD012](#rccmd012) | Warning | Apenas um `[MapApiHandlers]` por projeto |
| [RCCMD013](#rccmd013) | Error | Uso inválido de `[MapApiHandlers]` |
| [RCCMD014](#rccmd014) | Error | Propriedade `Id` não encontrada no objeto retornado |
| [RCCMD015](#rccmd015) | Error | Tipo retornado pelo comando não encontrado |
| [RCCMD016](#rccmd016) | Error | Propriedade não encontrada no objeto retornado |
| [RCCMD017](#rccmd017) | Error | Tipo de Find inválido |
| [RCCMD018](#rccmd018) | Error | `WithDbContext` não pode ser usado com `WithUnitOfWork` |
| [RCCMD019](#rccmd019) | Error | `WithWorkContext` não pode ser usado com `WithUnitOfWork` |
| [RCCMD020](#rccmd020) | Error | `WithWorkContext` não pode ser usado com `WithDbContext` |
| [RCCMD021](#rccmd021) | Error | Uso inválido de `[MapFind]` |
| [RCCMD022](#rccmd022) | Error | Uso inválido de `[MapSearch]` |
| [RCCMD023](#rccmd023) | Error | Uso inválido de `[WithFilter]` |
| [RCCMD024](#rccmd024) | Error | `WithRetryOnConcurrency` exige `WithWorkContext` |
| [RCCMD025](#rccmd025) | Error | `WithRetryOnConcurrency` exige `maxAttempts` maior que zero |
| [RCCMD026](#rccmd026) | Error | Uma classe de comando só pode ter um método `[Command]` |
| [RCCMD027](#rccmd027) | Error | Uma classe de comando só pode ter um atributo `Map*` |
| [RCCMD028](#rccmd028) | Error | Atributo `Map*` exige route pattern e endpoint name |
| [RCCMD029](#rccmd029) | Error | Parâmetro colide com identificador reservado do código gerado |
| [RCCMD030](#rccmd030) | Error | Nome de endpoint duplicado entre endpoints mapeados |

---

## RCCMD000
**Uso inválido de `CommandAttribute`.** A mensagem detalha o motivo: método sem classe, classe/método genérico,
classe aninhada ou file-local (`file class`), método `static`/`abstract`/inacessível, `ProduceNewEntity` + `EditEntity`
juntos, entre outros.
**Correção:** declare o comando como um método de instância `public`/`internal`, em uma classe de nível superior
não genérica e não file-local.

## RCCMD001
O método marcado com `[Command]` deve retornar um valor de domínio (ou `Result`/`Result<T>`), não `Task`/`void`,
quando o cenário exige um valor. **Correção:** ajuste o tipo de retorno.

## RCCMD002
`WithValidateModel` exige um método `HasProblems` na classe do comando. **Correção:** adicione
`bool HasProblems(out Problems? problems)` ou remova `WithValidateModel`.

## RCCMD003
O método `HasProblems` deve retornar `bool`. **Correção:** ajuste a assinatura.

## RCCMD004
O método `HasProblems` deve ter um parâmetro `out Problems`. **Correção:** ajuste a assinatura.

## RCCMD005
Um parâmetro de tipo entidade requer uma propriedade de Id correspondente na classe do comando (ex.: `ClienteId`
para o parâmetro `cliente`). **Correção:** adicione a propriedade de Id.

## RCCMD006
`ProduceNewEntity` exige `WithUnitOfWork`. **Correção:** adicione `WithUnitOfWork<TContext>`.

## RCCMD007
Quando o comando com `ProduceNewEntity` retorna `Result`, deve ser `Result<T>` (com valor). **Correção:** retorne
`Result<TEntidade>`.

## RCCMD008
`CancellationToken` só pode ser usado em métodos assíncronos. **Correção:** torne o método assíncrono ou remova o token.

## RCCMD009
`EditEntity` exige `WithUnitOfWork`. **Correção:** adicione `WithUnitOfWork<TContext>`.

## RCCMD010
Com `EditEntity`, o primeiro parâmetro deve ser do mesmo tipo da entidade informada no atributo. **Correção:** ajuste
o primeiro parâmetro.

## RCCMD011
`WithParameter` não pode ser aplicado a um parâmetro que é entidade, coleção de entidades ou o contexto. **Correção:**
remova `WithParameter` desse parâmetro.

## RCCMD012
Deve haver apenas um `[MapApiHandlers]` por projeto. **Correção:** mantenha um único host de mapeamento.

## RCCMD013
Uso inválido de `[MapApiHandlers]` (a mensagem detalha). **Correção:** aplique em uma `static partial class`.

## RCCMD014
`MapIdResultValue` exige que o objeto retornado tenha uma propriedade `Id`. **Correção:** exponha `Id` ou remova o atributo.

## RCCMD015
O tipo retornado pelo comando não pôde ser resolvido para `MapResponseValues`. **Correção:** verifique o tipo de retorno.

## RCCMD016
Uma propriedade informada em `MapResponseValues` não existe no objeto retornado. **Correção:** corrija o nome da propriedade.

## RCCMD017
Uso inválido de `MapFind` (a mensagem detalha). **Correção:** verifique tipos/parâmetros do Find.

## RCCMD018
`WithDbContext` não pode ser combinado com `WithUnitOfWork`. **Correção:** use apenas um.

## RCCMD019
`WithWorkContext` não pode ser combinado com `WithUnitOfWork`. **Correção:** use apenas um.

## RCCMD020
`WithWorkContext` não pode ser combinado com `WithDbContext`. **Correção:** use apenas um.

## RCCMD021
Uso inválido do atributo `MapFind` (a mensagem detalha). **Correção:** ajuste o uso conforme a documentação.

## RCCMD022
Uso inválido do atributo `MapSearch` (a mensagem detalha). **Correção:** ajuste o uso conforme a documentação.

## RCCMD023
Uso inválido do atributo `WithFilter` (a mensagem detalha). **Correção:** ajuste o uso conforme a documentação.

## RCCMD024
`WithRetryOnConcurrency` só é suportado junto com `WithWorkContext`. **Correção:** adicione `WithWorkContext`.

## RCCMD025
O número máximo de tentativas de `WithRetryOnConcurrency` deve ser maior que zero. **Correção:** informe `maxAttempts > 0`.

## RCCMD026
Uma classe de comando deve declarar apenas um método `[Command]`. **Correção:** mantenha um único método de comando por classe.

## RCCMD027
Uma classe de comando deve declarar apenas um atributo `Map*` (`MapPost`, `MapPut`, `MapPatch`, `MapDelete`, `MapGet`).
**Correção:** mantenha um único atributo de mapeamento.

## RCCMD028
Um atributo `Map*` exige route pattern e endpoint name. **Correção:** informe os dois argumentos, ex.: `[MapPost("/", "create")]`.

## RCCMD029
Um parâmetro do comando usa um nome reservado pelo código gerado no mesmo escopo. No escopo do handler:
`command`, `ct`, `accessor`, `commandResult`, `decorators`, `decoratorsMediator`, `retryOptions`,
`retryProblemFactory`. No escopo do endpoint Minimal API (quando o comando é mapeado, para parâmetros
`[WithParameter]`): `handler`, `result` e, com `EditEntity`, `{parâmetroDaEntidade}Id`.
**Correção:** renomeie o parâmetro.

## RCCMD030
O mesmo endpoint name é usado por mais de um endpoint mapeado (`WithName` exige nomes globais únicos). O erro é
reportado em cada ocorrência, na localização do argumento do atributo, e os endpoints conflitantes não são
emitidos no host. **Correção:** use nomes de endpoint únicos.
