# Plano: Cenarios de uso integrados na API Demo

## Objetivo

Evoluir `RoyalCode.SmartCommands.Demo` para conter exemplos de uso mais realistas, com API funcional e testes de integracao cobrindo o comportamento de ponta a ponta.

Cada fase deve entregar:

- um exemplo de dominio pequeno, mas plausivel;
- endpoints gerados por `SmartCommands`;
- persistencia via `WorkContext`;
- erros de negocio expostos com `SmartProblems`;
- validacoes com `SmartValidations`;
- buscas com `SmartSearch` quando aplicavel;
- testes em `RoyalCode.SmartCommands.Demo.Tests` usando host real e SQLite in-memory;
- notas sobre gaps encontrados nas libs RoyalCode.

O objetivo nao e criar uma aplicacao comercial completa. O objetivo e usar a demo como laboratorio vivo para provar integracao, documentar padroes de uso e revelar lacunas de design.

## Status

**Fases 1 (Catalogo) e 2 (Estoque e reserva) CONCLUIDAS.** Fases 3-6 pendentes. A demo ganhou dominio proprio (pasta
`Domain/`: `Produto`, `ProdutoEstoque`, `Loja`, `DemoDbContext`), substituindo os entes de Tests.Models (decisao de
base F2), com catalogo rico (SKU obrigatorio e unico, preco > 0, editar preservando SKU) e fluxo de estoque com saldo
disponivel/reservado, token `Version` e retry de concorrencia. Detalhes nos "Resultados" das fases 1 e 2.

Este plano e um **living plan**: cada fase concluida recebe status/notas, e o registro de gaps reflete o que foi achado
e onde foi resolvido.

## Resultados ja incorporados

Gaps ja descobertos pela demo e resolvidos nas libs (fecham o loop do laboratorio). Testes: `RoyalCode.SmartCommands.Demo.Tests` (execucao real, SQLite in-memory) + snapshots do gerador em `RoyalCode.SmartCommands.Tests`.

- **Concorrencia otimista com retry** [Feature] — `[WithRetryOnConcurrency]` + primitiva `IUnitOfWork.RetryOnConcurrencyAsync` no `RoyalCode.SmartCommands.WorkContext`; laco rollback + `CleanUp` entre tentativas; politica por `RetryOnConcurrencyOptions` (bind via `BindConfiguration`). Coberto por `DemoApiConcurrencyRetryTests` e `RetryOnConcurrencyTests`.
- **Problem factory por operation key** [Feature] — `IConcurrencyRetryProblemFactory` + delegates/provider + fallback por options, para customizar o problema quando o retry esgota. Coberto por `ConcurrencyRetryProblemFactoryTests` e cenarios de retry da demo.
- **Body-ness dos comandos** [Design] — comando sem forma de corpo (sem propriedades settaveis nem parametros de construtor publicos) nao entra como parametro do endpoint; e instanciado via `new`, e a requisicao pode ser enviada sem body. Coberto pelos snapshots em `Scenarios/Hs/Tests.cs`.
- **Guard de body ausente** [Bug] — comando com corpo cujo body nao chega vira `400 InvalidParameter` (SmartProblems), em vez de `NullReferenceException` (500). Coberto por `DemoApiErrorContractTests`.

## Decisoes iniciais

