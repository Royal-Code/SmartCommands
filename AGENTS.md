# Instruções para agentes

Estas instruções valem para todo o repositório SmartCommands. Instruções explícitas do usuário e arquivos `AGENTS.md` mais próximos do código em edição têm precedência.

## Comunicação e escopo

- Comunique-se em português do Brasil, preservando o idioma e o estilo já usados em código, nomes de API, mensagens e documentos.
- Em pedidos de análise, diagnóstico ou revisão, não altere arquivos — implemente somente quando o usuário também pedir a implementação.
- Não faça commit, push, publicação de pacote, alteração de versão ou de CI/CD sem pedido explícito.
- Quando o pedido depender de uma decisão sobre contrato público, breaking change, arquitetura, persistência, segurança ou publicação, apresente a decisão antes de implementar.

## Projetos e finalidade

A solução está em `src/SmartCommands.sln`. Os projetos se dividem em três grupos:

| Grupo | Projetos | Finalidade |
|---|---|---|
| Bibliotecas publicáveis | `RoyalCode.SmartCommands`, `RoyalCode.SmartCommands.Generators`, `RoyalCode.SmartCommands.EntityFramework`, `RoyalCode.SmartCommands.WorkContext` | Pacotes NuGet que formam a entrega do repositório. |
| Testes e suporte | `RoyalCode.SmartCommands.Tests`, `RoyalCode.SmartCommands.Tests.Models`, `RoyalCode.SmartCommands.Demo.Tests` | Testes do runtime/generator, modelos e fixtures compartilhados, e testes de integração HTTP dos exemplos. `Tests.Models` não é biblioteca de produção. |
| Exemplos e demonstrações | `RoyalCode.SmartCommands.Demo` e todos os `RoyalCode.SmartCommands.Demo.*` restantes | Host demonstrativo e módulos verticais que exercitam formas de uso da biblioteca. Não são aplicações reais de produção nem modelos completos de requisitos não funcionais, segurança, observabilidade ou operação. |

Alvos: bibliotecas runtime `net8.0`/`net9.0`/`net10.0`; testes e Demo `net10.0`; generator/analyzer `netstandard2.0`.

## Arquitetura das bibliotecas publicáveis

```text
RoyalCode.SmartCommands.Generators --(build/analyzer)--> código dos handlers, DI e endpoints

RoyalCode.SmartCommands.EntityFramework ---┐
RoyalCode.SmartCommands.WorkContext -------┴--> RoyalCode.SmartCommands
                                                 ├── RoyalCode.SmartProblems
                                                 └── RoyalCode.SmartValidations
```

- `RoyalCode.SmartCommands` é o núcleo runtime: atributos públicos, contratos de accessors/repositórios, decorators e mediator. Não contém implementação concreta de EF Core ou WorkContext.
- `RoyalCode.SmartCommands.Generators` é a camada de compilação: incremental generator/analyzer, diagnósticos, leitura dos atributos e emissão de handlers, registros de DI e mapeamentos HTTP. `Generators/` contém transformação/modelos/emissão; `Commands/` contém blocos usados para construir o fluxo gerado. O pacote é distribuído em `analyzers/dotnet/cs` e usa `RoyalCode.Extensions.SourceGenerator` como base.
- `RoyalCode.SmartCommands.EntityFramework` adapta os contratos do núcleo diretamente ao EF Core. Organiza-se em `Adapters/`, `Extensions/` para DI e `Options/`.
- `RoyalCode.SmartCommands.WorkContext` adapta os mesmos contratos ao ecossistema WorkContext, incluindo repositórios, UnitOfWork e retry de concorrência. Organiza-se em `Adapters/`, `Extensions/`, `Options/`, `Internals/` e contratos/factories de problemas de concorrência.
- `pack.targets` centraliza metadados e empacotamento; `Directory.Build.props` centraliza TFMs e versões das dependências. Mudanças de API pública devem considerar os quatro pacotes e o comportamento do código gerado.

## Documentação e referências de leitura

Antes de tarefas que envolvam arquitetura, comportamento ou uso das bibliotecas, leia o que for pertinente:

