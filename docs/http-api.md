# Contratos HTTP — UsersAPI

JSON camelCase, GUIDs em strings; respostas nunca contêm PasswordHash. Erros usam application/problem+json. Nome/e-mail/senha são obrigatórios no cadastro; senha tem ao menos oito caracteres, letra, número e caractere especial.

| Método e rota | Entrada | Acesso | Sucesso / erros principais |
|---|---|---|---|
| POST /api/users | name, email, password | Público | 201 com id/name/email/role; 400 inválido; 409 e-mail duplicado |
| POST /api/auth/login | email, password | Público | 200 com accessToken; 400 e-mail malformado; 401 credenciais incorretas |
| GET /api/admin/users | — | Admin | 200 com lista de usuários; 401/403 |
| GET /api/admin/users/{id} | — | Admin | 200 usuário; 404 inexistente; 401/403 |
| PUT /api/admin/users/{id}/role | role: User ou Admin | Admin | 204; 400 papel inválido/número; 404 inexistente; 401/403 |
| GET /health/live | — | Público | 200 com status Healthy |
| GET /health/ready | — | Público | 200 banco/schema disponíveis; 503 indisponível ou migration pendente |

E-mail é normalizado com Trim + ToLowerInvariant no cadastro e login; índice PostgreSQL protege duplicidade sob concorrência. Cadastro público sempre cria User, independentemente de campos adicionais enviados. Alteração de papel aceita os nomes sem distinção de caixa, mas rejeita representações numéricas.

JWT HS256 contém sub (GUID), email e role; issuer, audience, assinatura, algoritmo e expiração são validados, sem tolerância adicional de relógio. Enviar Authorization: Bearer com o accessToken. Não colocar tokens na URL. O registro HTTP 201 confirma persistência de usuário e evento na Outbox; não confirma recebimento das boas-vindas.

## Evento

[UserCreatedEvent V1](contracts/v1/user-created-v1.md): EventId, OccurredAt UTC, UserId, Email normalizado e Name. Sem senha, hash ou JWT. Publicação transacional; entrega exatamente uma vez não é prometida. Consumidores deverão ser idempotentes.