- Manter cenarios pequenos e focados, para cada fase caber em uma revisao clara.
- Preferir regras de dominio reais a exemplos artificiais.
- Testar sempre pelo endpoint HTTP quando o objetivo for validar geracao de codigo e contrato publico.
- Usar testes unitarios apenas para regras de dominio puras ou gaps muito especificos.
- Registrar gaps em comentarios no plano ou em arquivo de review quando exigirem decisao de design.
- Evitar dependencias externas alem das libs RoyalCode ja usadas pela demo, salvo quando a fase justificar explicitamente.
- Preservar a demo como exemplo legivel: nomes, rotas e comandos devem ensinar o uso das libs.
- **Modelos de dominio proprios da demo.** Nao reutilizar `RoyalCode.SmartCommands.Tests.Models` (`Produto`, `Some`): esse projeto e compartilhado com os testes de snapshot do gerador, e enriquecer `Produto` (SKU, preco, unicidade) quebraria esses testes. A demo passa a ter seus proprios entes.
- **Sem lib `Domain` dedicada.** Nao existe `RoyalCode.Domain` no repo; as invariantes de agregado usam `RoyalCode.Entities` e `RoyalCode.Repositories` como dependencia indireta. Remover das fases a mencao condicional "se a lib estiver disponivel".

## Estrutura esperada por fase

Para cada fase:

1. Modelar ou ajustar o dominio.
2. Criar comandos e endpoints.
3. Criar buscas ou detalhes quando fizer sentido.
4. Mapear problemas esperados.
5. Criar testes de sucesso, erro e persistencia.
6. Avaliar gaps de `SmartCommands`, `WorkContext`, `SmartProblems`, `SmartValidations`, `SmartSearch` e do modelo de dominio local da demo.

## Fase 1 - Catalogo com regras de dominio

### Objetivo

Transformar o exemplo de produto em um catalogo minimo com regras de dominio reais.

### Funcionalidades

- Criar produto com:
  - nome obrigatorio;
  - SKU obrigatorio;
  - SKU unico;
  - preco maior que zero;
  - status inicial ativo.
- Editar nome e preco.
- Impedir SKU duplicado.
- Obter detalhes do produto.
- Listar produtos ativos.

### Integracoes exercitadas

- `SmartCommands` para comandos de criacao e edicao.
- `SmartValidations` para validacoes simples de entrada.
- `SmartProblems` para erros de SKU duplicado e preco invalido.
- `WorkContext` para persistencia e unidade de trabalho.
- `SmartSearch` para listagem basica por nome, SKU e status.
- `Entities`/`Repositories` para invariantes no agregado (ver Decisoes iniciais; nao ha lib `Domain` dedicada).

### Testes

- Criar produto valido retorna `201` e persiste dados.
- Criar produto com nome vazio retorna `400`.
- Criar produto com preco zero ou negativo retorna problema esperado.
- Criar produto com SKU duplicado retorna conflito/estado invalido.
- Editar produto altera nome/preco e preserva SKU.
- Buscar/listar retorna apenas produtos esperados.

### Gaps a observar

- Como expressar validacao assincrona de unicidade com `SmartCommands`.
- Melhor padrao para converter erro de dominio em `Problem`.
- Se a API gerada ajuda ou atrapalha comandos com regras alem de `HasProblems`.

### Resultado — CONCLUIDA

Entregue com dominio proprio da demo (decisao de base F2). Nova pasta `Domain/` com `Produto` (rico: `Nome`, `Sku`,
`Preco`, `Ativo`, com `Editar`/`Desativar`), `Loja` e `DemoDbContext` (substitui o `CineDbContext` de Tests.Models;
indice unico de SKU). Os comandos/consultas de produto foram migrados e enriquecidos: `CriarProduto2` (validacao
`NotEmpty`/`GreaterThan` + unicidade de SKU por consulta assincrona, devolvendo `409` com `typeId`
`demo.produto.sku_duplicado`), `EditarProduto` (nome + preco, preservando SKU), `ProdutoDetalhes`/`ProdutoFiltro`
(SKU + preco; filtro por nome/SKU/status). `ProgramExtensions` passou a usar `AddWorkContext<DemoDbContext>()`.

**Invariantes no agregado:** o construtor de `Produto` e `Editar` guardam as invariantes (nome/SKU nao vazios,
preco > 0) com fail-fast — defesa em profundidade caso um comando futuro crie/edite `Produto` sem passar pela
validacao amigavel da feature (que continua no comando via `HasProblems`).