- [`src/README.md`](src/README.md) — visão geral e uso público.
- [`src/.docs/commands.md`](src/.docs/commands.md) — documentação completa de SmartCommands: atributos, handlers, pipeline, adapters e Minimal APIs.
- [`src/.docs/feature-slice-architecture.md`](src/.docs/feature-slice-architecture.md) — padrão arquitetural atual para novos módulos: DDD modular, Features verticais e lentes Explícita/Gritante.
- [`src/.docs/archtecture.md`](src/.docs/archtecture.md) — arquitetura anterior baseada em `Contracts` + `Application`, útil para compreender soluções existentes e migrações. Em conflito para código novo, `feature-slice-architecture.md` prevalece.

Em `src/.docs/references/`, os arquivos `*.ai-rules.md` são regras operacionais concisas; os arquivos `*.md` correspondentes são os guias conceituais completos, com contexto, exemplos e referência de API:

- Domínio — [regras para IA](src/.docs/references/domain.ai-rules.md) | [guia completo](src/.docs/references/domain.md): Entities, Aggregates e DomainEvents.
- Persistência — [regras para IA](src/.docs/references/workcontext.ai-rules.md) | [guia completo](src/.docs/references/workcontext.md): WorkContext, UnitOfWork e Repositories.
- Validações — [regras para IA](src/.docs/references/validations.ai-rules.md) | [guia completo](src/.docs/references/validations.md): SmartValidations, `RuleSet` e `IValidable`.
- Problemas e resultados — [regras para IA](src/.docs/references/problems.ai-rules.md) | [guia completo](src/.docs/references/problems.md): `Problem`, `Problems`, `Result` e `FindResult`.
- Projeções — [regras para IA](src/.docs/references/selector.ai-rules.md) | [guia completo](src/.docs/references/selector.md): SmartSelector e seu source generator.
- Buscas — [regras para IA](src/.docs/references/smartsearch.ai-rules.md) | [guia completo](src/.docs/references/smartsearch.md): SmartSearch, critérios, filtros, ordenação e paginação.

Os documentos em `src/.docs/references/` são a documentação das bibliotecas consumidas: use-os para orientar o código, mas não os edite sem pedido explícito.

## Ecossistema RoyalCode e fontes locais

As versões consumidas por NuGet são definidas em `src/Directory.Build.props`. No layout local padrão, os repositórios das dependências ficam como irmãos de `SmartCommands` sob a pasta `RoyalCode/`. Os caminhos abaixo servem para consultar a implementação ou realizar trabalho coordenado entre repositórios; os projetos desta solução continuam referenciando os pacotes NuGet, salvo mudança deliberada para investigação local.

