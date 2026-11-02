# Evidências do G2 — UsersAPI

Validação local em 02/11/2026, Windows/PowerShell, SDK .NET 8 e Docker Desktop com containers Linux. Escopo restrito a Users; Fase 1 preservada. Os comandos abaixo foram executados; esta evidência não representa pipeline remoto.

## Resultado

| Verificação | Resultado observado |
|---|---|
| Restore com lockfiles e build da solução | Sucesso; build sem warnings ou erros |
| dotnet test FiapCloudGames.Users.sln --no-build --logger 'trx;LogFileName=g2-tests.trx' | 28 executados, 28 aprovados, zero falhas; execução final entre 10:30:09 e 10:30:53, horário de Brasília |
| dotnet format whitespace FiapCloudGames.Users.sln --verify-no-changes --no-restore | Código de saída zero |
| docker compose build users | Imagem local construída; bases SDK/runtime fixadas por digest |
| Comando explícito --migrate no container | Schema Users/Outbox aplicado ao banco fcg_users; uma entrada em __EFMigrationsHistory |
| Comando explícito --bootstrap | Um administrador; repetição retornou Administrador criado: False |
| Container Users | Em execução com UID 1654; porta 5081 publicada no localhost |
| GET /health/live e /health/ready | HTTP 200 |
| POST /api/auth/login e GET /api/admin/users | Login válido e consulta autorizada; um usuário retornado, sem expor token na evidência |
| Entrega do evento de bootstrap | OutboxMessage com zero registros; notifications-user-created-v1 com uma mensagem e zero consumidores |

Imagem validada: `sha256:06d1acbd1e7e83f3043a42c7ddb5b717ded1b7f02cb28baf91cc1c2e7d399137`. Digest identifica o build local, não uma publicação em registry. Contagens refletem o instante da validação; podem mudar durante uso posterior.

## Cobertura relevante

- Domínio: dados obrigatórios, política de senha, normalização e papéis.
- Contrato: campos/tipos V1, serialização e identidade MassTransit, consumidor com assembly independente.
- Integração real: cadastro, hash, claims/expiração JWT, erros de autenticação, autorização 401/403, administração e bootstrap idempotente.
- Oito cadastros concorrentes de variantes do mesmo e-mail produzem um sucesso e sete conflitos, protegidos por índice único PostgreSQL.
- Rollback remove tanto o usuário quanto o evento pendente, sem entrega ao consumidor.
- Broker de testes indisponível: cadastro persiste, API reinicia, readiness permanece disponível e evento é entregue após recuperação.
- Fila recriada antes do produtor: mensagem permanece disponível para consumidor iniciado posteriormente. Esse teste detectou uma falha na implantação da topologia, corrigida com DeployPublishTopology e binding na exchange do evento antes da execução final aprovada.
- Schema vazio: startup normal não aplica migrations; liveness 200 e readiness 503.

## Reprodução e limites

Executar `scripts/Validate-G2.ps1` após `scripts/Initialize-Local.ps1`, conforme [operação](../operations.md). Os testes usam projeto Compose fcg-users-g2-tests, banco e broker próprios, sem limpar dados de G1. O TRX é gerado em TestResults do projeto de testes e permanece ignorado pelo Git. Não versionar .env ou capturas contendo tokens/credenciais.

O consumidor utilizado na suíte é de teste. Boas-vindas reais no console de Notifications e deduplicação de notificações serão verificadas no G5/G6. Kubernetes não foi implantado neste gate; /k8s e validações do cluster pertencem ao G7. Retentativas de handlers e filas de erro serão verificadas nos serviços consumidores. A entrega é at-least-once, sem promessa de emissão exatamente uma vez.
