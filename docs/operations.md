# Operação local — UsersAPI

## Preparação e execução

Pré-requisitos: SDK .NET 8.0.414 ou patch compatível, Docker Desktop Linux e infraestrutura G1 em execução. Não há importação de dados da Fase 1.

Na raiz deste repositório:

```powershell
./scripts/Initialize-Local.ps1
./scripts/Invoke-Users.ps1 -Action migrate
./scripts/Invoke-Users.ps1 -Action bootstrap
./scripts/Invoke-Users.ps1 -Action run
```

Initialize-Local lê somente o .env local do repositório de infraestrutura para reutilizar a credencial exclusiva fcg_users e a conexão RabbitMQ. É possível informar outro caminho com -InfrastructureEnv. Gera chave JWT, senha do administrador e credenciais de testes sem imprimi-las. Um .env existente não é sobrescrito. O acoplamento é de preparação local; os projetos .NET não referenciam arquivos ou projetos irmãos.

Invoke-Users importa configurações apenas no processo e restaura o ambiente ao terminar. A API fica em http://127.0.0.1:5081. Se a política do PowerShell bloquear scripts, utilizar -ExecutionPolicy Bypass apenas no processo que executa o script, sem alterar a política global.

## Container local

```powershell
docker compose config --quiet
docker compose build users
docker compose run --rm users --migrate
docker compose run --rm users --bootstrap
docker compose up -d users
```

O Compose de Users conecta à rede externa fiap-cloud-games_default criada no G1; PostgreSQL e RabbitMQ continuam sob gestão da Infraestrutura. Imagem fiap-cloud-games-users:local, sem publicação. Dockerfile multi-stage, runtime sem SDK e execução com APP_UID não privilegiado. A porta é publicada somente no localhost. `docker compose stop users` para a API sem remover dados.

## Configuração

| Chave | Uso |
|---|---|
| ConnectionStrings__Users | PostgreSQL exclusivo fcg_users; conexão local via 55432 |
| USERS_DOCKER_CONNECTION | Mesmo banco/credencial; host postgres:5432 no Compose |
| Jwt__Key | Segredo simétrico externo, mínimo 32 bytes |
| Jwt__Issuer / Jwt__Audience | fiap-cloud-games-users / fiap-cloud-games; Catalog deverá validar os mesmos valores |
| Jwt__ExpirationMinutes | 60 minutos, compatível com Fase 1 |
| RabbitMq__Host / Port / VirtualHost | Endereço do broker e vhost fcg |
| RabbitMq__Username / Password | Credenciais externas; não usar guest |
| AdminBootstrap__Name / Email / Password | Usadas somente pelo comando explícito --bootstrap |
| ASPNETCORE_URLS | Endereço local do processo dotnet; o container usa porta 8080 |
| TEST_DB_PASSWORD / TEST_RABBIT_PASSWORD | Credenciais exclusivas dos containers de teste |
| TEST_DB_PORT / TEST_RABBIT_PORT | 55433 / 5673, configuráveis |

Nunca versionar .env, chaves ou tokens. `docker compose config` sem --quiet expande segredos: não usar sua saída como evidência compartilhada. Senhas de banco/broker já persistidas não mudam apenas por editar .env; rotação requer procedimento explícito.

## Migrations e bootstrap

A migration InitialUsersAndOutbox cria Users, índice único UX_Users_Email e tabelas técnicas MassTransit OutboxMessage, OutboxState e InboxState. InboxState integra o modelo da biblioteca; Users não possui consumidor de negócio neste gate. O startup normal não chama Migrate ou EnsureCreated. Readiness retorna 503 quando migrations estão pendentes.

`--migrate` aplica somente as migrations do UsersDbContext e termina sem iniciar a API ou o bus. `--bootstrap` exige schema pronto, valida dados e usa lock transacional PostgreSQL para serializar tentativas concorrentes. Se já existir administrador, não cria outro; se o e-mail pertencer a um usuário comum, falha sem promovê-lo silenciosamente. Cadastro e evento de bootstrap usam a mesma transação. Não há senha padrão embutida.

A ferramenta EF está fixada em .config/dotnet-tools.json. Para criar futuras migrations, executar dotnet tool restore e configurar ConnectionStrings__Users no processo; usar os projetos Infrastructure e Api como project/startup-project. Revisar o código e testar antes de aplicar. Não usar drop/reset de banco como procedimento de atualização.

## Outbox e broker

O cadastro adiciona usuário, chama IPublishEndpoint e confirma uma única unidade de persistência. UseBusOutbox captura o evento no mesmo UsersDbContext antes do SaveChanges. Erro de unicidade vira HTTP 409; uma transação abortada não deixa usuário ou evento pendente. A entrega é at-least-once: consumidores precisam deduplicar pelo EventId e pela identidade de negócio.

DeployPublishTopology e o binding durável de UserCreatedEvent para notifications-user-created-v1 preparam a fila antes da entrega da Outbox, inclusive antes do primeiro deploy de Notifications. O contrato/URN continua FiapCloudGames.Contracts.V1.UserCreatedEvent. Não há consumer de boas-vindas de produção neste repositório. Notifications deverá usar essa fila, conforme a matriz do G1.

Se o broker estiver indisponível, a API pode aceitar cadastros com o PostgreSQL disponível; os eventos permanecem na Outbox e são entregues após recuperação, inclusive depois de reiniciar a API. Readiness mede capacidade de persistir (banco + migrations), não disponibilidade do broker. Monitorar logs do MassTransit, filas e pendências de Outbox; não interpretar readiness como evento já entregue. Retentativas de handlers e filas de erro serão configuradas nos serviços consumidores; não há handler de negócio em Users.

Logs JSON omitem payloads de cadastro, tokens, hashes e detalhes SQL sensíveis. A role é um snapshot do JWT; alteração de papel exige novo login para refletir no token, e tokens anteriores expiram em até 60 minutos. TLS deve ser terminado na exposição de produção; HTTP local está restrito a loopback. Não há refresh token neste escopo.

## Testes

```powershell
./scripts/Validate-G2.ps1
```

O script sobe somente os containers fcg-users-g2-tests, restaura com lockfiles, compila e testa. Usa banco fcg_users_test e broker/vhost fcg_test; não limpa nem migra o banco do G1. A suíte para/reinicia o broker de testes e recria sua fila notifications-user-created-v1 para verificar recuperação e primeiro consumidor. Não executar duas suítes simultâneas nesse ambiente. Dados e volumes de teste permanecem; e-mails aleatórios e bootstrap idempotente permitem repetir a suíte.

Testes de domínio e contrato sem containers: `dotnet test FiapCloudGames.Users.sln --filter 'Category!=Integration'`. Resultados TRX ficam em TestResults ignorado. Fixtures são consumers de teste com assembly independente; não substituem NotificationsAPI. A pasta /k8s e seus manifests serão entregues no G7.

## Compatibilidade técnica

.NET 8, EF Core 9.0.10 e Npgsql EF 9.0.4, MassTransit 8.5.8. EF 9 é compatível com net8.0 e atende à dependência do MassTransit.EntityFrameworkCore escolhido no G1; versões diretas e transitivas estão fixadas nos lockfiles. Referências: [Outbox EF](https://masstransit.io/documentation/configuration/middleware/outbox), [implantação da topologia](https://masstransit.massient.com/configuration/topology), [pacote Npgsql EF](https://www.nuget.org/packages/Npgsql.EntityFrameworkCore.PostgreSQL/9.0.4).
