# SPEC-P2-USERS — UsersAPI

**Estado:** Especificação de planejamento, alinhada aos ADRs aceitos; implementação ainda não iniciada.

## Responsabilidade
Cadastro, login JWT e autorização. Reaproveitar regras e testes compatíveis da Fase 1; não migrar o `AppDbContext` compartilhado. UsersAPI é dono de usuários e credenciais.

## Fluxos e aceite
1. Cadastro válido persiste usuário com hash seguro e publica logicamente `UserCreatedEvent` após sucesso da transação; erro não produz evento de usuário criado.
2. E-mail duplicado, senha inválida e dados inválidos mantêm regras da Fase 1, salvo incompatibilidade documentada.
3. Login com credenciais corretas emite JWT; credenciais incorretas não autenticam.
4. Autorização diferencia Usuário e Administrador, incluindo cenários 401/403.
5. Testes cobrem cadastro, login, claims, expiração, evento e falha temporária do broker.

## Integrações
Publica `docs/contracts/user-created-v1.md`. Configurações e segredos por ambiente. Não disponibilizar senhas, hashes nem tokens em eventos/logs.

## Decisões confirmadas e detalhamento técnico
Confirmado em 30/10/2026: sem importação legada; e-mail Trim + ToLowerInvariant com unicidade; preservar administração e bootstrap. Contrato V1 e Outbox aprovados. Migrations explícitas antes das APIs.

## Baseline obrigatória para implementação

As decisões dos ADR-P2-001 a ADR-P2-007 estão **Aceitas**. Implementar com PostgreSQL e Docker Desktop Kubernetes; contratos V1 conforme `docs/contracts/`.
