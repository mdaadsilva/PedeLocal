# Auditoria técnica e de segurança

Auditoria completa do backend na fase de estabilização (branch
`fix/security-hardening`), sem novas funcionalidades de produto — só
correção de vulnerabilidades, bugs e configurações inseguras. Baseline
antes de qualquer alteração: `dotnet test` **159/159** passando. Ao final:
**176/176** passando (17 testes novos, nenhum removido).

## Áreas auditadas

Autenticação/JWT (access/refresh token, rotação, revogação, reuso, claims,
roles), autorização/IDOR em todos os módulos (pedidos, entregas,
estabelecimentos, catálogo, financeiro, endereços, device tokens),
SignalR (grupos, isolamento), FCM/device tokens (ownership, logs,
payloads), validação de input em todos os endpoints com corpo de
requisição, concorrência (aceite de entrega, estoque, código de entrega),
rate limiting, CORS/headers/produção, secrets no repositório, dependências
.NET vulneráveis, banco (índices/constraints/cascades/migrations), logs/LGPD.

## Vulnerabilidades encontradas e corrigidas

### 1. Validação de input nunca era executada (Alta)

**O quê:** toda classe `*RequestValidator` (FluentValidation) do projeto —
mais de 15 arquivos, cobrindo pedidos, produtos, endereços, estabelecimentos,
device tokens, auth, etc. — estava registrada em DI
(`AddValidatorsFromAssembly`) mas **nunca era chamada por nada**. Não havia
`AddFluentValidationAutoValidation()`, nenhum action filter, nenhuma chamada
manual a `ValidateAsync`. Toda regra de validação (senha forte, quantidade
mínima, formato de e-mail/CPF, limites de tamanho de string, etc.) definida
nesses arquivos era código morto — qualquer requisição passava direto para
o service, validada apenas pelo que cada service checava manualmente.

**Risco:** quantidades negativas/absurdas, strings sem limite de tamanho,
senhas fracas, e-mails malformados, CPF fora do formato — tudo isso só era
barrado onde um service individualmente decidiu checar à mão. Vários campos
não tinham nenhuma checagem manual equivalente (ex.: tamanho de `Notes` do
pedido, quantidade máxima por item, número de itens por pedido).

**Correção:** `src/PedeLocal.Api/Filters/ValidationActionFilter.cs` — um
`IAsyncActionFilter` global (registrado em `AddControllers(...)` no
`Program.cs`) que resolve `IValidator<T>` do DI para cada argumento
vinculado da ação e lança `FluentValidation.ValidationException` se
inválido — já mapeado pelo `ApiExceptionHandler` existente para
`422 VALIDATION_ERROR` com erros por campo. Este é o padrão hoje
recomendado pela própria FluentValidation (o pacote de auto-validação do
ASP.NET Core foi descontinuado pelos mantenedores a partir da v9/v11 em
favor exatamente deste filtro manual) — não é um workaround.

Ativar a validação revelou uma regra **incorreta**: `CreateOrderRequestValidator`
exigia `AddressId` sempre presente (`NotEmpty()`), o que teria quebrado
todo pedido `Pickup` (que legitimamente envia `AddressId = null`) assim que
o filtro passasse a rodar de verdade. Essa regra foi removida — a
exigência condicional (obrigatório só para `Delivery`) já é corretamente
tratada no `OrderService.BuildQuoteAsync` (`ADDRESS_REQUIRED`), que é o
lugar certo para uma regra cross-field dependente de outro campo.

Também adicionados limites que não existiam em lugar nenhum: quantidade
máxima por item de pedido (100) e por ajuste de estoque manual
(1.000.000 — evita valores absurdos chegando à aritmética de estoque),
número máximo de itens distintos por pedido (100), tamanho máximo de
`Notes` (1000, já documentado mas nunca aplicado), `StockQuantity`
não-negativo na criação de produto.

**Testes:** `tests/PedeLocal.IntegrationTests/Security/InputValidationTests.cs`
(8 testes, HTTP real) — quantidade zero/negativa/absurda, excesso de itens,
notas excessivas, `EstablishmentId` vazio, senha fraca no registro, e um
teste de regressão confirmando que `Pickup` sem endereço **continua
funcionando** (a correção da regra, não uma quebra).

