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

## Decisoes iniciais

- Manter cenarios pequenos e focados, para cada fase caber em uma revisao clara.
- Preferir regras de dominio reais a exemplos artificiais.
- Testar sempre pelo endpoint HTTP quando o objetivo for validar geracao de codigo e contrato publico.
- Usar testes unitarios apenas para regras de dominio puras ou gaps muito especificos.
- Registrar gaps em comentarios no plano ou em arquivo de review quando exigirem decisao de design.
- Evitar dependencias externas alem das libs RoyalCode ja usadas pela demo, salvo quando a fase justificar explicitamente.
- Preservar a demo como exemplo legivel: nomes, rotas e comandos devem ensinar o uso das libs.

## Estrutura esperada por fase

Para cada fase:

1. Modelar ou ajustar o dominio.
2. Criar comandos e endpoints.
3. Criar buscas ou detalhes quando fizer sentido.
4. Mapear problemas esperados.
5. Criar testes de sucesso, erro e persistencia.
6. Avaliar gaps de `SmartCommands`, `WorkContext`, `SmartProblems`, `SmartValidations`, `SmartSearch` e `Domain`.

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
- `Domain` para invariantes no agregado, se a lib estiver disponivel no repo/demo.

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

- `Domain` para regra de saldo e reserva.
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

### Gaps a observar

- Suporte mais natural a concurrency token/row version nos exemplos.
- Limites do retry quando ha multiplas entidades alteradas.
- Como documentar side effects que nao podem ficar dentro do retry.

## Fase 3 - Pedido simples

### Objetivo

Construir um fluxo de pedido que combine catalogo e estoque, exercitando orquestracao de regras e persistencia atomica.

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
- `Domain` para regras do pedido.
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

- `Domain` para transicoes ativo/inativo.
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

- `Domain` para maquina de estados simples.
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

## Registro de gaps

Cada gap encontrado deve ser classificado como:

- **Bug**: comportamento atual quebra o contrato esperado.
- **Design**: a API funciona, mas o uso fica pouco claro ou verboso.
- **Feature**: a necessidade e valida, mas ainda nao existe suporte na lib.
- **Docs**: o comportamento existe, mas precisa de exemplo ou documentacao.

O registro pode ficar neste plano enquanto a fase estiver ativa. Se o gap crescer ou exigir decisao, mover para um arquivo especifico em `.docs` ou plano proprio.

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
