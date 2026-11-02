# SPEC-P2-USERS — UsersAPI

**Estado:** Escopo G2 implementado e validado em 02/11/2026; [evidências](quality/g2-validation.md). Integração com Notifications no G5/G6 e Kubernetes no G7.

## Responsabilidade
Cadastro, login JWT e autorização. Reaproveitar regras e testes compatíveis da Fase 1; não migrar o `AppDbContext` compartilhado. UsersAPI é dono de usuários e credenciais.

## Fluxos e aceite
1. Cadastro válido persiste usuário com hash seguro e publica logicamente `UserCreatedEvent` após sucesso da transação; erro não produz evento de usuário criado.
2. E-mail duplicado, senha inválida e dados inválidos mantêm regras da Fase 1, salvo incompatibilidade documentada.
3. Login com credenciais corretas emite JWT; credenciais incorretas não autenticam.
4. Autorização diferencia Usuário e Administrador, incluindo cenários 401/403.
5. Testes cobrem cadastro, login, claims, expiração, evento e falha temporária do broker.

## Integrações
Publica [UserCreatedEvent V1](contracts/v1/user-created-v1.md) por Outbox transacional. Configurações e segredos por ambiente. Não disponibilizar senhas, hashes nem tokens em eventos/logs. A fila durável notifications-user-created-v1 é preparada antes da entrega, permitindo implantação posterior de Notifications. Entrega at-least-once exige idempotência do consumidor.

## Decisões confirmadas e detalhamento técnico
Confirmado em 30/10/2026: sem importação legada; e-mail Trim + ToLowerInvariant com unicidade; preservar administração e bootstrap. Contrato V1 e Outbox aprovados. Migrations explícitas antes das APIs.

## Baseline obrigatória para implementação

As decisões dos ADR-P2-001 a ADR-P2-007 estão **Aceitas**. PostgreSQL implementado; implantação em Docker Desktop Kubernetes prevista para G7. Contratos V1 conforme `docs/contracts/v1/`.