### 2. IDOR: leitura de cardápio/catálogo de qualquer estabelecimento (Alta)

**O quê:** `CatalogService.GetMenuCategoriesAsync`, `GetProductsAsync` e
`GetProductByIdAsync` — usados por `GET /api/merchant/establishments/{id}/menu-categories`
e `.../products[/​{productId}]` — nunca chamavam
`EstablishmentAccessGuard.EnsureAccessAsync`. Todo endpoint de escrita do
mesmo controller (criar/editar categoria, criar/editar produto,
disponibilidade) já checava corretamente; só os três de leitura ficaram de
fora.

**Risco:** qualquer usuário autenticado (CUSTOMER, DRIVER, ou staff de
**outro** estabelecimento) podia ler o cardápio completo de qualquer
estabelecimento pelo `establishmentId` na URL — incluindo produtos
indisponíveis (nunca expostos no endpoint público) e de estabelecimentos
suspensos/invisíveis (que o endpoint público `GET /api/establishments/{id}/products`
corretamente esconde). Exposição de dados internos de um concorrente
(preços, estratégia promocional, estoque via `StockQuantity`), sem
nenhuma verificação de vínculo.

**Correção:** adicionado `await accessGuard.EnsureAccessAsync(establishmentId, ct);`
nos três métodos.

**Testes:** `tests/PedeLocal.UnitTests/Catalog/CatalogServiceTests.cs` —
não-staff e staff de **outro** estabelecimento recebem `ForbiddenException`
para os três métodos; staff do próprio estabelecimento continua
funcionando (regressão).

### 3. Reuso de refresh token não derrubava a sessão viva (Média/Alta)

**O quê:** `AuthService.RefreshAsync` já rejeitava um refresh token já
revogado (rotação) com `401 INVALID_REFRESH_TOKEN`, mas não fazia nada
além disso — a documentação (`docs/AUTH_FLOW.md`) já dizia "é também o
sinal de que o token pode ter vazado", mas nenhum código de fato tratava
esse sinal. `RefreshToken.ReplacedByTokenHash` existia no schema
justamente para permitir andar a cadeia de rotação, mas nunca era lido.
Também faltava checar `existing.User.IsActive` — um usuário desativado
(hoje só via `DELETE /api/me/account`, que já revoga sessões, mas nada
impede um caminho futuro de desativação que esqueça de fazer o mesmo)
continuaria conseguindo renovar tokens indefinidamente com um refresh
token ainda não revogado.

**Risco:** cenário clássico de roubo de refresh token — se um atacante e o
usuário legítimo acabam com cópias do mesmo token (vazamento, log
comprometido, etc.) e ambos tentam usá-lo, apenas quem perde a corrida
recebe erro; a sessão de quem venceu (o descendente vivo da rotação)
seguia válida indefinidamente, sem nenhum sinal para o usuário legítimo de
que algo está errado nem qualquer ação automática de contenção.

**Correção:** `AuthService.RefreshAsync` agora, ao detectar reuso de um
token já revogado, percorre a cadeia via `ReplacedByTokenHash` e revoga
todo descendente ainda ativo (`RevokeDescendantChainAsync`) — mata a
sessão inteira, não só nega a chamada atual — e registra um `AuditLog`
(`REFRESH_TOKEN_REUSE_DETECTED`, com `ActorUserId`/`Ip`, nunca o token em
si) antes de responder `401`. Adicionada também a checagem de
`User.IsActive`.

**Testes:** `tests/PedeLocal.UnitTests/Identity/AuthServiceTests.cs` —
reuso do token original após rotação legítima derruba o descendente vivo
(uma segunda tentativa de refresh com o token novo também falha) e gera
`AuditLog`; usuário desativado não consegue renovar mesmo com token
válido/não expirado.

### 4. Estoque: race condition entre pedidos concorrentes (Média)