Testes: `CatalogoTests` (8 cenarios da Fase 1: criar valido, nome vazio, SKU vazio, preco invalido, SKU duplicado,
editar preservando SKU, listar por SKU/status) + os 17 testes de integracao existentes migrados — **25/25 verdes**;
suite do gerador **83/83**.

**Limitacao conhecida — corrida de SKU (F2).** O `AnyAsync` cobre o caso comum com `409` amigavel; o indice unico do
banco garante que nenhum SKU duplicado seja persistido. Porem, sob criacao **concorrente**, o perdedor cai na violacao
de constraint e hoje vira `500` (o `WorkContext.SaveAsync` converte `DbUpdateException` em `Problems.InternalError`), e
nao no mesmo `409`. Mapear violacao de constraint do provider para um `Problem` de dominio e um gap de stack
(WorkContext/SmartProblems), nao especifico da demo — nao resolvido aqui.

**Exceção temporaria — Tests.Models (F3).** `Produto`/`Loja` foram migrados (o ponto principal da Fase 1), mas os
exemplos de `Movies` (`ReviewDetails`/`ReviewFilter`) ainda usam `Movie`/`Review` de `RoyalCode.SmartCommands.Tests.Models`,
e o `.csproj` mantem a referencia por causa deles. Esses exemplos nao estao ligados ao app (nao ha grupo/rota de Movies
em `ConfigurePipeline`). Migrar ou remover o dominio de `Movies` fica para uma fase futura; ate la, referencia mantida
como excecao.

**Gap descoberto** [Bug] — `RoyalCode.SmartSearch.Linq.Sortings.OrderByProvider.GetDefaultHandler` inicializa o
handler de ordenacao default (ex.: `(Produto, Id)`) num dicionario estatico **nao thread-safe**: sob execucao paralela,
dois testes que disparam a primeira busca da mesma entidade colidem com *"An item with the same key has already been
added"*. Contorno: os testes de integracao da demo rodam **serialmente** (`DisableTestParallelization` em
`AssemblyInfo.cs`). Correcao pertence a `SmartSearch` (outro repo) — registrado abaixo.

## Fase 2 - Estoque e reserva

### Objetivo

Criar um fluxo pequeno de estoque para exercitar transacao, concorrencia e regras de negocio com impacto real.

### Funcionalidades

- Registrar saldo inicial de estoque para um produto.
- Adicionar entrada de estoque.
- Reservar quantidade.
- Liberar reserva.
- Impedir reserva acima do disponivel.
- Consultar saldo disponivel e reservado.

### Integracoes exercitadas

- Entidades/agregados locais da demo para regra de saldo e reserva.
- `WorkContext` para transacao e persistencia de mutacoes.
- `SmartCommands` para comandos de entrada, reserva e liberacao.
- `SmartProblems` para estoque insuficiente e produto inexistente.
- `WithRetryOnConcurrency` para conflito de atualizacao do estoque.
- SQLite in-memory nos testes para evitar falso positivo do EF InMemory.

### Testes

- Entrada de estoque aumenta saldo disponivel.
- Reserva reduz disponivel e aumenta reservado.
- Liberacao reduz reservado e aumenta disponivel.
- Reserva acima do disponivel retorna problema.
- Reserva de produto inexistente retorna `404`.
- Conflito transitorio no save tenta novamente e conclui.
- Conflito persistente retorna problema configurado pela operation key.

### Decisao tomada

Usar `Version` inteiro no agregado `ProdutoEstoque`, configurado como concurrency token no EF. O dominio incrementa a
versao em cada mutacao de saldo/reserva. Em SQLite isso evita depender de `rowversion` e deixa o exemplo portavel.

### Gaps a observar

- Suporte mais natural a concurrency token/`Version` nos exemplos.
- Limites do retry quando ha multiplas entidades alteradas.
- Como documentar side effects que nao podem ficar dentro do retry.