- **SmartProblems** — resultados e problemas padronizados: [core](../SmartProblems/src/RoyalCode.SmartProblems/RoyalCode.SmartProblems.csproj), [ApiResults](../SmartProblems/src/RoyalCode.SmartProblems.ApiResults/RoyalCode.SmartProblems.ApiResults.csproj), [HTTP](../SmartProblems/src/RoyalCode.SmartProblems.Http/RoyalCode.SmartProblems.Http.csproj), [ProblemDetails](../SmartProblems/src/RoyalCode.SmartProblems.ProblemDetails/RoyalCode.SmartProblems.ProblemDetails.csproj) e [EntityFramework](../SmartProblems/src/RoyalCode.SmartProblems.EntityFramework/RoyalCode.SmartProblems.EntityFramework.csproj).
- **SmartValidations** — regras e composição de validações: [RoyalCode.SmartValidations](../SmartValidations/RoyalCode.SmartValidations/RoyalCode.SmartValidations.csproj).
- **SmartSelector** — projeções tipadas e geração de mapeamentos: [runtime](../SmartSelector/src/RoyalCode.SmartSelector/RoyalCode.SmartSelector.csproj) e [generator](../SmartSelector/src/RoyalCode.SmartSelector.Generators/RoyalCode.SmartSelector.Generators.csproj).
- **SmartSearch** — filtros, critérios e execução de buscas; o repositório local chama-se `Searches`: [Abstractions](../Searches/src/RoyalCode.SmartSearch.Abstractions/RoyalCode.SmartSearch.Abstractions.csproj), [Core](../Searches/src/RoyalCode.SmartSearch.Core/RoyalCode.SmartSearch.Core.csproj), [Linq](../Searches/src/RoyalCode.SmartSearch.Linq/RoyalCode.SmartSearch.Linq.csproj), [EntityFramework](../Searches/src/RoyalCode.SmartSearch.EntityFramework/RoyalCode.SmartSearch.EntityFramework.csproj) e [AspNetCore](../Searches/src/RoyalCode.SmartSearch.AspNetCore/RoyalCode.SmartSearch.AspNetCore.csproj).
- **Bibliotecas de domínio** — base DDD no repositório `EnterprisePatterns`: [Entities](../EnterprisePatterns/RoyalCode.EnterprisePatterns/RoyalCode.Entities/RoyalCode.Entities.csproj), [DomainEvents](../EnterprisePatterns/RoyalCode.EnterprisePatterns/RoyalCode.DomainEvents/RoyalCode.DomainEvents.csproj) e [Aggregates](../EnterprisePatterns/RoyalCode.EnterprisePatterns/RoyalCode.Aggregates/RoyalCode.Aggregates.csproj).
- **WorkContext e persistência** — abstrações e implementações EF Core no repositório `EnterprisePatterns`: [Repositories.Abstractions](../EnterprisePatterns/RoyalCode.EnterprisePatterns/RoyalCode.Repositories.Abstractions/RoyalCode.Repositories.Abstractions.csproj), [Repositories.EntityFramework](../EnterprisePatterns/RoyalCode.EnterprisePatterns/RoyalCode.Repositories.EntityFramework/RoyalCode.Repositories.EntityFramework.csproj), [UnitOfWork.Abstractions](../EnterprisePatterns/RoyalCode.EnterprisePatterns/RoyalCode.UnitOfWork.Abstractions/RoyalCode.UnitOfWork.Abstractions.csproj), [UnitOfWork.EntityFramework](../EnterprisePatterns/RoyalCode.EnterprisePatterns/RoyalCode.UnitOfWork.EntityFramework/RoyalCode.UnitOfWork.EntityFramework.csproj), [WorkContext.Abstractions](../EnterprisePatterns/RoyalCode.EnterprisePatterns/RoyalCode.WorkContext.Abstractions/RoyalCode.WorkContext.Abstractions.csproj) e [WorkContext.EntityFramework](../EnterprisePatterns/RoyalCode.EnterprisePatterns/RoyalCode.WorkContext.EntityFramework/RoyalCode.WorkContext.EntityFramework.csproj).
- **Extensions.SourceGenerator** — infraestrutura compartilhada dos generators: [RoyalCode.Extensions.SourceGenerator](../Utils/RoyalCode.Utils/RoyalCode.Extensions.SourceGenerator/RoyalCode.Extensions.SourceGenerator.csproj).

## Convenções de implementação

- Documente APIs públicas e funcionalidade de domínio com XML documentation ao criar ou alterar seus contratos.
- Para falhas esperadas de domínio, prefira `Result`, `Result<T>`, `Problems` e `Problem` em vez de exceções; valide com SmartValidations/`Rules.Set<T>()` e `HasProblems`.
- Propague `CancellationToken` em fluxos assíncronos e evite `async void`.
- Mudanças no generator devem manter saída determinística, cabeçalho `// <auto-generated/>`, `#nullable enable` e diagnósticos para entradas inválidas em vez de falhas do generator.
- Prefira corrigir o código-fonte que gera os artefatos a editar artefatos gerados à mão; atualize snapshots/fixtures apenas com a mudança verificada.

## Fluxo de trabalho

- Mantenha os patches focados no pedido. Problemas fora do escopo podem ser relatados à parte, em vez de expandir a alteração.
- Para executar planos em `src/.ai/plans/`, leia antes as decisões, dependências, invariantes, critérios de aceite e riscos. Atualize tarefas e resultados só depois da verificação correspondente.

## Build e testes

A partir de `src/`, os comandos padrão são:

```powershell
dotnet build SmartCommands.sln -c Release
dotnet test RoyalCode.SmartCommands.Tests/RoyalCode.SmartCommands.Tests.csproj -c Release
dotnet test RoyalCode.SmartCommands.Demo.Tests/RoyalCode.SmartCommands.Demo.Tests.csproj -c Release
```

- Valide na proporção do risco: testes diretos para mudanças localizadas; suíte relevante completa para contratos públicos, generator ou mudanças transversais.
- Não aceite warnings novos silenciosamente; compare com o baseline.
- Relate exatamente quais verificações rodaram e seus resultados — não afirme que um teste passou sem tê-lo executado.

## Critério de conclusão

- O comportamento pedido está implementado (ou a análise está sustentada por evidências), com testes criados/atualizados quando o comportamento mudou.
- As verificações relevantes passaram, ou os impedimentos e falhas remanescentes foram relatados com clareza.