**O quê:** `OrderService.DeductStockAsync`/`RestoreStockAsync` e
`InventoryService.AdjustStockAsync` liam `Product.StockQuantity`,
calculavam o novo valor e salvavam — sem nenhum token de concorrência.
`Delivery` já tinha esse problema resolvido (`Delivery.Version`, decisão
#07 em `docs/DECISIONS.md`) mas `Product` nunca ganhou o mesmo tratamento.

**Risco:** dois pedidos diferentes do mesmo produto (`StockMode.Controlled`)
aceitos por requisições concorrentes (dois atendentes, ou double-click)
podiam gerar *lost update* — a segunda escrita sobrescreve a primeira sem
saber dela, permitindo vender mais do que o estoque real (oversell) ou
descartar silenciosamente um ajuste manual concorrente.

**Correção:** `Product.Version` (int, mesmo padrão de `Delivery.Version`),
incrementado em toda mutação de `StockQuantity`
(`DeductStockAsync`/`RestoreStockAsync`/`InventoryService.AdjustStockAsync`),
marcado `.IsConcurrencyToken()`. `OrderService` ganhou
`SaveChangesGuardingStockConcurrencyAsync` (usado por `AcceptAsync` via
`TransitionAsync` e por `CancelAsync`) e `InventoryService.AdjustStockAsync`
um try/catch equivalente — ambos traduzem `DbUpdateConcurrencyException`
em `409 PRODUCT_STOCK_CONFLICT` (retryable), nunca um 500 genérico.
Migration `AddProductStockConcurrencyToken` — só adiciona a coluna
(`default 0`), sem perda de dado; aplicada e conferida contra Postgres
real (`docker compose`).

**Testes:** `tests/PedeLocal.UnitTests/Orders/OrderServiceTests.cs` —
`Accept_TwoOrdersForSameProduct_Concurrently_SecondGetsStockConflict_NeverOversells`,
mesmo padrão de dois `DbContext` independentes já usado pelo teste de
concorrência de `Delivery`.

### 5. Dependência vulnerável: `System.Security.Cryptography.Xml` (Alta, conhecida e documentada — decisão #18)

**O quê:** o pacote não é usado em nenhuma linha de código do projeto — é
uma dependência **transitiva apenas em tempo de build**, puxada por
`Microsoft.Build.Tasks.Core` (parte da cadeia do
`Microsoft.EntityFrameworkCore.Design`, `PrivateAssets=all`, nunca vai para
o artefato publicado/imagem Docker). A referência direta existente fixava
a versão em `10.0.0`, que por sua vez também tinha os mesmos CVEs — o
"pin" não estava, de fato, pinando para uma versão corrigida.

**Correção:** atualizado para `10.0.11` (última versão estável sem
advisory conhecido no momento desta auditoria). `dotnet list package
--vulnerable --include-transitive` agora reporta **zero pacotes
vulneráveis** em todos os 7 projetos da solução (antes: 8 CVEs de
severidade alta em 3 projetos).

### 6. Headers de segurança e HSTS ausentes (Baixa/Média)

**O quê:** `Program.cs` já tinha `UseHttpsRedirection()`, mas nunca
`UseHsts()`, e nenhum header de resposta como `X-Content-Type-Options`/
`X-Frame-Options`/`Referrer-Policy`. Relevante em especial para
`/uploads/*` (arquivos enviados por usuários, servidos como estáticos) —
sem `nosniff`, um browser antigo/mal configurado poderia, em teoria,
tentar sniffar conteúdo de um arquivo além do `Content-Type` declarado.

**Correção:** `app.UseHsts()` fora de Development; middleware leve
adicionando `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`,
`Referrer-Policy: no-referrer` em toda resposta. Nenhum dos dois depende
de confiar em header vindo do cliente (ver risco residual #1 abaixo sobre
`X-Forwarded-*`).

### 7. `NoOpPushSender` logava o token FCM completo (Baixa — regressão da sessão anterior)

**O quê:** `logger.LogInformation("... to {Token}: ...", deviceToken, ...)`
— violava diretamente o requisito "tokens não aparecem em logs" desta
auditoria (e da anterior, que introduziu o no-op sender sem notar isso).

**Correção:** loga só o comprimento do token
(`token[len={TokenLength}]`), nunca o valor.

### 8. Rate limiting ausente em `PUT/DELETE /api/me/device-tokens` (Baixa)

**O quê:** todo outro endpoint sensível (`login`, `refresh`,
`forgot-password`, `reset-password`, confirmação de entrega, upload) já
está sob a política `auth` (mais restritiva); registro/remoção de device
token, que é autenticado mas ainda abusável (fricção de custo/quota do
Firebase, ou tentativa de adivinhar um token de outro usuário — embora
tokens FCM sejam strings longas e opacas, não triviais de adivinhar),
ficou de fora.

**Correção:** `[EnableRateLimiting("auth")]` nos dois endpoints.

**Testes:** `tests/PedeLocal.IntegrationTests/Security/RateLimitingTests.cs`
(novo) — login e registro de device token excedendo o limite recebem
`429`. Usa uma factory dedicada com limite baixo
(`RateLimitedWebApplicationFactory`) em vez da factory compartilhada, que
propositalmente usa um limite alto para não atrapalhar os outros testes —
não havia **nenhum** teste de rate limiting antes desta auditoria.

## Áreas revisadas sem achado (já corretas)

- **SignalR**: hub `[Authorize]`, grupos decididos só pelo servidor a
  partir do JWT/dados no banco (`docs/REALTIME_EVENTS.md`), sem método
  `Subscribe` para o cliente escolher grupo. Já coberto por
  `HubAuthorizationTests` (isolamento por cliente/estabelecimento/cidade,
  rejeição sem autenticação) de uma auditoria anterior — revalidado, sem
  regressão.
- **Ownership em pedidos/endereços/entregas/financeiro**: `OrderService`,
  `AddressService`, `DeliveryService`, `FinanceService` checam
  ownership/vínculo em todo path de leitura e escrita relevante — revisão
  sistemática de todo `SingleOrDefaultAsync`/`FirstOrDefaultAsync` do
  projeto contra o filtro de ownership correspondente.
- **Preços/total/taxa**: `CreateOrderRequest` nunca aceita preço, subtotal,
  taxa de entrega ou total do cliente — tudo calculado no servidor a
  partir de `Product.Price`/`ProductOption.PriceDelta`/
  `DeliveryPricingSettings` (`OrderService.BuildQuoteAsync`). `POST
  /api/orders/preview` roda exatamente o mesmo código de cálculo do `POST
  /api/orders`, então não pode divergir.
- **First-accept-wins de entrega**: `Delivery.Version` (otimista) mais
  pré-checagem — já testado (`DeliveryServiceTests`,
  `DeliveryConcurrencyTests` via HTTP real concorrente).
- **Código de confirmação de entrega**: hash comparado (nunca texto
  puro), 5 tentativas, expiração 24h, uso único — já testado. Efeito
  colateral positivo desta auditoria: como o formato (`^\d{6}$`) agora é
  validado antes de chegar ao service (achado #1), um código mal formado
  não consome mais uma das 5 tentativas — só tentativas de 6 dígitos
  contam.
- **CORS**: falha fechado por padrão (`Cors:AllowedOrigins` vazio → nenhum
  header CORS adicionado, nenhuma origem permitida) — sem
  `AllowAnyOrigin`/`AllowCredentials` inseguros.
- **OpenAPI/Scalar**: só mapeado em Development/Staging.
- **Erros/stack traces**: `ApiExceptionHandler` nunca inclui stack trace
  ou detalhe interno na resposta — 500 genérico ("Ocorreu um erro
  inesperado") em qualquer ambiente, exceção completa só no log do
  servidor.
- **Upload de imagem**: nome gerado no servidor (sem path traversal),
  validação por magic bytes (não confia em extensão/`Content-Type`
  declarado), limite de tamanho.
- **Secrets no repositório**: busca por `password`/`secret`/`apikey`/
  `private_key`/connection string em todo o repo — únicos hits são
  `.env`/`.env.example` com placeholders (`change-me`) e
  `appsettings.json` com `Secret: ""`; `.env` real está no `.gitignore` e
  **nunca foi commitado** (`git ls-files` confirma). Nenhum
  `service-account.json`/`google-services.json`/`GoogleService-Info.plist`
  em lugar nenhum do repositório.
- **Dockerfile**: roda como usuário não-root (`appuser`, uid 5678), sem
  segredo hardcoded.
- **Cascades no banco**: todo `OnDelete(Cascade)` é entre uma entidade e
  seus filhos diretos de agregado (ex. `Product` → `ProductOptionGroup`,
  `User` → `RefreshToken`/`DeviceToken`); não existe endpoint de exclusão
  física de `City`/`Establishment`/`Product` no sistema (desativação é
  sempre por flag de status, nunca `DELETE`), então não há cascade
  perigoso alcançável por nenhum fluxo real.

## Riscos residuais (antes de produção)

1. **`X-Forwarded-For`/`X-Forwarded-Proto` não configurados.** Se este
   backend for colocado atrás de um proxy reverso/load balancer real em
   produção (o `docker-compose.yml` atual expõe a API diretamente, sem
   proxy na frente), `HttpContext.Connection.RemoteIpAddress` vai refletir
   o IP do proxy, não o do cliente — isso degrada (não quebra) o rate
   limiting (todos os clientes atrás do proxy compartilhariam o mesmo
   "IP") e a auditoria (`RefreshToken.CreatedByIp`, `AuditLog.Ip`).
   Deliberadamente **não adicionado** `UseForwardedHeaders()` nesta
   auditoria: configurado sem `KnownProxies`/`KnownNetworks` corretos
   (que dependem da infraestrutura real de deploy, ainda não definida —
   ver `docs/FRONTEND_GUIDE.md`), esse middleware passaria a **confiar
   cegamente** em um header `X-Forwarded-For` enviado pelo próprio
   cliente, permitindo spoofing de IP e bypass do rate limiting — pior do
   que o estado atual. **Ação antes do deploy**: configurar
   `ForwardedHeadersOptions.KnownProxies`/`KnownNetworks` apontando para o
   IP/rede real do proxy/LB escolhido pela infra.
2. **`forgot-password` ainda não envia e-mail/push** — o token é gerado e
   persistido, mas a entrega ao usuário está pendente de um provedor de
   e-mail (já documentado em `docs/DECISIONS.md`/`docs/AUTH_FLOW.md`,
   fora do escopo desta auditoria de segurança). Não é uma vulnerabilidade
   em si (o endpoint já não faz account enumeration), mas é um fluxo
   incompleto que precisa existir antes de produção real.
3. **Push real (Firebase) segue opcional/no-op por padrão** —
   comportamento correto e intencional (`docs/PUSH_NOTIFICATIONS.md`), só
   listado aqui como lembrete de que produção precisa de
   `Firebase:Enabled=true` + credencial real via variável de
   ambiente/secret manager para push de verdade funcionar.
4. **Sem WAF/proteção de borda dedicada** (fora do escopo de código desta
   auditoria) — o rate limiting é por IP e por instância do processo
   (`FixedWindowLimiter` em memória); múltiplas réplicas do container
   atrás de um load balancer, sem um limitador compartilhado (ex. Redis),
   dividem o limite entre si em vez de aplicá-lo globalmente. Considerar
   se a escala de produção justifica um limitador distribuído.
5. **Sem monitoramento/alerta automático sobre `REFRESH_TOKEN_REUSE_DETECTED`**
   — o evento agora é auditado (achado #3), mas nada hoje observa a
   tabela `audit_logs` e dispara um alerta. Vale conectar isso a um canal
   de observabilidade antes de produção.

## Recomendações antes do deploy

- Configurar `ForwardedHeadersOptions` assim que a topologia real de
  proxy/LB de produção estiver definida (risco residual #1).
- Rodar `dotnet list package --vulnerable` periodicamente (ex. CI
  agendado) — a lista de CVEs conhecidos muda com o tempo.
- Definir uma política de rotação para `Jwt:Secret` e revisão do valor de
  produção (nunca reaproveitar o de `.env.example`).
- Decidir um provedor de e-mail para completar o fluxo de
  `forgot-password` (risco residual #2).
- Avaliar rate limiting distribuído se o deploy de produção usar múltiplas
  réplicas (risco residual #4).
