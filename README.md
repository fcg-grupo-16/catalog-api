# CatalogAPI — FIAP Cloud Games

Microsserviço de catálogo de jogos, biblioteca do usuário e início do fluxo de compra da plataforma FIAP Cloud Games (FCG) — Fase 2.

![CI](https://github.com/fcg-grupo-16/catalog-api/actions/workflows/ci.yml/badge.svg)

---

## 1. Visão geral

A **CatalogAPI** é um dos microsserviços do ecossistema FCG (organização [`fcg-grupo-16`](https://github.com/fcg-grupo-16)). Suas responsabilidades são:

- **CRUD de jogos** (catálogo): listar, obter por ID, cadastrar, atualizar e desativar (soft delete).
- **Biblioteca do usuário**: listar os jogos que um usuário já adquiriu.
- **Início do fluxo de compra**: validar o jogo e disparar a compra de forma **assíncrona** (orientada a eventos).

Este serviço **não** gerencia usuários nem emite tokens. Ele apenas **valida** o JWT emitido pela `users-api` e lê a identidade do usuário a partir da claim do token (ver seção 6).

### Eventos

Os contratos de evento vivem no namespace `Fcg.Contracts.Events` (arquivo `src/Fcg.Catalog.Application/Contracts/Events.cs`). Esse namespace e os nomes dos tipos **devem ser idênticos em todos os microsserviços** — o MassTransit identifica a mensagem pela URN derivada de `namespace:NomeDoTipo` (ex.: `urn:message:Fcg.Contracts.Events:OrderPlacedEvent`).

**Publica `OrderPlacedEvent`** (ao iniciar uma compra; consumido pela `payments-api`):

| Campo | Tipo | Descrição |
| --- | --- | --- |
| `OrderId` | `Guid` | Identificador do pedido (gerado na CatalogAPI). |
| `UserId` | `string` | ID do usuário (claim do JWT). |
| `GameId` | `string` | ID do jogo a adquirir. |
| `Price` | `decimal` | Preço do jogo no momento da compra. |

**Consome `PaymentProcessedEvent`** (publicado pela `payments-api`; grava na biblioteca se aprovado):

| Campo | Tipo | Descrição |
| --- | --- | --- |
| `OrderId` | `Guid` | Identificador do pedido. |
| `UserId` | `string` | ID do usuário. |
| `GameId` | `string` | ID do jogo. |
| `Price` | `decimal` | Preço processado. |
| `Status` | `string` | `"Approved"` ou `"Rejected"`. |

O `PaymentProcessedConsumer` grava o jogo na biblioteca **somente** quando `Status == "Approved"` (de forma idempotente). Quando `Rejected`, apenas registra um log e nenhuma ação é tomada.

### Diagrama do fluxo de compra

```text
  Cliente                CatalogAPI                  RabbitMQ              payments-api
    |                        |                          |                       |
    |  POST /api/v1/biblioteca|                          |                       |
    |----------------------->|                          |                       |
    |                        | valida jogo (existe,     |                       |
    |                        | ativo, não possuído)     |                       |
    |                        |--- publica OrderPlacedEvent -------------------->|
    |   202 Accepted         |                          |                       |
    |<-----------------------|                          |                       |
    |                        |                          |   processa pagamento  |
    |                        |                          |<-- PaymentProcessedEvent
    |                        | PaymentProcessedConsumer |                       |
    |                        |<-------------------------|                       |
    |                        | se Approved => grava na  |                       |
    |                        | biblioteca (idempotente) |                       |
    |                        |                          |                       |
    |  GET /api/v1/biblioteca |                          |                       |
    |----------------------->| jogo já aparece          |                       |
    |<-----------------------|                          |                       |
```

---

## 2. Stack

- **.NET 10** (C#)
- **Clean Architecture** — 4 projetos (Domain, Application, Infrastructure, API)
- **MongoDB** (via EF Core provider `MongoDB.EntityFrameworkCore`) — database `catalogdb`
- **RabbitMQ + MassTransit 8.x** — mensageria pub/sub
- **Serilog** — logging estruturado (JSON compacto no console)
- **FluentValidation** — validação de entrada
- **xUnit** — testes unitários
- **JWT Bearer** — autenticação/autorização (validação de token)
- **Swagger / OpenAPI** — documentação (apenas em Development)

---

## 3. Arquitetura

Estrutura de pastas (top 3 níveis):

```text
catalog-api/
├── CatalogApi.sln
├── Dockerfile
├── global.json
├── k8s/
│   ├── configmap.yaml
│   ├── deployment.yaml
│   ├── secret.yaml
│   └── service.yaml
├── src/
│   ├── Fcg.Catalog.Domain/
│   │   ├── Entities/          (Jogo, BibliotecaJogo)
│   │   ├── Enums/             (GeneroJogo)
│   │   ├── Exceptions/
│   │   ├── Repositories/      (IJogoRepository, IBibliotecaRepository)
│   │   └── ValueObjects/      (Preco)
│   ├── Fcg.Catalog.Application/
│   │   ├── Contracts/         (Events.cs)
│   │   ├── DTOs/              (Request, Response)
│   │   ├── Interfaces/        (IEventPublisher)
│   │   ├── Services/          (JogoService, BibliotecaService, PurchaseService)
│   │   └── Validators/
│   ├── Fcg.Catalog.Infrastructure/
│   │   ├── Extensions/        (ServiceCollectionExtensions)
│   │   ├── Messaging/         (MassTransitEventPublisher, PaymentProcessedConsumer)
│   │   ├── Persistence/       (AppDbContext, Configurations)
│   │   ├── Repositories/
│   │   ├── Seed/              (DatabaseSeed)
│   │   └── Settings/          (MongoDbSettings, JwtSettings)
│   └── Fcg.Catalog.Api/
│       ├── Controllers/       (JogosController, BibliotecaController)
│       ├── Extensions/
│       ├── Middlewares/
│       └── Program.cs
└── tests/
    └── Fcg.Catalog.UnitTests/
        ├── Entities/
        ├── Services/          (PurchaseServiceTests, JogoServiceTests)
        ├── Validators/
        └── ValueObjects/
```

### Papel de cada projeto

| Projeto | Responsabilidade |
| --- | --- |
| `Fcg.Catalog.Domain` | Entidades (`Jogo`, `BibliotecaJogo`), value object `Preco`, enum `GeneroJogo`, exceções de domínio e interfaces de repositório. Sem dependências externas. |
| `Fcg.Catalog.Application` | DTOs, serviços de caso de uso (`JogoService`, `BibliotecaService`, `PurchaseService`), validadores FluentValidation, abstração `IEventPublisher` e os contratos de evento (`Contracts/Events.cs`). |
| `Fcg.Catalog.Infrastructure` | `AppDbContext` (MongoDB/EF Core), configurações de mapeamento, repositórios, `MassTransitEventPublisher`, `PaymentProcessedConsumer`, seed de dados e settings. |
| `Fcg.Catalog.Api` | Controllers, middlewares (correlation id, exception handler), composição da DI e `Program.cs`. Porta 8080. |

### Como os dois lados do fluxo de eventos se encaixam

- **Publicação — `PurchaseService`** (`src/Fcg.Catalog.Application/Services/PurchaseService.cs`): o método `IniciarCompraAsync(usuarioId, jogoId)` valida que o jogo existe, está ativo e não está na biblioteca do usuário; gera um `OrderId` e publica `OrderPlacedEvent` através de `IEventPublisher` (implementado por `MassTransitEventPublisher`). Não grava nada na biblioteca — é o gatilho assíncrono.
- **Consumo — `PaymentProcessedConsumer`** (`src/Fcg.Catalog.Infrastructure/Messaging/PaymentProcessedConsumer.cs`): implementa `IConsumer<PaymentProcessedEvent>`. Quando o `Status` é `Approved`, cria um `BibliotecaJogo(UserId, GameId)` e o persiste via `IBibliotecaRepository` (ignorando se o usuário já o possui — idempotência). Quando `Rejected`, apenas loga.

O MassTransit usa um prefixo de fila por serviço (`KebabCaseEndpointNameFormatter("catalog", ...)`), garantindo filas distintas entre microsserviços que consomem o mesmo evento (fanout pub/sub, não competing consumers).

---

## 4. Pré-requisitos

- **.NET 10 SDK** (`global.json` fixa a versão `10.0.100`, com `rollForward: latestFeature`).
- **Docker** (para subir dependências e/ou rodar a imagem).
- **MongoDB** e **RabbitMQ** acessíveis.

Subindo as dependências localmente via Docker:

```bash
# MongoDB
docker run -d --name fcg-mongo -p 27017:27017 mongo:7

# RabbitMQ (com painel de gestão em http://localhost:15672, login guest/guest)
docker run -d --name fcg-rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```

---

## 5. Variáveis de ambiente

Todas as chaves do `appsettings.json` podem ser sobrescritas por variável de ambiente usando duplo underscore (`__`) como separador de seção.

| Variável (`Secao__Chave`) | Descrição | Default |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | Ambiente de execução. Swagger só é exposto em `Development`. | `Production` (definido nos manifestos k8s) |
| `MongoDbSettings__ConnectionString` | String de conexão do MongoDB. | `mongodb://localhost:27017` |
| `MongoDbSettings__DatabaseName` | Nome do banco de dados. | `catalogdb` |
| `JwtSettings__SecretKey` | Chave HMAC (≥256 bits) usada para **validar** o token. **Deve ser idêntica à da `users-api`.** | `OVERRIDE_VIA_ENV_VAR_EM_PRODUCAO` (substituir!) |
| `JwtSettings__Issuer` | Issuer válido do token. | `FiapCloudGames` |
| `JwtSettings__Audience` | Audience válida do token. | `FiapCloudGames` |
| `JwtSettings__ExpiracaoEmMinutos` | Tempo de expiração de referência (a CatalogAPI valida `exp`, não emite tokens). | `30` |
| `RabbitMq__Host` | Host do RabbitMQ. | `localhost` |
| `RabbitMq__Username` | Usuário do RabbitMQ. | `guest` |
| `RabbitMq__Password` | Senha do RabbitMQ. | `guest` |

> **Importante:** `JwtSettings__SecretKey` precisa ser **exatamente igual** à chave configurada na `users-api`, assim como `Issuer` e `Audience` (`FiapCloudGames`). Se a chave divergir, todos os endpoints autenticados retornarão **401 Unauthorized**.

---

## 6. Como rodar localmente (dotnet)

1. Suba MongoDB e RabbitMQ (ver seção 4).
2. Defina a `SecretKey` (deve bater com a `users-api`):

```bash
export JwtSettings__SecretKey="FiapCloudGames_Dev_SecretKey_Com_Pelo_Menos_256_Bits_Para_HMAC_SHA256!"
export ASPNETCORE_ENVIRONMENT=Development
```

3. Restaure, compile e execute:

```bash
dotnet restore
dotnet build -c Release --no-restore
dotnet run --project src/Fcg.Catalog.Api
```

A API sobe na porta **8080**:

- API: `http://localhost:8080`
- Health check: `http://localhost:8080/health`
- Swagger (somente em `Development`): `http://localhost:8080/swagger`

Na inicialização, o serviço executa um **seed de 5 jogos** de exemplo (se a coleção estiver vazia).

### Obtendo um JWT para chamar endpoints autenticados

A CatalogAPI **não emite tokens** — ela apenas os valida. Para chamar `/api/v1/biblioteca` (ou criar/editar jogos como admin), obtenha um JWT da `users-api`:

1. Cadastre/autentique um usuário na `users-api` (ex.: `POST /api/v1/auth/login`).
2. Copie o token retornado.
3. Envie o token no header das requisições à CatalogAPI:

```bash
curl http://localhost:8080/api/v1/biblioteca \
  -H "Authorization: Bearer <SEU_TOKEN_JWT>"
```

> No Swagger, use o botão **Authorize** e informe apenas o token (sem o prefixo `Bearer`).
>
> Garanta que a `users-api` e a `catalog-api` compartilhem a mesma `JwtSettings__SecretKey`, `Issuer` e `Audience`.

---

## 7. Como rodar com Docker

Build da imagem (multi-stage, baseada em `mcr.microsoft.com/dotnet/aspnet:10.0`):

```bash
docker build -t catalog-api:local .
```

Executando o container (mapeando a porta do host `8082` para a `8080` interna):

```bash
docker run --rm -p 8082:8080 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e MongoDbSettings__ConnectionString="mongodb://host.docker.internal:27017" \
  -e RabbitMq__Host="host.docker.internal" \
  -e JwtSettings__SecretKey="FiapCloudGames_Dev_SecretKey_Com_Pelo_Menos_256_Bits_Para_HMAC_SHA256!" \
  catalog-api:local
```

A API ficará acessível em `http://localhost:8082` (health em `http://localhost:8082/health`).

> Em macOS/Windows, `host.docker.internal` aponta para o host. Em Linux, ajuste para o IP do host ou rode tudo em uma rede Docker compartilhada (ver seção 8).

---

## 8. Rodar o ecossistema completo (end-to-end)

Para subir a CatalogAPI junto com `users-api`, `payments-api`, `notifications-api`, MongoDB e RabbitMQ de uma vez, use o repositório de orquestração:

**https://github.com/fcg-grupo-16/orchestration**

```bash
git clone https://github.com/fcg-grupo-16/orchestration.git
cd orchestration
docker compose up
```

### Exemplo do fluxo de compra (end-to-end)

Com o ecossistema no ar e um JWT válido em mãos:

```bash
TOKEN="<SEU_TOKEN_JWT>"

# 1) Descubra um jogo do catálogo (anônimo)
curl http://localhost:8080/api/v1/jogos

# 2) Inicie a compra (assíncrono -> 202 Accepted)
curl -i -X POST http://localhost:8080/api/v1/biblioteca \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "jogoId": "<ID_DO_JOGO>" }'
# HTTP/1.1 202 Accepted

# 3) Aguarde o processamento do pagamento e consulte a biblioteca
curl http://localhost:8080/api/v1/biblioteca \
  -H "Authorization: Bearer $TOKEN"
# o jogo aparece aqui assim que o PaymentProcessedEvent (Approved) for consumido
```

O `202 Accepted` significa que a compra foi **aceita para processamento**, não que já foi concluída. O jogo só passa a aparecer na biblioteca após o `PaymentProcessedConsumer` receber o evento de pagamento **aprovado**.

---

## 9. Endpoints

| Método | Rota | Autorização / Policy | Descrição |
| --- | --- | --- | --- |
| GET | `/api/v1/jogos` | Anônimo (`AllowAnonymous`) | Lista jogos com paginação (`pagina`, `tamanhoPagina`) e filtro opcional por `genero`. |
| GET | `/api/v1/jogos/{id}` | Anônimo (`AllowAnonymous`) | Obtém um jogo por ID. |
| POST | `/api/v1/jogos` | `ApenasAdmin` (role `Administrador`) | Cadastra um novo jogo. Retorna `201 Created`. |
| PUT | `/api/v1/jogos/{id}` | `ApenasAdmin` (role `Administrador`) | Atualiza um jogo. |
| DELETE | `/api/v1/jogos/{id}` | `ApenasAdmin` (role `Administrador`) | Desativa (soft delete) um jogo. Retorna `204 No Content`. |
| GET | `/api/v1/biblioteca` | `UsuarioAutenticado` | Lista a biblioteca do usuário autenticado (id vindo da claim do token). |
| POST | `/api/v1/biblioteca` | `UsuarioAutenticado` | Inicia a compra de um jogo: publica `OrderPlacedEvent`. Retorna `202 Accepted`. |
| GET | `/health` | Anônimo | Health check. |

> O controller de jogos exige `UsuarioAutenticado` por padrão, mas as ações de **leitura** (`GET`) são marcadas com `AllowAnonymous`; as ações de **escrita** exigem a role `Administrador`. O controller de biblioteca exige usuário autenticado em todas as ações.

Códigos de status relevantes do `POST /api/v1/biblioteca`: `202` (aceito), `404` (jogo não encontrado), `409` (jogo já adquirido), `422` (jogo inativo).

---

## 10. Testes

Os testes unitários ficam em `tests/Fcg.Catalog.UnitTests` (xUnit) e cobrem entidades, value objects, validadores e serviços — incluindo `PurchaseServiceTests` (regras do início de compra) e `JogoServiceTests`.

```bash
dotnet test -c Release
```

A pipeline de CI (`.github/workflows/ci.yml`) roda `dotnet restore`, `dotnet build -c Release` e `dotnet test -c Release` em todo push e PR para `main`.

---

## 11. Como contribuir

1. Abra (ou pegue) uma **issue** descrevendo a tarefa.
2. Crie uma **branch** a partir de `main`: `feat/<n>-descricao` ou `fix/<n>-descricao` (onde `<n>` é o número da issue).
3. Faça commits seguindo **Conventional Commits** (`feat:`, `fix:`, `docs:`, `test:`, `refactor:`, etc.).
4. Abra um **Pull Request** para `main`, referenciando a issue com `Closes #n` na descrição.
5. Garanta a **CI verde** (build + testes).
6. Após aprovação e CI verde, faça o **merge**.

> **Nunca** faça commit de segredos (chaves JWT reais, senhas, connection strings de produção). Use variáveis de ambiente e os manifestos de `Secret` do Kubernetes.

---

## 12. Deploy de versão

Versionamento por **SemVer**, marcado com tags (`vMAJOR.MINOR.PATCH`).

```bash
# 1) Build da imagem com a tag de versão
docker build -t ghcr.io/fcg-grupo-16/catalog-api:v1.2.3 .

# 2) Push para o GitHub Container Registry (GHCR)
docker push ghcr.io/fcg-grupo-16/catalog-api:v1.2.3

# 3a) Atualize a imagem no manifesto k8s/deployment.yaml e aplique
#     (altere o campo image: para ghcr.io/fcg-grupo-16/catalog-api:v1.2.3)
kubectl apply -f k8s/deployment.yaml

# 3b) OU atualize diretamente no cluster, sem editar o YAML:
kubectl set image deployment/catalog-api \
  catalog-api=ghcr.io/fcg-grupo-16/catalog-api:v1.2.3 -n fcg
```

---

## 13. Kubernetes

Os manifestos vivem em `k8s/`:

| Arquivo | Função |
| --- | --- |
| `k8s/configmap.yaml` | `ConfigMap` `catalog-api-config` com config não sensível (`ASPNETCORE_ENVIRONMENT`, `RabbitMq__Host`, `MongoDbSettings__DatabaseName`, `JwtSettings__Issuer`, `JwtSettings__Audience`). |
| `k8s/secret.yaml` | `Secret` `catalog-api-secret` com dados sensíveis (`MongoDbSettings__ConnectionString`, `JwtSettings__SecretKey`, credenciais do RabbitMQ). A `SecretKey` deve bater com a da `users-api`. |
| `k8s/deployment.yaml` | `Deployment` `catalog-api` (1 réplica, container na porta 8080, `envFrom` ConfigMap+Secret, liveness/readiness em `/health`). |
| `k8s/service.yaml` | `Service` `catalog-api` do tipo `ClusterIP` (porta 80 → targetPort 8080). |

Aplicando tudo no namespace `fcg`:

```bash
kubectl apply -f k8s/ -n fcg
```

> O deploy **agregado** de toda a plataforma (todos os microsserviços + dependências) é coordenado pelo repositório [`orchestration`](https://github.com/fcg-grupo-16/orchestration). Os manifestos aqui cobrem apenas a CatalogAPI.

---

## 14. Troubleshooting

| Sintoma | Causa provável | Solução |
| --- | --- | --- |
| **401 Unauthorized** em endpoints autenticados | `JwtSettings__SecretKey` (ou `Issuer`/`Audience`) diverge da `users-api`. | Use exatamente a **mesma** `SecretKey`, `Issuer` (`FiapCloudGames`) e `Audience` (`FiapCloudGames`) nos dois serviços. Verifique também se o token não expirou (`ClockSkew` é zero). |
| App não sobe / falha ao iniciar mensageria | **RabbitMQ indisponível** ou credenciais erradas. | Confirme `RabbitMq__Host/Username/Password` e que o RabbitMQ está no ar (painel em `:15672`). |
| Erros de conexão de dados / seed falha | **MongoDB indisponível** ou `ConnectionString` incorreta. | Verifique `MongoDbSettings__ConnectionString` e se o Mongo está acessível na porta 27017. |
| Compra retorna `202` mas o **jogo não aparece na biblioteca** | O pagamento foi **rejeitado** pela `payments-api` (ex.: preço acima do limite configurado na payments-api), então o `PaymentProcessedEvent` veio com `Status = Rejected` e nada é gravado. | Confira os logs da CatalogAPI (mensagem "Pagamento rejeitado...") e as regras/limite da `payments-api`. Compras aprovadas levam alguns instantes para refletir (processamento assíncrono). |
| Swagger retorna 404 | Swagger só é exposto em `Development`. | Defina `ASPNETCORE_ENVIRONMENT=Development`. |
| Porta ocupada / não responde | A API escuta na porta **8080** (`ASPNETCORE_URLS=http://+:8080`). | Garanta que a 8080 esteja livre no container e ajuste o mapeamento do host (`-p <host>:8080`). |