### Resultado - CONCLUIDA

Entregue com `ProdutoEstoque` como agregado proprio da demo, contendo `Disponivel`, `Reservado` e `Version`.
`DemoDbContext` mapeia `Version` como concurrency token e relaciona estoque 1:1 com `Produto`.

Comandos/endpoints gerados em `/produtos/{id}/estoque`: registrar saldo inicial, adicionar entrada, reservar e liberar
reserva. Todos usam `EditEntity<Produto, Guid>` para reaproveitar o `404` de produto inexistente, `WithValidateModel`
para quantidade positiva, `WithWorkContext` para transacao e `[WithRetryOnConcurrency]` para conflitos de atualizacao.
As regras do agregado `ProdutoEstoque` retornam `Result`/`Result<ProdutoEstoque>`, evitando exception como fluxo de
negocio e eliminando a duplicacao "checar antes, executar depois" nos comandos.

A consulta `GET /produtos/{id}/estoque` e gerada por `MapFind` + `AutoSelect` sobre `ProdutoEstoqueDetalhes`. Como
`ProdutoEstoque.Id == Produto.Id`, a rota continua usando o id do produto; se o estoque ainda nao foi registrado, o
recurso `/estoque` retorna `404` em vez de saldo zerado sintetico.

`ReservarEstoque` usa operation key `demo.estoques.reservar` com problem configurado para retry esgotado
("O estoque foi alterado por outro processo."). Estoque insuficiente e estoque nao registrado retornam `409` via
`SmartProblems`.

Testes: `EstoqueTests` cobre 8 cenarios (entrada, reserva, liberacao, consulta sem estoque registrado, estoque insuficiente,
produto inexistente, conflito transitorio e conflito persistente com problem da operation key). Suite da demo:
**33/33 verdes**. Suite completa: `RoyalCode.SmartCommands.Tests` **83/83** + demo **33/33**.

**Gap descoberto** [Feature/Design] - `[WithRetryOnConcurrency]` no caminho atual funciona bem com `Result`, mas nao com
comando mutacional retornando `Result<T>` dentro da primitiva de retry. A fase ficou intencionalmente como comandos
mutacionais `Result` + consulta GET para ler o estado. Se a lib quiser suportar "muta e retorna DTO" com retry, falta
um overload generico de `RetryOnConcurrencyAsync` e ajuste no gerador.

## Fase 3 - Pedido simples

### Objetivo

Construir um fluxo de pedido que combine catalogo e estoque, exercitando orquestracao de regras e persistencia atomica.

> **Fatiar para revisao clara** (principio "pequeno e focado"): 3a — criar pedido + reservar estoque; 3b — cancelar pedido + liberar reservas; 3c — consulta/listagem. As funcionalidades abaixo cobrem os tres cortes; executar e revisar por corte.

### Funcionalidades

- Criar pedido com itens.
- Validar produto existente e ativo.
- Validar quantidade maior que zero.
- Reservar estoque durante a criacao do pedido.
- Calcular total do pedido a partir do preco atual.
- Consultar detalhes do pedido.
- Cancelar pedido e liberar reservas.

### Integracoes exercitadas

- `SmartCommands` para comando com lista de itens.
- `SmartValidations` para validacao estrutural do request.
- Entidades/agregados locais da demo para regras do pedido.
- `WorkContext` para transacao envolvendo pedido e estoque.
- `SmartProblems` para produto inativo, estoque insuficiente e pedido inexistente.
- `SmartSearch` para listagem de pedidos por status.

### Testes

- Criar pedido valido retorna `201`, cria pedido e reserva estoque.
- Criar pedido com item invalido retorna `400`.
- Criar pedido com produto inativo retorna problema de negocio.
- Criar pedido sem estoque suficiente retorna problema e nao persiste pedido parcial.
- Cancelar pedido libera estoque reservado.
- Buscar detalhes retorna itens e total calculado.

### Gaps a observar

