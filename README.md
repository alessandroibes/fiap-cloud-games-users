# fiap-cloud-games-users

Cadastro, login JWT, administração de usuários e UserCreatedEvent com Outbox.

## Estado

G2 implementado e validado em 02/11/2026: .NET 8, PostgreSQL/EF Core, JWT com papéis, cadastro e administração, bootstrap explícito e publicação transacional de UserCreatedEvent por MassTransit/RabbitMQ. 28 testes aprovados; imagem Docker executada localmente. [Evidências e limites da validação](docs/quality/g2-validation.md).

Boas-vindas integradas com Notifications; Compose validado no G6 e Kubernetes Docker Desktop no G7.

## Documentação

- [ADRs aceitos](docs/architecture/decisions/README.md).
- [Decisões funcionais confirmadas](docs/architecture/functional-decisions.md).
- [Especificação de referência](docs/specification.md).
- [Contratos HTTP](docs/http-api.md), [UserCreatedEvent V1](docs/contracts/v1/user-created-v1.md) e [operação local](docs/operations.md).
- Contratos canônicos e roadmap: repositório fiap-cloud-games-infrastructure, docs/contracts/v1/ e docs/implementation/roadmap.md. Tipos C# locais, sem dependência de projetos irmãos.

## Repositório e solução completa

Código deste serviço: [GitHub](https://github.com/alessandroibes/fiap-cloud-games-users). Para executar os quatro serviços juntos, siga o [README da infraestrutura](https://github.com/alessandroibes/fiap-cloud-games-infrastructure), com clones irmãos e `scripts/Start-Local.ps1` ou `scripts/Start-Kubernetes.ps1`.

Os comandos abaixo descrevem o modo de desenvolvimento individual. Não iniciar o Compose individual ao mesmo tempo que a API correspondente do Compose central, pois compartilham portas e dependências. As suítes de teste usam ambientes exclusivos.

## Execução e configuração

Com a infraestrutura G1 em execução, na raiz deste repositório:

```powershell
./scripts/Initialize-Local.ps1
docker compose build users
docker compose run --rm users --migrate
docker compose run --rm users --bootstrap
docker compose up -d users
```

API em http://localhost:5081; saúde em /health/live e /health/ready. Configurações geradas em .env ignorado pelo Git. Não há migration automática no startup. Consulte [operação](docs/operations.md) para execução pelo SDK, configuração e uso seguro dos segredos.

```powershell
./scripts/Validate-G2.ps1
```

A validação utiliza PostgreSQL e RabbitMQ exclusivos dos testes e interrompe somente o broker de testes para verificar recuperação. Não executar suítes simultâneas nesse ambiente.

## Organização

- src/: Domain (regras), Application (casos de uso), Infrastructure (persistência, JWT, hash e Outbox), Api (HTTP) e Contracts (evento V1).
- tests/: domínio, contrato com assembly consumidor independente e integração HTTP/PostgreSQL/RabbitMQ.
- scripts/: preparação local, comandos explícitos e validação reproduzível.
- Dockerfile multi-stage e compose.yaml: imagem local sem usuário privilegiado, conectada à infraestrutura G1.

## Git e segurança

Clone independente fornecido pelo responsável; manter o origin existente. Commits separados, sem push ou publicação de imagens sem autorização. Documentação técnica é versionada. AGENTS.md, specs/, .codex/ e docs/course/ são locais e ignorados; segredos fora do Git.

## Kubernetes local

Manifestos e sequência de implantação em [k8s/README.md](k8s/README.md). G7 concluído; [evidências deste serviço](docs/quality/g7-validation.md) e registro transversal na infraestrutura.

## Resiliência — G8

Regressão concluída com 28 testes aprovados; [evidências e limites](docs/quality/g8-validation.md). Recuperação integrada no Kubernetes registrada na infraestrutura. Preparação documental da entrega G9 na infraestrutura; vídeo e relatório acadêmico dependem dos dados e da gravação do responsável.

## Variáveis de ambiente

Exemplo sem credenciais reais: [.env.example](.env.example). Gere a configuração com Initialize-Local; segredos efetivos ficam em .env ignorado. A infraestrutura central possui configuração própria e traduz suas variáveis para estas chaves da aplicação.

| Chave | Finalidade |
|---|---|
| ConnectionStrings__Users | Conexão PostgreSQL exclusiva deste serviço |
| USERS_DOCKER_CONNECTION | Conexão usada pelo Compose individual, com DNS postgres |
| RabbitMq__Host / RabbitMq__Port | Endereço e porta AMQP |
| RabbitMq__VirtualHost / RabbitMq__Username / RabbitMq__Password | Vhost e credencial externa do broker |
| ASPNETCORE_URLS | Endereço HTTP na execução local pelo SDK |
| Jwt__Key / Jwt__Issuer / Jwt__Audience | Assinatura e validação compatíveis entre Users e Catalog |
| Jwt__ExpirationMinutes | 60 minutos |
| AdminBootstrap__Name / AdminBootstrap__Email / AdminBootstrap__Password | Usados pelo comando explícito de bootstrap |

Portas e credenciais exclusivas de testes estão descritas em .env.example e [operação](docs/operations.md). Consulte [k8s/README.md](k8s/README.md) para ConfigMap/Secret, probes e Jobs. Nunca usar credenciais de outro banco nem executar migrations automaticamente no startup.
