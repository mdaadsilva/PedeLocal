# Fluxo de autenticação

## Visão geral

JWT de curta duração (access token) + refresh token rotativo, opaco, armazenado
apenas como hash no banco. Senhas usam BCrypt (fator de custo 12).

```text
POST /api/auth/register/customer
POST /api/auth/register/driver
POST /api/auth/login
POST /api/auth/refresh
POST /api/auth/logout
POST /api/auth/forgot-password
POST /api/auth/reset-password
```

Todos os endpoints acima estão sob rate limit mais restritivo (política
`auth`, padrão: 5 requisições/60s por IP — configurável via
`RateLimiting:AuthPermitLimit`/`AuthWindowSeconds`).

## Cadastro

### Cliente

```http
POST /api/auth/register/customer
{
  "name": "Maria Silva",
  "email": "maria@example.com",
  "phone": "14999990000",
  "password": "Senha123"
}
```

Senha exige: mínimo 8 caracteres, 1 maiúscula, 1 minúscula, 1 número.
Cidade **não** é obrigatória neste momento — o cliente escolhe depois.

### Entregador

```http
POST /api/auth/register/driver
{
  "name": "João", "cpf": "12345678901", "phone": "14999990001",
  "email": "joao@example.com", "password": "Senha123",
  "cityId": "<guid da cidade>",
  "vehicleType": "Motorcycle",   // Bike | Motorcycle | Car
  "pixKeyType": "Cpf",           // Cpf | Email | Phone | Random
  "pixKey": "12345678901"
}
```

O entregador nasce com `approvalStatus = "PendingApproval"` e **não pode
trabalhar** até o Super Admin aprovar (`POST /api/admin/drivers/{id}/approve`).

## Login

```http
POST /api/auth/login
{ "email": "maria@example.com", "password": "Senha123" }

200 OK
{
  "accessToken": "eyJ...",
  "refreshToken": "base64-opaco...",
  "accessTokenExpiresAtUtc": "2026-09-07T15:30:00Z"
}
```

- `accessToken`: JWT, claims incluem `sub` (userId), `email`, `name`, e uma
  claim `role` por papel do usuário. Validade padrão: 15 minutos
  (`Jwt:AccessTokenMinutes`).
- `refreshToken`: string opaca, validade padrão 30 dias
  (`Jwt:RefreshTokenDays`). Guarde com segurança (secure storage no
  mobile/web) — nunca em `localStorage` puro se puder evitar.

Envie o access token em toda chamada autenticada:

```text
Authorization: Bearer <accessToken>
```

## Refresh

```http
POST /api/auth/refresh
{ "refreshToken": "..." }
```

**Rotativo**: cada chamada de refresh revoga o token antigo e devolve um par
novo. Reusar um refresh token já trocado devolve `401 INVALID_REFRESH_TOKEN`
— trate isso como sessão expirada e mande o usuário para o login (é também o
sinal de que o token pode ter vazado).

## Logout

```http
POST /api/auth/logout
{ "refreshToken": "..." }
```

Revoga apenas aquele refresh token (aquela sessão/dispositivo). Idempotente.

## Esqueci minha senha

```http
POST /api/auth/forgot-password
{ "email": "maria@example.com" }
→ 204 No Content (sempre, exista ou não o e-mail — evita enumeração de contas)
```

Um token de uso único (validade 30 min) é gerado, mas **ainda não é
enviado ao usuário** — o módulo de notificações (push/e-mail) para esse fluxo
específico é o próximo passo pendente do lado do backend (ver
`docs/DECISIONS.md`). Até lá, o Super Admin pode gerar o link manualmente via
banco em ambiente de desenvolvimento.

```http
POST /api/auth/reset-password
{ "token": "...", "newPassword": "NovaSenha123" }
```

Redefinir a senha revoga **todas** as sessões ativas do usuário.

## Papéis (roles)

```text
SUPER_ADMIN
CUSTOMER
DRIVER
ESTABLISHMENT_ADMIN
ESTABLISHMENT_MANAGER   (papel existe, ainda sem fluxo de atribuição dedicado)
ATTENDANT               (idem)
STOCK_OPERATOR          (idem)
```

Um usuário pode ter mais de um papel (tabela `user_roles`), mas os fluxos de
cadastro atuais atribuem exatamente um papel por vez. O papel
`ESTABLISHMENT_ADMIN` é atribuído automaticamente ao criar um estabelecimento
(`POST /api/admin/establishments`), junto com o novo usuário administrador.

## Exclusão / dados da conta (LGPD)

```text
GET    /api/me/profile
PUT    /api/me/profile
GET    /api/me/data          -- export básico (perfil + contagem de endereços/pedidos)
DELETE /api/me/account        -- anonimiza + desativa a conta, revoga sessões
```

`DELETE /api/me/account` **não apaga** pedidos, auditoria ou dados
financeiros (retenção legítima) — apenas anonimiza os dados pessoais do
usuário e desativa o login.