- Ergonomia de comandos com colecoes complexas.
- Padrao para orquestrar multiplos agregados sem esconder regra em handler gerado.
- Necessidade de decorators especificos para carregamento de contexto.

## Fase 4 - Busca avancada de produtos

### Objetivo

Expandir os exemplos de busca para cobrir filtros, ordenacao, paginacao e erros de query em um caso de catalogo.

### Funcionalidades

- Buscar produtos por:
  - nome parcial;
  - SKU;
  - faixa de preco;
  - status;
  - disponibilidade em estoque.
- Ordenar por nome, preco e data de criacao.
- Paginar resultados.
- Retornar shape estavel para busca.

### Integracoes exercitadas

- `SmartSearch` com criterios compostos.
- `SmartCommands` para endpoint gerado de search.
- `WorkContext`/EF para traducao de query.
- `SmartProblems` para query invalida.
- OpenAPI gerado quando aplicavel.

### Testes

- Filtro por nome retorna produtos esperados.
- Filtro por faixa de preco respeita limites.
- Ordenacao por preco ascendente/descendente funciona.
- Paginacao retorna total/itens conforme contrato atual.
- Query invalida retorna problema esperado.
- Busca por disponibilidade considera estoque reservado.

### Gaps a observar

- Clareza do contrato HTTP para filtros complexos.
- Como expor erros de parsing/ordenacao de forma padronizada.
- Qualidade do OpenAPI gerado para endpoints de search.

## Fase 5 - Soft delete e desativacao

### Objetivo

Modelar remocao logica/desativacao para exercitar filtros padrao, comandos de estado e buscas administrativas.

### Funcionalidades

- Desativar produto.
- Reativar produto.
- Impedir criacao de pedido com produto desativado.
- Listagem publica deve esconder inativos por padrao.
- Busca administrativa pode incluir inativos via parametro explicito.
- Detalhes de produto inativo devem seguir regra definida pela API.

### Integracoes exercitadas

- Entidades/agregados locais da demo para transicoes ativo/inativo.
- `SmartCommands` para comandos de desativacao/reativacao.
- `SmartProblems` para estado invalido.
- `SmartSearch` para filtro padrao e filtro administrativo.
- `WorkContext` para persistencia do estado.

### Testes

- Desativar produto ativo retorna `200`.
- Desativar produto ja inativo retorna problema de estado invalido ou sucesso idempotente, conforme decisao da fase.
- Reativar produto retorna `200`.
- Listagem publica nao retorna produto inativo.
- Busca administrativa retorna produto inativo quando solicitado.
- Pedido com produto inativo falha sem reservar estoque.

### Decisao requerida

Definir se desativar produto ja inativo deve ser:

- idempotente e retornar sucesso;
- erro de estado invalido.

### Gaps a observar

- Suporte a filtros globais ou criterios padrao em `SmartSearch`/`WorkContext`.
- Como diferenciar busca publica e administrativa sem duplicar muito codigo.
- Padrao para comandos idempotentes.

## Fase 6 - Fluxo de aprovacao e publicacao

### Objetivo

Criar um fluxo de estado com transicoes controladas, bom para demonstrar regras de dominio e problemas de negocio previsiveis.

### Funcionalidades

- Produto pode nascer como rascunho.
- Enviar produto para revisao.
- Aprovar produto.
- Rejeitar produto com motivo.
- Publicar produto aprovado.
- Impedir transicoes invalidas.
- Consultar historico minimo de transicoes, se isso nao aumentar demais a fase.

### Integracoes exercitadas

- Entidades/agregados locais da demo para maquina de estados simples.
- `SmartCommands` para comandos por transicao.
- `SmartProblems` para transicao invalida.
- `SmartValidations` para campos obrigatorios, como motivo de rejeicao.
- `WorkContext` para persistencia das transicoes.
- `SmartSearch` para filtro por estado de publicacao.

### Testes

