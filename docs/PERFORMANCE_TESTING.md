# Testes de performance (k6)

Suíte de carga/performance do PedeLocal, complementar aos testes unitários, de
integração, de segurança e E2E (Playwright) já existentes — não os substitui.
Objetivo: identificar gargalos **antes** do deploy em staging, de forma
reproduzível e sem enfraquecer rate limits ou segurança para viabilizar os
testes.

Fica em `tests/performance/`, usando [k6](https://k6.io) — mesma escolha de
stack de qualquer ambiente de CI moderno para carga HTTP/WebSocket, com
thresholds declarativos e saída estruturada.

## Instalação do k6

```powershell
choco install k6         # Windows (usado para validar esta suíte)
```

Alternativas: `winget install k6.k6`, `scoop install k6`,
[instruções oficiais](https://grafana.com/docs/k6/latest/set-up/install-k6/)
para Linux/macOS, ou a imagem `grafana/k6` do Docker Hub (útil em CI sem
instalar nada no runner: `docker run --rm -i grafana/k6 run - < script.js`,
apontando `PEDELOCAL_API_URL` para um host acessível do container).

Verifique: `k6 version`.

## Estrutura

```text
tests/performance/
  lib/
    config.js      — variáveis de ambiente, defaults, helpers (uniqueSuffix, randomId, wsUrl)
    http.js        — wrappers de request com tags por endpoint, tratamento de 429, checks
    profiles.js    — executores smoke/load/stress (ramping-vus e shared-iterations)
    setup.js       — bootstrap via API real (cidade/estabelecimento/produto/cliente/motoristas),
                      mesmo papel que tests/e2e/src/fixtures/world.ts cumpre para o Playwright
  scenarios/
    01-login.js                      — Cenário 1: autenticação simultânea
    02-catalog-browse.js             — Cenário 2: cidade → estabelecimentos → produtos → detalhes
    03-order-preview.js              — Cenário 3: preview de pedido sob carga
    04-order-creation.js             — Cenário 4: criação de pedidos concorrente + estoque
    05-driver-first-accept-wins.js   — Cenário 5: corrida de aceite de entrega (crítico)
    06-order-status-transitions.js   — Cenário 6: transições de status concorrentes
    07-signalr.js                    — Cenário 7: conexões SignalR simultâneas
  results/        — saída de --summary-export (gitignored, exceto .gitkeep)
  .env.example
  run-smoke.sh / run-smoke.ps1   — roda todos os cenários em PROFILE=smoke
```

Cada cenário é um módulo k6 independente (`k6 run scenarios/XX-nome.js`), sem
framework de execução próprio — só o próprio k6.

## Configuração

k6 não lê arquivos `.env` nativamente — exporte as variáveis antes de rodar:

```bash
# bash
set -a; source tests/performance/.env; set +a
```

```powershell
# PowerShell
Get-Content tests/performance/.env | ForEach-Object {
    if ($_ -match '^([^#=]+)=(.*)$') { [Environment]::SetEnvironmentVariable($Matches[1], $Matches[2]) }
}
```

| Variável | Default | Uso |
|---|---|---|
| `PEDELOCAL_API_URL` | `http://localhost:8080` | API .NET alvo |
| `PEDELOCAL_ADMIN_EMAIL` / `_PASSWORD` | seed de Development | SUPER_ADMIN usado por `lib/setup.js` para criar cidade/estabelecimento isolados por execução |
| `PEDELOCAL_CUSTOMER_EMAIL` / `_PASSWORD` | (vazio → auto-registra) | reaproveitar uma conta CUSTOMER já existente em vez de registrar uma nova a cada execução (útil em staging) |
| `PEDELOCAL_DRIVER_EMAIL` / `_PASSWORD` | (reservado) | o cenário 05 sempre registra seu próprio pool de motoristas (precisa de vários, com aprovação/online controlados) — variáveis aqui só para uso futuro |
| `PEDELOCAL_TEST_PASSWORD` | `Senha123!` | senha das contas que a própria suíte registra |
| `PROFILE` | `smoke` | `smoke` \| `load` \| `stress` |
| `STRESS_MAX_VUS`, `STRESS_STEPS` | `80`, `4` | perfil stress — teto de VUs e nº de degraus de rampa |
| `STRESS_RACE_VUS`, `STRESS_RACE_ITERATIONS` | `5`, `30` | perfil stress do cenário 05 (é `shared-iterations`, não `ramping-vus`) |
| `LOGIN_POOL_SIZE` | `5` | nº de contas no pool de login do cenário 01 |
| `RACE_SIZE` | `3` | motoristas por trial no cenário 05 |
| `STATUS_CONCURRENT_CALLS` | `4` | chamadas simultâneas por trial no cenário 06 |

Nenhum secret é versionado — `.env.example` só documenta nomes/defaults
não-sensíveis (a senha do seed de Development, `Senha123!`, já é pública nos
outros docs deste repositório).

### Nunca contra produção sem autorização explícita

Esta suíte **nunca** deve rodar contra uma URL de produção sem autorização
explícita e coordenação com quem opera aquele ambiente — mesmo o perfil
`smoke` cria dados reais (cidade/estabelecimento/pedidos) e o perfil `stress`
gera carga deliberadamente crescente. `PEDELOCAL_API_URL` aponta para
`localhost` por padrão exatamente para isso: staging/produção exigem decisão
e configuração manual, nunca acontecem "por acidente" rodando um comando sem
variáveis.

## Rate limiting — como esta suíte respeita (não contorna) o dele da API

`POST /api/auth/*`, `POST /api/driver/deliveries/{id}/confirm`,
`POST /api/uploads/image` e (desde a auditoria de segurança) `PUT`/`DELETE
/api/me/device-tokens` estão sob a policy `auth` do rate limiter
(`Program.cs`) — default **5 requisições/60s por IP**. Esta suíte nunca tenta
contornar isso:

- `lib/http.js` marca toda resposta `429` num contador dedicado
  (`pedelocal_rate_limited_total`), nunca como falha de `http_req_failed` —
  ver `http.setResponseCallback(http.expectedStatuses(...))` no topo de cada
  cenário, sempre incluindo `429` na lista de status "esperados".
- Os perfis `smoke` desta suíte usam poucos VUs (5) justamente para caber
  dentro do limite default sem precisar de nenhuma configuração especial —
  é assim que `run-smoke.sh`/`run-smoke.ps1` conseguem rodar em CI contra uma
  API com o rate limit default.
- Para os perfis `load`/`stress` do **cenário 1** (e de qualquer cenário que
  faça login/registro repetidamente em volume), configure
  `RateLimiting__AuthPermitLimit`/`RateLimiting__AuthWindowSeconds` no
  **ambiente de teste** (variável de ambiente do processo da API sendo
  testada) para um valor compatível com a carga desejada — exatamente a
  mesma orientação já documentada para o E2E (`docs/E2E_TESTING.md`). Isso
  configura o ambiente de QA/staging que está sendo testado, nunca o default
  de produção no código.

## Cenários

### 1 — Login (`01-login.js`)

Autenticação simultânea: pool pequeno de contas **válidas** (reaproveitadas
ou auto-registradas em `setup()`), carga gradual (ramping-vus), sem brute
force (nunca testa senha errada). Mede p50/p95/p99/throughput/erro de
`POST /api/auth/login`.

### 2 — Catálogo (`02-catalog-browse.js`)

`GET /api/cities` → `GET /api/establishments?cityId=` →
`GET /api/establishments/{id}` → `GET /api/establishments/{id}/products`.
Provavelmente o fluxo de maior volume real. Um único token de cliente
(obtido uma vez em `setup()`) é reaproveitado por todos os VUs — leitura não
muta estado, então não há razão para logar de novo a cada VU.

### 3 — Preview de pedido (`03-order-preview.js`)

`POST /api/orders/preview` sob carga concorrente. Preço/taxa fixos no
`setup()` tornam o resultado **determinístico** — qualquer divergência sob
carga (`pedelocal_preview_inconsistent_total > 0`) é um bug real, não ruído
estatístico.

### 4 — Criação de pedidos (`04-order-creation.js`)

Pedidos concorrentes contra um produto `StockMode=Controlled` com estoque
inicial pequeno e conhecido (calculado a partir do teto de VUs do perfil
ativo — deliberadamente apertado, para garantir contenção real, não só
"todo mundo cabe"). Cada trial também reenvia a mesma `idempotencyKey`
(simulando retry de cliente) e confere que a segunda resposta é o MESMO
`orderId`. `teardown()` confere, via API (k6 não tem driver de banco):
`estoqueFinal == estoqueInicial - pedidosAceitos` e `estoqueFinal >= 0` — a
prova de que "banco não entra em estado inconsistente" sob esta carga.

### 5 — Driver first-accept-wins (`05-driver-first-accept-wins.js`) — crítico

Cada trial cria um pedido pronto para entrega e dispara o aceite de **todos**
os motoristas do pool **ao mesmo tempo**, via `http.batch()` (concorrência de
rede real, não uma sequência de chamadas que só "parece" concorrente).
Confere exatamente 1 vencedor (200), o resto 409 `DELIVERY_ALREADY_TAKEN`,
que o perdedor não lê os detalhes da entrega (403), que só o vencedor
aparece em `GET .../current`, e conclui a entrega (pickup → saiu para
entrega → confirma com o código) para liberar o motorista para o próximo
trial. Repetido N vezes (`iterations`) — a garantia de atomicidade já
coberta em `tests/PedeLocal.IntegrationTests/Deliveries/DeliveryConcurrencyTests.cs`
é a mesma sendo exercitada aqui, sob volume k6.

Cada VU tem seu **próprio** pool de motoristas (particionado por `__VU` em
`setup()`) — evita qualquer interferência entre VUs disputando o pool alheio.

### 6 — Status de pedido (`06-order-status-transitions.js`)

Duas checagens por trial: (1) N chamadas simultâneas de `accept` no mesmo
pedido `Pending` — todas devem retornar 200 (idempotente) **e** o histórico
do pedido deve conter exatamente uma entrada `Accepted`; (2) N chamadas
simultâneas de `ready` pulando `preparing` — todas devem continuar
rejeitadas com 409 `INVALID_ORDER_TRANSITION`.

**Achado real** (ver "Gargalos encontrados" abaixo): a checagem (1) falha
consistentemente — `Order` não tem token de concorrência otimista, então
chamadas concorrentes de uma transição idempotente podem duplicar a entrada
de histórico (e o envio de evento SignalR/notificação) mesmo com o valor
final de `Order.Status` correto.

### 7 — SignalR (`07-signalr.js`)

k6 não tem cliente SignalR pronto — implementa o protocolo JSON do Hub
diretamente sobre `k6/ws` (módulo estável): negocia (`POST
/hubs/pedelocal/negotiate?negotiateVersion=1`, autenticado), conecta o
WebSocket, faz o handshake (`{"protocol":"json","version":1}` + `\x1e`),
escuta invocações. Mede conexões simultâneas, latência de handshake e —
ponta a ponta — se um evento de domínio real (`OrderStatusChanged`,
disparado por um `accept` feito pelo próprio VU logo após conectar) chega
dentro de um timeout.

**Limitação documentada**: correlacionar eventos entre VUs *independentes*
("VU B recebe o evento causado pelo pedido do VU A") não é praticamente
testável em k6 puro — VUs são processos JS isolados sem canal de
coordenação em tempo real nativo (diferente de, digamos, um cliente
Node/Playwright rodando várias conexões no mesmo processo). Por isso cada VU
aqui dispara e escuta o **próprio** evento; isolamento por
usuário/estabelecimento/cidade já está coberto por testes de integração
.NET reais (`tests/PedeLocal.IntegrationTests/Security/HubAuthorizationTests.cs`)
— não duplicado aqui. Reconexão automática do SDK cliente
(`@microsoft/signalr`, backoff exponencial) também não é simulada — este
cenário mede o que o **servidor** entrega sob N conexões simultâneas, não o
comportamento do SDK cliente.

**Detalhe de protocolo não óbvio**: a resposta de `negotiate` com
`negotiateVersion=1` traz `connectionId` (só para exibição) **e**
`connectionToken` — é o `connectionToken` que a URL do WebSocket espera em
`id=`; usar `connectionId` aí retorna 404 "No Connection with that ID". Não
afeta clientes reais (o SDK `@microsoft/signalr` resolve isso internamente),
só quem implementa o protocolo cru como este script.

## Perfis de carga

| Perfil | Cenários `ramping-vus` (01,02,03,04,07) | Cenários `shared-iterations` (05,06) |
|---|---|---|
| **smoke** | 5 VUs, ~35s | 1 VU, 3 iterações |
| **load** | rampa até 25–50 VUs (varia por cenário), alguns minutos | 3 VUs, 10–15 iterações |
| **stress** | rampa em degraus até `STRESS_MAX_VUS` (default 80, nunca hardcoded em milhares) | `STRESS_RACE_VUS`/`STRESS_RACE_ITERATIONS` (defaults 5/30) |

Selecionado via `PROFILE=smoke|load|stress` (default `smoke`). Ver
`lib/profiles.js` para os números exatos por cenário — cada um ajusta VUs
conforme o que faz sentido para aquele fluxo (catálogo aguenta mais VUs que
login, por exemplo).

```bash
PROFILE=smoke k6 run tests/performance/scenarios/01-login.js
PROFILE=load  k6 run tests/performance/scenarios/02-catalog-browse.js
PROFILE=stress STRESS_MAX_VUS=150 k6 run tests/performance/scenarios/02-catalog-browse.js
```

## Comandos

```bash
# um cenário
k6 run tests/performance/scenarios/01-login.js

# exportar resumo estruturado
k6 run --summary-export tests/performance/results/login-smoke.json tests/performance/scenarios/01-login.js

# todos os cenários em smoke
tests/performance/run-smoke.sh        # bash
tests/performance/run-smoke.ps1       # PowerShell
```

## Thresholds

Definidos por cenário (ver cada arquivo em `scenarios/`), sempre em duas
categorias:

- **Latência** (`http_req_duration{name:...}`) — `p(95)`/`p(99)` por
  endpoint nomeado (tags), nunca um número global genérico.
- **Correção** (`pedelocal_*_total` customizados) — contadores que devem
  ficar em `count==0`: duplicidade de pedido, valores inconsistentes,
  anomalia de corrida, anomalia de transição. Qualquer valor > 0 aqui é
  sempre um bug real, nunca ruído estatístico (ao contrário de latência, que
  naturalmente varia com hardware/rede).

Os números de latência atuais são **um ponto de partida deliberadamente não
agressivo**, calibrado com uma única execução local (ver "Resultados
observados" abaixo) — **não são um SLA**. Refine-os depois de um baseline
real em staging (hardware, rede e volume de dados diferentes do notebook
onde esta suíte foi validada). Os contadores de correção, ao contrário, não
devem ser afrouxados — `count==0` é a barra certa independentemente de
hardware.

## Como interpretar os resultados

A saída padrão do k6 (`k6 run`) já imprime, ao final:

- `checks_succeeded`/`checks_failed` — validações de negócio (status code,
  forma da resposta, invariantes como "vencedor único").
- `http_req_duration` — geral e por `{name:...}` (tag de endpoint) — aqui
  estão os p50 (`med`)/p90/p95 por endpoint; para p99 explícito, use
  `--summary-export` (JSON completo) ou rode com `-e K6_SUMMARY_TREND_STATS`
  configurando `p(99)`.
- `http_req_failed` — taxa de erro real (429 já excluído via
  `setResponseCallback`, ver seção de rate limiting acima).
- `http_reqs` / a duração total — throughput aproximado (`http_reqs` ÷
  duração do teste).
- Métricas `pedelocal_*` customizadas — os invariantes de negócio de cada
  cenário (ver tabela por cenário acima).

Para achar o endpoint mais lento: procure a métrica `http_req_duration` com
a maior tag `{name:...}` — cada `apiGet`/`apiSend`/`apiSend` em `lib/http.js`
tageia a chamada pelo nome lógico do endpoint (não a URL crua com IDs), então
os agregados já vêm agrupados corretamente por endpoint mesmo com centenas
de pedidos/entregas distintos.

## Resultados observados (baseline local, 2026-09-08)

Executado localmente (Windows, Docker Desktop, Postgres em container,
mesma máquina) — `PROFILE=smoke` em todos os cenários, API em
**Development** com `RateLimiting__AuthPermitLimit` elevado só para esta
validação (nunca o default de produção — ver seção de rate limiting).
Números de **um notebook de desenvolvimento**, não de hardware de staging —
tratem como um piso de sanidade, não como referência de capacidade.

| Cenário | Requisições | p95 chave | Resultado |
|---|---|---|---|
| 01 — Login | 90 | `auth:login` p95 ≈ 818ms, p99 ≈ 898ms–1,51s (variou entre execuções) | ✅ passou (após ajuste de threshold — ver gargalo abaixo) |
| 02 — Catálogo | 296 | todos os endpoints p95 < 11ms | ✅ passou |
| 03 — Preview | 147 | `orders:preview` p95 ≈ 13ms | ✅ passou, 0 inconsistências |
| 04 — Criação de pedidos | 154 | `orders:create` p95 ≈ 13ms | ✅ passou — 3 pedidos aceitos, 132 corretamente rejeitados por falta de estoque, estoque final consistente |
| 05 — Driver race | 69 | `driver:accept` p95 ≈ 204ms | ✅ passou — 3/3 trials com exatamente 1 vencedor, 0 anomalias |
| 06 — Transições de status | 42 | `orders:accept` p95 ≈ 41ms | ❌ achado real — ver abaixo |
| 07 — SignalR | 240 HTTP + 76 conexões WS | handshake p95 = 6ms, entrega de evento p95 ≈ 64ms | ✅ passou — 100% conexão, 100% entrega de evento |

Throughput e taxa de erro: em todos os cenários, `http_req_failed` ficou em
**0%** depois dos ajustes descritos abaixo (a única fonte de erro real
encontrada foi o bug do cenário 6, não infraestrutura/timeout).

## Gargalos encontrados

### 1. Login: latência dominada por BCrypt (esperado, não é bug)

`POST /api/auth/login` mostrou p99 ≈ 1,5s já com só 5 VUs locais — alto para
um endpoint de leitura simples, mas **por design**: `AuthService`/
`BCryptPasswordHasher` usa fator de custo 12 (`docs/AUTH_FLOW.md`), que é
deliberadamente lento para dificultar força bruta offline caso o hash
vaze. Reduzir o custo do BCrypt para "melhorar performance" enfraqueceria
segurança — **não é uma correção aceitável aqui** (viola a regra desta
etapa: nunca enfraquecer segurança para facilitar/acelerar testes). O
threshold do cenário 1 foi ajustado para refletir esse patamar real
(`p(99)<2500`) em vez de reduzir a segurança para bater um número mais
baixo. Recomendação para staging: medir o custo de CPU do BCrypt sob carga
real e dimensionar réplicas/CPU da API de acordo — não o algoritmo.

### 2. `Order` sem token de concorrência otimista — histórico duplicado sob transição concorrente (real, correção não aplicada aqui)

**Achado**: o cenário 6 mostrou, de forma reproduzível (3/3 trials em duas
execuções distintas), que **N chamadas concorrentes de uma transição
idempotente permitida** (`accept` num pedido `Pending`) produzem **N
entradas** de `OrderStatusHistory` com o mesmo status, não uma — mesmo o
valor final de `Order.Status` ficando correto. O mesmo caminho também
dispara `realtime.OrderStatusChangedAsync`/`notificationService.NotifyAsync`
uma vez por chamada concorrente, não uma vez por transição real.

**Causa raiz**: `OrderService.AcceptAsync` (e as demais transições em
`TransitionAsync`/`CancelAsync`, e as chamadas equivalentes em
`DeliveryService`) fazem a checagem de idempotência lendo `order.Status` no
início do método, mas `Order` **não tem** um token de concorrência otimista
(diferente de `Delivery.Version` e `Product.Version`, já existentes —
`docs/DECISIONS.md` #07 e `docs/SECURITY_AUDIT.md` #4). Se duas (ou mais)
requisições concorrentes leem `Pending` antes de qualquer uma commitar,
todas passam pela guarda de idempotência e cada uma insere sua própria
entrada de histórico.

**Por que não corrigido nesta PR**: a correção correta e seguindo o mesmo
padrão já estabelecido (`Order.Version` como concurrency token) exigiria
alterações em **múltiplos pontos de mutação de `Order.Status` em dois
services** (`OrderService.TransitionAsync`/`MarkReadyAsync`/`CancelAsync` e
`DeliveryService.AcceptAsync`/`PickupAsync`/`OutForDeliveryAsync`/
`ConfirmAsync`), cada um precisando de tratamento de
`DbUpdateConcurrencyException` + uma nova migration — uma mudança
estrutural, não uma correção pequena e isolada. Por instrução explícita
desta etapa ("mudanças estruturais devem ser documentadas para outra PR"),
o achado fica registrado aqui para uma PR dedicada, com o mesmo padrão já
usado duas vezes neste código (`Delivery.Version`, `Product.Version`) como
referência direta de implementação.

**Impacto real**: não corrompe o estado final do pedido (o status termina
correto) nem duplica efeitos financeiros/estoque para produtos
`Uncontrolled` (a maioria) — para produtos `Controlled`, o
`Product.Version` já existente absorve parte do problema (uma segunda
dedução concorrente de estoque already viraria 409 `PRODUCT_STOCK_CONFLICT`).
O impacto real e confirmado é: histórico de auditoria poluído (múltiplas
entradas idênticas) e tráfego redundante de SignalR/push por transição —
qualidade de dado e ruído, não perda de integridade financeira. Ainda
assim, é um bug real de idempotência que deveria ser corrigido antes de
produção lidar com volume real de cliques duplicados/retries de rede.

**Cenário 6 falha propositalmente no smoke** até essa correção existir —
ver seção de CI abaixo para como isso é tratado sem travar todo PR.

## Banco: observações desta etapa

Nenhuma consulta N+1 nem falta de índice óbvia foi observada nos cenários
mais lidos (catálogo, preview) — latências desses caminhos ficaram na casa
de milissegundos de ponta a ponta já no baseline local (seção acima), sem
sinal de degradação a 5 VUs. Isso **não** substitui um perfil de `load`real
contra staging com volume de dados representativo (o baseline local usa um
banco praticamente vazio, criado pela própria suíte) — os índices existentes
(`docs/SECURITY_AUDIT.md` já revisou constraints/cascades/unique index na
auditoria anterior) devem ser reavaliados especificamente sob volume
realista de pedidos/produtos antes do deploy, não assumidos como suficientes
só por este baseline.

Nenhuma migration foi alterada nesta etapa (só o achado documentado acima
recomendaria uma nova migration, para a PR dedicada).

## CI

`run-smoke.sh` está pronto para um job de CI **não-bloqueante** (`continue-on-error`
no step do k6, artefato de resultado sempre enviado) — ver
`.github/workflows/ci.yml`, job `perf-smoke`. Não-bloqueante propositalmente:
o cenário 6 tem uma falha real e conhecida (achado #2 acima) que não deve
travar todo PR até ser corrigido numa PR dedicada; um job que sempre falha
"por padrão" só treina o time a ignorar o sinal vermelho.

`load`/`stress` **não** rodam automaticamente em nenhum job — são
executados manualmente (`PROFILE=load k6 run ...`) ou por um workflow
disparado sob demanda (`workflow_dispatch`), nunca em todo PR. Gerar carga
crescente configurável (`stress`) automaticamente a cada push seria caro e
ruidoso sem necessidade.

## Antes de rodar em staging

1. Nunca aponte `PEDELOCAL_API_URL` para staging/produção sem autorização
   explícita de quem opera o ambiente.
2. Configure `RateLimiting__AuthPermitLimit`/`GlobalPermitLimit` no
   ambiente de staging (nunca no código de produção) antes de rodar
   `load`/`stress` — o `smoke` já cabe no default.
3. Rode `smoke` primeiro sempre, mesmo em staging — confirma que a suíte e
   o ambiente estão íntegros antes de qualquer carga maior.
4. Espere `stress` gerar 4xx genuínos (409 de estoque/corrida, 429 se o
   limite não for ajustado) — isso é esperado; o que importa é
   `pedelocal_*_total` customizados ficarem em zero e a taxa de `5xx`
   ficar baixa.
5. Refine os thresholds de latência com o baseline real de staging antes de
   usá-los como critério de aprovação/reprovação de deploy — os valores
   atuais vêm de uma única máquina de desenvolvimento (seção "Resultados
   observados" acima).
6. Após rodar, confira `docs/SECURITY_AUDIT.md` — nada nesta suíte deve
   levar a enfraquecer rate limit/autenticação de produção; qualquer ajuste
   de limite é sempre escopado ao ambiente de teste.
