# Testes E2E (Playwright)

Suíte de QA dos fluxos críticos do PedeLocal. Não substitui os testes de
integração .NET (`tests/PedeLocal.IntegrationTests`) — estes falam com um
**ambiente HTTP real** (API + painéis web), sem enfraquecer autenticação e
sem inventar endpoints.

Cliente e entregador vivem no app Flutter; os fluxos 1, 3, 4, 5 e 6 são
exercitados via **API** (mesmos contratos dos apps). Merchant e Super Admin
têm painel Next.js; os fluxos 2 e 7 também têm cobertura **UI** quando o
painel está no ar.

## Instalação

Na raiz do repositório:

```powershell
cd tests/e2e
npm ci
npx playwright install chromium
copy .env.example .env
```

Ajuste `.env` se a API não estiver em `http://localhost:8080`.

## Variáveis

| Variável | Default local | Uso |
|---|---|---|
| `PEDELOCAL_API_URL` | `http://localhost:8080` | API .NET |
| `PEDELOCAL_MERCHANT_URL` | `http://localhost:3000` | Painel do estabelecimento |
| `PEDELOCAL_ADMIN_URL` | `http://localhost:3001` | Painel Super Admin |
| `PEDELOCAL_ADMIN_EMAIL` | `admin@pedelocal.dev` | SUPER_ADMIN do seed de Development |
| `PEDELOCAL_ADMIN_PASSWORD` | `Senha123!` | senha do seed (nunca usar em produção) |
| `PEDELOCAL_TEST_PASSWORD` | `Senha123!` | senha dos usuários criados pelo teste |
| `PEDELOCAL_E2E_REALTIME` | `false` | `true` para o spec SignalR (exige CORS) |

Nenhum secret deve ser commitado. `.env` está no `.gitignore`.

O `dotnet run` da API usa a porta **5178** (`launchSettings.json`). Docker
expõe **8080**. Os painéis web defaultam 8080 — alinhe `PEDELOCAL_API_URL`.

## Subir o ambiente local

1. Postgres acessível (`docker compose up postgres` ou o Postgres local).
2. API em **Development** (o `DevelopmentDataSeeder` cria o SUPER_ADMIN):

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:RateLimiting__AuthPermitLimit = "1000"
$env:RateLimiting__GlobalPermitLimit = "10000"
# ConnectionStrings__Default e Jwt__Secret conforme .env da raiz
dotnet run --project src/PedeLocal.Api/PedeLocal.Api.csproj --urls http://localhost:8080
```

O rate limit padrão de auth é **5/60s por IP**. Sem aumentá-lo no **ambiente
de teste**, logins repetidos do E2E tomam 429. Isso não altera o default de
produção — só a variável do processo da API de QA.

3. Painéis (opcional, specs UI):

```powershell
cd apps/merchant-web
$env:PEDELOCAL_API_URL = "http://localhost:8080"
npm run dev

cd apps/admin-web
$env:PEDELOCAL_API_URL = "http://localhost:8080"
npm run dev
```

4. SignalR no browser exige origens em `Cors:AllowedOrigins`, por exemplo:

```powershell
$env:Cors__AllowedOrigins__0 = "http://localhost:3000"
$env:Cors__AllowedOrigins__1 = "http://localhost:3001"
```

Depois: `PEDELOCAL_E2E_REALTIME=true`.

## Executar

Todos os projetos (api + merchant + admin + realtime):

```powershell
cd tests/e2e
npx playwright test
```

Só API (não precisa dos Next.js):

```powershell
npx playwright test --project=api
```

Um arquivo / um teste:

```powershell
npx playwright test specs/pickup.spec.ts
npx playwright test -g "first-accept-wins"
```

UI:

```powershell
npx playwright test --project=merchant
npx playwright test --project=admin
```

Scripts npm: `npm test`, `npm run test:api`, `npm run test:merchant`,
`npm run test:admin`, `npm run test:realtime`.

## Relatórios

- HTML: `tests/e2e/playwright-report/` — `npx playwright show-report playwright-report`
- Trace / screenshot / vídeo: só em falha, em `tests/e2e/test-results/`
- Artefatos pesados **não** entram no Git

## Dados de teste

O worker do Playwright cria **uma** cidade/loja/cliente (não um mundo por teste),
para não esgotar o rate limit de `/api/auth/*`. Drivers extras e um segundo
cliente/loja só nascem nos fluxos que precisam (IDOR, concorrência).

1. Cidade única (`POST /api/admin/cities`) com SUPER_ADMIN (token em cache)
2. Estabelecimento + admin da loja (`POST /api/admin/establishments`)
3. `PATCH` operacional `Open` (o create nasce `ClosedManual`)
4. Taxa `Fixed` R$ 5, categoria, produto
5. CUSTOMER + endereço na mesma cidade
6. DRIVER(s) registrados, aprovados e online quando o fluxo precisa

O cliente de teste espera `Retry-After` em HTTP 429 nas rotas de auth; isso
não altera o limite do servidor.

Não usa dados manuais de produção. A senha do SUPER_ADMIN vem do seed de
Development (`admin@pedelocal.dev` / `Senha123!`) ou das variáveis acima.

Após o teste: estabelecimento `Inactive` e cidade desativada. Pedidos não
são apagados (a API não expõe delete de pedido).

## Limitações

- Flutter não é coberto pelo Playwright; customer/driver são API.
- Sem `google-services.json` / Firebase o push não entra neste E2E.
- Realtime UI depende de CORS do hub; o spec fica skipped até
  `PEDELOCAL_E2E_REALTIME=true`.
- Specs UI do merchant/admin são skipped se a URL não responder.
- A autoridade final do estado é sempre a API REST.
- Rate limit de auth precisa estar folgado no ambiente de QA.
- `POST /api/driver/deliveries/{id}/confirm` usa a policy `auth`; o cliente
  E2E espera 429 e tenta de novo, sem alterar o limite do servidor.
- `GET /api/admin/drivers` projetava `ValueTuple` no EF; isso gerava 500 e
  foi corrigido para uma projeção nomeada (o E2E cobre o endpoint).

## CI

O workflow `.github/workflows/ci.yml` tem o job `e2e`. Sobe Postgres + API
Development (seed) com rate limit alto e roda `--project=api`. Painéis web
não sobem nesse job (ficam para execução local / futura).