- Rascunho pode ir para revisao.
- Produto em revisao pode ser aprovado.
- Produto aprovado pode ser publicado.
- Produto rascunho nao pode ser publicado direto.
- Rejeicao exige motivo.
- Historico registra transicoes quando implementado.
- Busca por estado retorna produtos corretos.

### Gaps a observar

- Padrao para comandos de transicao de estado.
- Melhor forma de retornar problema com estado atual e transicao tentada.
- Se `SmartCommands` precisa de suporte mais ergonomico para comandos sem body ou com body minimo.

## Verificacao geral

Ao final de cada fase:

- `dotnet test src\RoyalCode.SmartCommands.Demo.Tests\RoyalCode.SmartCommands.Demo.Tests.csproj`
- `dotnet test src\SmartCommands.sln`

Quando a fase alterar generator ou WorkContext compartilhado, adicionar tambem testes unitarios/snapshot no projeto `RoyalCode.SmartCommands.Tests`, se aplicavel.

### Isolamento de testes

Hoje cada teste cria sua propria `DemoApiFactory` com conexao SQLite in-memory propria e chama `EnsureDeleted`/`EnsureCreated` (isolamento por teste, ok no momento). Antes de crescer fixtures/dados nas fases, consolidar e documentar a estrategia (seed compartilhado vs. reset por teste) para evitar acoplamento entre cenarios.

### OpenAPI

`WithOpenApi` aparece como gap de qualidade (Fase 4), mas ainda nao ha estrategia de teste do **documento** gerado — os testes atuais validam apenas rotas via `EndpointDataSource`. Quando a Fase 4 chegar, definir como assertar o schema/spec, nao so as rotas.

## Registro de gaps

Cada gap encontrado deve ser classificado como:

- **Bug**: comportamento atual quebra o contrato esperado.
- **Design**: a API funciona, mas o uso fica pouco claro ou verboso.
- **Feature**: a necessidade e valida, mas ainda nao existe suporte na lib.
- **Docs**: o comportamento existe, mas precisa de exemplo ou documentacao.

O registro pode ficar neste plano enquanto a fase estiver ativa. Se o gap crescer ou exigir decisao, mover para um arquivo especifico em `.docs` ou plano proprio.

### Gaps abertos

- **[Bug] `SmartSearch.OrderByProvider` nao thread-safe (Fase 1).** `GetDefaultHandler` popula o handler de ordenacao
  default (ex.: chave da entidade) num dicionario estatico sem sincronizacao; a primeira busca concorrente da mesma
  entidade lanca `ArgumentException: An item with the same key has already been added`. Contornado serializando os
  testes de integracao da demo (`DisableTestParallelization`). A correcao (tornar o cache thread-safe / usar
  `TryAdd`/`GetOrAdd`) pertence ao repo `SmartSearch`.
- **[Feature/Design] `WithRetryOnConcurrency` com `Result<T>` (Fase 2).** O gerador/primitiva atual cobre o retry com
  `Result` simples. Para comandos que mutam estado e precisam retornar DTO tipado no mesmo endpoint, falta suporte
  generico. Contorno usado na demo: comando retorna `Result` e o estado e lido pelo GET.

## Fora de escopo

- Transformar a demo em produto completo.
- Criar autenticacao/autorizacao real.
- Cobrir todos os atributos do `SmartCommands` em matriz completa.
- Substituir testes unitarios existentes dos generators.
- Criar banco externo para testes.

## Ordem recomendada

1. Catalogo com regras de dominio.
2. Estoque e reserva.
3. Pedido simples.
4. Busca avancada de produtos.
5. Soft delete e desativacao.
6. Fluxo de aprovacao e publicacao.

Essa ordem maximiza reaproveitamento: catalogo prepara produto, estoque usa produto, pedido usa catalogo e estoque, busca amplia leitura, soft delete ajusta regras transversais, e aprovacao consolida comandos de estado.
