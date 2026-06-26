# FIAP Cloud Games — Catalog API

Microsserviço de catálogo da plataforma FIAP Cloud Games (FCG), Fase 2.

## Propósito

- CRUD de jogos (catálogo).
- Biblioteca de jogos adquiridos por usuário.
- Inicia o fluxo de compra de forma assíncrona, publicando o evento `OrderPlacedEvent` para o `payments-api`.
- Consome o evento `PaymentProcessedEvent`: quando o pagamento é `Approved`, grava o jogo na biblioteca do usuário (de forma idempotente); quando é `Rejected`, apenas registra em log.

Este serviço **não** possui entidade/repositório de `Usuario`. A identidade do usuário vem do JWT validado (claim `NameIdentifier`), emitido pela `users-api`.

## Arquitetura

Clean Architecture, 4 projetos (`CatalogApi.sln`):

| Projeto | Responsabilidade |
| --- | --- |
| `Fcg.Catalog.Domain` | Entidades (`Jogo`, `BibliotecaJogo`), value object `Preco`, enum `GeneroJogo`, exceções, interfaces de repositório. |
| `Fcg.Catalog.Application` | DTOs, serviços (`JogoService`, `BibliotecaService`, `PurchaseService`), validadores, `IEventPublisher`, contratos de evento (`Contracts/Events.cs`). |
| `Fcg.Catalog.Infrastructure` | `AppDbContext` (MongoDB/EF Core), configurações, repositórios, settings, `MassTransitEventPublisher`, `PaymentProcessedConsumer`, seed. |
| `Fcg.Catalog.Api` | Controllers, middlewares, `Program.cs`, configuração. |

## Endpoints

| Método | Rota | Autorização | Descrição |
| --- | --- | --- | --- |
| GET | `/api/v1/jogos` | Anônimo | Lista jogos (paginação + filtro por gênero). |
| GET | `/api/v1/jogos/{id}` | Anônimo | Obtém um jogo por ID. |
| POST | `/api/v1/jogos` | `ApenasAdmin` | Cadastra um novo jogo. |
| PUT | `/api/v1/jogos/{id}` | `ApenasAdmin` | Atualiza um jogo. |
| DELETE | `/api/v1/jogos/{id}` | `ApenasAdmin` | Desativa (soft delete) um jogo. |
| GET | `/api/v1/biblioteca` | `UsuarioAutenticado` | Lista a biblioteca do usuário autenticado. |
| POST | `/api/v1/biblioteca` | `UsuarioAutenticado` | Inicia a compra de um jogo (publica `OrderPlacedEvent`). Retorna **202 Accepted**. |
| GET | `/health` | Anônimo | Health check. |

## Variáveis de ambiente

Todas sobrescrevíveis via variável de ambiente usando duplo underscore (`__`).

| Variável | Padrão | Descrição |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Ambiente de execução. |
| `ASPNETCORE_URLS` | `http://+:8080` | Endereço/porta de escuta (definido no Dockerfile). |
| `MongoDbSettings__ConnectionString` | `mongodb://localhost:27017` | String de conexão do MongoDB. |
| `MongoDbSettings__DatabaseName` | `catalogdb` | Nome do banco de dados. |
| `JwtSettings__SecretKey` | (override obrigatório) | Chave HMAC (≥256 bits). **Deve ser idêntica à da `users-api`.** |
| `JwtSettings__Issuer` | `FiapCloudGames` | Issuer válido do token. |
| `JwtSettings__Audience` | `FiapCloudGames` | Audience válida do token. |
| `RabbitMq__Host` | `localhost` | Host do RabbitMQ. |
| `RabbitMq__Username` | `guest` | Usuário do RabbitMQ. |
| `RabbitMq__Password` | `guest` | Senha do RabbitMQ. |

## Como executar

Pré-requisitos: .NET 10 SDK, MongoDB e RabbitMQ acessíveis.

```bash
# Build
dotnet build -c Release

# Testes
dotnet test -c Release

# Executar
dotnet run --project src/Fcg.Catalog.Api
```

Swagger fica disponível em `/swagger` no ambiente de Development.

### Docker

```bash
docker build -t catalog-api:local .
docker run -p 8080:8080 catalog-api:local
```

### Kubernetes

Manifestos em `k8s/` (`configmap`, `secret`, `deployment`, `service`):

```bash
kubectl apply -f k8s/
```

## Autenticação JWT

A catalog-api **valida** (não emite) tokens JWT. O token deve ser emitido pela `users-api`
usando exatamente a mesma `JwtSettings__SecretKey`, `Issuer` (`FiapCloudGames`) e `Audience`
(`FiapCloudGames`). O id do usuário é lido da claim `NameIdentifier`, e as roles definem as
políticas `ApenasAdmin` (role `Administrador`) e `UsuarioAutenticado`.

## Fluxo de compra orientado a eventos

1. `POST /api/v1/biblioteca` → `PurchaseService.IniciarCompraAsync` valida o jogo (existe, ativo, não possuído) e publica `OrderPlacedEvent`. Retorna `202 Accepted`.
2. `payments-api` processa o pagamento e publica `PaymentProcessedEvent`.
3. `PaymentProcessedConsumer` consome o evento: se `Approved`, adiciona o jogo à biblioteca (idempotente); se `Rejected`, apenas registra em log.
