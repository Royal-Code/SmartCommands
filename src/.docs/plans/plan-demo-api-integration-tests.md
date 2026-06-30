# Plano: Testes de integracao da API Demo

## Objetivo

Criar um projeto de testes de integracao para validar a API exposta por `RoyalCode.SmartCommands.Demo`, exercitando o codigo gerado em um host real.

O foco nao e testar snapshot de source generator. O foco e provar que endpoints, DI, handlers gerados, WorkContext, validacoes, respostas HTTP e `Problem`s funcionam juntos.

## Decisoes

- Criar o projeto `RoyalCode.SmartCommands.Demo.Tests`.
- Adicionar o projeto a `SmartCommands.sln`.
- Usar `WebApplicationFactory`/`TestServer` para subir o demo em memoria.
- Comecar pelos endpoints existentes antes de criar cenarios artificiais.
- Manter testes comportamentais: status code, payload, efeitos persistidos e `ProblemDetails`.
- Para concorrencia otimista, preferir SQLite in-memory quando o cenario exigir comportamento real de banco.

## Fase 1 - Estrutura e endpoints existentes

### Tarefas

- Criar o projeto `RoyalCode.SmartCommands.Demo.Tests`.
- Adicionar o projeto a `SmartCommands.sln`.
- Referenciar `RoyalCode.SmartCommands.Demo`.
- Adicionar dependencias de teste:
  - `Microsoft.AspNetCore.Mvc.Testing`;
  - `xunit`;
  - `xunit.runner.visualstudio`;
  - `Microsoft.NET.Test.Sdk`.
- Criar uma factory de teste para o demo.
- Garantir isolamento basico de dados por teste.
- Criar testes para os endpoints ja existentes:
  - criar produto;
  - editar produto;
  - criar loja;
  - endpoints de busca/listagem ja mapeados.

### Criterios de aceite

- O projeto compila.
- O projeto roda pela solution.
- Os endpoints principais retornam status code esperado.
- Payloads e dados persistidos sao validados.

## Fase 2 - Cenarios de erro e contrato HTTP

### Tarefas

- Cobrir validacoes de entrada.
- Cobrir `not found` para edicao/busca de entidade inexistente.
- Validar shape de `ProblemDetails`/problemas retornados pela API.
- Validar rotas geradas, parametros de rota/query e nomes de endpoint quando relevante.
- Cobrir comportamento de handlers gerados com `WithValidateModel`, `EditEntity`, `ProduceNewEntity` e `MapResponseValues`.

### Criterios de aceite

- Erros esperados retornam status code correto.
- Problemas retornados mantem categoria, detail/type quando aplicavel.
- Testes falham se o generator quebrar o contrato HTTP observavel.

## Fase 3 - Concorrencia, retry e factories de Problem

### Tarefas

- Configurar infraestrutura de teste com SQLite in-memory para cenarios de concorrencia.
- Criar cenario que force conflito otimista.
- Validar retry com sucesso apos conflito transitorio.
- Validar retry esgotado.
- Validar `WithRetryOnConcurrency(Operation = "...")` usando:
  - delegate tipado sem DI;
  - delegate tipado com DI;
  - provider registrado no container.
- Validar fallback de `RetryOnConcurrencyOptions` quando nao houver registro especifico.

### Criterios de aceite

- O codigo gerado executa o retry em host real.
- `onExhausted` usa a factory correta por operacao.
- Problemas customizados sao retornados na API.
- O uso de SQLite evita falso positivo do EF InMemory em concorrencia.

## Verificacao

- `dotnet test src\RoyalCode.SmartCommands.Demo.Tests\RoyalCode.SmartCommands.Demo.Tests.csproj`
- `dotnet test src\SmartCommands.sln`

## Fora de escopo inicial

- Testar detalhes internos do source generator.
- Substituir os testes unitarios/snapshot existentes.
- Criar uma matriz completa de todos os atributos do SmartCommands na primeira fase.
