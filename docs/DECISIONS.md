# Decisões de arquitetura

Registro de decisões tomadas diante de ambiguidades na especificação
(`01_pedelocal_claude_backend_FINAL.md`), conforme instruído na seção 84 do
próprio documento: "escolha a solução mais simples e segura; registre a
decisão aqui; não interrompa o trabalho por detalhes não críticos."

## 01 — Acesso a dados: `IAppDbContext` em vez de repositórios por agregado

A Application layer depende de uma interface `IAppDbContext` (exposta como
`DbSet<T>` por entidade + `SaveChangesAsync`), implementada pelo
`PedeLocalDbContext` na Infrastructure. Não foram criados repositórios
individuais por agregado.

**Por quê:** com ~20 módulos e dezenas de entidades, um repositório por
agregado multiplicaria a quantidade de código sem ganho real de testabilidade
(o próprio `IAppDbContext` já é substituível por EF InMemory nos testes).
Mesma abordagem usada pelo template "Clean Architecture" da Microsoft/Jason
Taylor. Satisfaz o requisito de "acesso a dados isolado" (seção 4) porque a
Application nunca referencia `PedeLocalDbContext` nem o pacote Npgsql.

## 02 — Enums persistidos como `string` no Postgres

Todos os enums de domínio (`OrderStatus`, `DeliveryStatus`, etc.) são mapeados
com `HasConversion<string>()`.

**Por quê:** legibilidade em consultas SQL diretas (auditoria, debug em
produção, pgAdmin) supera o ganho marginal de espaço de usar inteiros. Custo
de armazenamento é irrelevante na escala do projeto.

## 03 — Job de cobrança: Hangfire (não Quartz.NET)

A seção 53 pede para escolher uma das duas. Hangfire foi escolhido por ter
storage nativo em PostgreSQL (`Hangfire.PostgreSql`) e dashboard web pronto
para o Super Admin acompanhar execuções.

## 04 — `establishment_settings`: campos mínimos

A seção 5 lista a tabela `establishment_settings` sem detalhar colunas. Foi
modelada apenas com `IsVisible` e `AcceptsNewOrders`, que são exatamente os
filtros exigidos pela seção 19 (busca do cliente). Novos campos de
configuração podem ser adicionados sem migração destrutiva.

## 05 — `order_item_options` como snapshot relacional (não JSON)

A seção 35 pede snapshot de `options_snapshot` em `order_items`. Em vez de um
campo JSON, foi mantida a tabela relacional `order_item_options` (já prevista
na seção 5) com `OptionName`/`PriceDelta` congelados no momento do pedido.
Mais simples de consultar/testar do que serializar e desserializar JSON.

## 06 — Agrupamento de recebíveis em lote via FK nullable

`driver_receivables.payment_batch_id` (nullable) substitui uma tabela de
junção many-to-many. Um recebível pertence a no máximo um lote, então FK
simples é suficiente e mais simples que uma tabela extra.

## 07 — Concorrência na aceitação de entrega (seção 40-41)

`deliveries.version` (int) é um token de concorrência otimista **gerenciado
pela aplicação** (`DeliveryService.AcceptAsync` incrementa `Version` antes de
salvar), não a coluna de sistema `xmin` do Postgres.

**Por quê:** a primeira implementação usava `xmin` via shadow property. Ao
escrever o teste da corrida (dois `DbContext` sobre o mesmo estoque de dados,
simulando duas requisições concorrentes), o provider **InMemory** do EF Core
não gera um novo valor de `xmin` a cada UPDATE — o teste passava
silenciosamente sem nunca detectar o conflito. Uma coluna `int` incrementada
pela aplicação usa a mesma lógica de "valor original vs. valor atual" do EF
Core em qualquer provider, então o mesmo código é validado tanto no InMemory
(testes unitários) quanto no Postgres real (produção). Combinado com a
checagem rápida (`driver_id IS NULL AND status == AVAILABLE`) antes do save:
se duas requisições lerem a entrega quase ao mesmo tempo, a que salvar
primeiro vence; a segunda recebe `DbUpdateConcurrencyException`, traduzido
para `409 DELIVERY_ALREADY_TAKEN`. Nunca duas aceitam a mesma entrega — veja
`DeliveryServiceTests.Accept_ConcurrentDrivers_OnlyOneSucceeds_OtherGetsConflict`.

## 08 — Swagger: Scalar em vez de Swashbuckle

Swashbuckle.AspNetCore 7.2 ainda depende de `Microsoft.OpenApi` 1.x
(namespace `Microsoft.OpenApi.Models`), enquanto o .NET 10 (via
`Microsoft.AspNetCore.OpenApi` 10.x) já usa `Microsoft.OpenApi` 2.x — os dois
pacotes juntos geram erro de compilação (CS7069, tipos de assemblies
incompatíveis). Trocado por `Microsoft.AspNetCore.OpenApi` (geração nativa do
documento) + `Scalar.AspNetCore` (UI interativa), disponível apenas em
Development/Staging conforme seção 67.

## 09 — Rate limiting nativo do ASP.NET Core (não `AspNetCoreRateLimit`)

O pacote `AspNetCoreRateLimit` traz uma dependência transitiva vulnerável
(`Newtonsoft.Json` 11.0.1, GHSA-5crp-9r3c-p9vr) e está com manutenção lenta.
Usado em seu lugar `Microsoft.AspNetCore.RateLimiting`, nativo do framework
desde o .NET 7, configurado com uma política `"auth"` (fixed window, mais
restritiva) para os endpoints da seção 65 e um limitador global.

## 11 — Upload de arquivos: disco local por trás de uma abstração

`IFileStorageService` / `LocalFileStorageService` (seção 61). Valida
tamanho (5MB), extensão permitida (jpg/png/webp) **e** os magic bytes reais
do conteúdo (não confia em extensão nem `Content-Type` declarados pelo
cliente) antes de gravar com um nome gerado pelo servidor (nunca o nome
original — evita path traversal). Trocar para S3/Blob storage depois é
questão de nova implementação da mesma interface + registro na DI; nenhuma
regra de negócio muda.

## 12 — LGPD: exclusão de conta anonimiza, não apaga

`DELETE /api/me/account` (seção 64) sobrescreve nome/e-mail/telefone/senha
com valores anônimos, desativa o login e revoga sessões — não remove a
linha do usuário nem qualquer pedido/pagamento/auditoria que o referencie.
Isso preserva as retenções legítimas (fiscal, disputas, auditoria) exigidas
pela própria seção 64 ("a exclusão deve respeitar retenções legítimas") sem
exigir um sistema de retenção configurável, fora de escopo aqui.

## 13 — Revisão de segurança (seção 59 — OWASP)

Checklist e onde cada item foi endereçado nesta fase:

| Item | Cobertura |
|---|---|
| Broken Access Control / IDOR-BOLA | `EstablishmentAccessGuard`, `DriverAccessGuard`, checagem de ownership em Address/Order/Delivery/Notification — testado explicitamente em `OwnershipSecurityTests` (seção 77 da spec) |
| Mass Assignment | Controllers só fazem bind de DTOs em `PedeLocal.Contracts`, nunca de entidades de domínio |
| Injection | 100% EF Core parametrizado, nenhum SQL cru na base de código |
| JWT | Issuer/audience/assinatura validados, expiração curta (15min), secret validado no boot (mín. 32 chars) |
| Refresh Token | Rotativo, hash-only no banco, revogação em cascata no reset de senha |
| Rate Limit | Políticas dedicadas em auth, upload e confirmação de código de entrega; limite global para o resto |
| File Upload | Ver decisão 11 |
| Secrets | Nenhum secret hardcoded — `.env`/`user-secrets`/variáveis de ambiente; `.env` no `.gitignore` |
| Sensitive Data Exposure | Exception handler nunca retorna stack trace; logs não incluem senha/JWT completo (ver seção 63) |
| Race Conditions | Corrida de aceite de entrega — ver decisão 07 |

**Pendente para uma fase futura** (não coberto ainda, registrado aqui para
não ser esquecido): testes de integração HTTP reais contra o pipeline ASP.NET
Core completo (hoje a cobertura é 100% via testes de unidade dos serviços de
Application, o que valida as regras de negócio mas não a composição de DI/
middleware do `Program.cs` em si).

## 14 — Auditoria final (2026-09-07): 2 lacunas reais encontradas e corrigidas

Uma auditoria completa pós-implementação encontrou duas lacunas entre o que a
seção 84 pedia e o que realmente existia:

1. **3 de 8 ações de auditoria nunca eram gravadas.** `PRICE_CHANGED`,
   `STOCK_ADJUSTED` e `ORDER_CANCELLED` existiam como constantes
   (`AuditActions`) mas nenhum serviço realmente chamava
   `db.AuditLogs.Add(...)` para eles. Corrigido em
   `InventoryService.AdjustStockAsync`, `CatalogService.UpdateProductAsync`
   (só grava se o preço realmente mudou) e `OrderService.CancelAsync`.
   Testado em `tests/PedeLocal.UnitTests/Audit/AuditTrailTests.cs`.
2. **O seed de desenvolvimento da seção 74 nunca foi implementado.** Só
   `roles` e `establishment_categories` eram semeados. Adicionado
   `DevelopmentDataSeeder` (Super Admin, as 3 cidades de lançamento, 1
   cliente/entregador/estabelecimento de exemplo com produto e assinatura),
   chamado em `Program.cs` **apenas quando `IsDevelopment()`** — nunca deve
   rodar em produção (mesma exigência do documento original). Todos os
   usuários de exemplo usam a mesma senha de desenvolvimento (`Senha123!`),
   propositalmente óbvia para nunca ser confundida com uma senha real (ver
   credenciais completas no relatório final da sessão).

Nenhuma outra lacuna foi encontrada nesta auditoria — todo o resto já estava
implementado e coberto por teste antes desta revisão.

## 15 — Testes de integração HTTP reais (`PedeLocal.IntegrationTests`)

`WebApplicationFactory<Program>` sobe o `Program.cs` **de verdade** (mesmo
DI, mesmos middlewares, mesma serialização) contra EF Core InMemory, sem
precisar de Postgres/Docker. Chegar a isso funcionando revelou 3 bugs reais,
não só problemas de configuração de teste:

1. **`Log.Logger` sem nenhum sink configurado em produção.** `appsettings.json`
   não tinha seção `"Serilog"`. O bootstrap logger (`CreateBootstrapLogger()`)
   funcionava só até `UseSerilog(...)` substituir `Log.Logger` pelo logger
   "real" — que, sem sinks configurados via `ReadFrom.Configuration`, **descarta
   todos os logs silenciosamente**. Isso afetava produção também, não só
   testes — só não tinha sido percebido porque nenhum erro real havia
   acontecido ainda em um ambiente rodando de verdade. Corrigido adicionando
   a seção `Serilog` (Console sink) em `appsettings.json`.
2. **`app.Run()` dentro de um `try/catch` que engolia exceções silenciosamente.**
   O padrão original (`try { ...tudo... app.Run(); } catch (Exception ex) when
   (ex is not HostAbortedException) { Log.Fatal(...); }`) quebrava a
   interceptação que `WebApplicationFactory` usa para capturar o host sem
   escutar de verdade — qualquer exceção de configuração antes de `Run()`
   era logada e o processo terminava "com sucesso" sem nunca ter subido um
   servidor, e o teste falhava com um erro genérico do TestServer sem pista
   nenhuma da causa real. `app.Run()` foi movido para fora de qualquer
   `catch` (só um `finally` para `Log.CloseAndFlush()`, que não suprime
   exceções).
3. **Hangfire e `RecurringJob.AddOrUpdate` exigem uma conexão Postgres real
   só para *registrar*.** Ambos agora são condicionados a
   `!isTestingEnvironment` (`builder.Environment.IsEnvironment("Testing")`).
   A migração também passou a checar `db.Database.IsRelational()` — o
   provider InMemory usa `EnsureCreated` em vez de `Migrate` (que não existe
   para providers não relacionais). Nenhum destes gates muda comportamento
   fora de testes.

Outras notas de implementação:

- **Configuração via variáveis de ambiente, não `ConfigureAppConfiguration`.**
  `Program.cs` lê `Jwt:Secret` e `ConnectionStrings:Default`
  *sincronamente*, entre `CreateBuilder` e `Build()`. As customizações de
  `WebApplicationFactory.ConfigureWebHost` (`ConfigureAppConfiguration`/
  `ConfigureServices`) só são garantidas até `Build()` completar — tarde
  demais para essas leituras síncronas, que viam os valores vazios do
  `appsettings.json` (gerando uma `SymmetricSecurityKey` de tamanho zero).
  `PedeLocalWebApplicationFactory` define essas variáveis via
  `Environment.SetEnvironmentVariable` no construtor, que **é** lido por
  `WebApplication.CreateBuilder(args)` desde o início.
- **Registrar dois providers de EF Core ao mesmo tempo.** Trocar o Npgsql
  pelo InMemory exige remover **todos** os descritores de serviço fechados
  sobre `PedeLocalDbContext` (não só `DbContextOptions<T>` — o EF Core
  também registra `IDbContextOptionsConfiguration<T>` e outros por
  provider), senão o EF Core recusa com "Only a single database provider
  can be registered".
- **Paralelismo de testes desabilitado** (`[assembly:
  CollectionBehavior(DisableTestParallelization = true)]`). Cada classe de
  teste sobe seu próprio host, e o bootstrap do Serilog usa `Log.Logger`
  estático — rodar múltiplos hosts em paralelo no mesmo processo colidia
  com "the logger is already frozen". Testes de integração são mais pesados
  mesmo; rodar em série é a troca padrão aqui.

Cobertura: 16 testes end-to-end reais — login, registro, refresh (com
rotação e reuso bloqueado), `GET /api/cities`, listagem de estabelecimentos
por cidade, catálogo público, criação de pedido com cálculo de subtotal/taxa,
fluxo completo do comércio (accept/preparing/ready), fluxo completo do
entregador (accept/pickup/out-for-delivery/confirm com código), **a corrida
de aceite de entrega via duas requisições HTTP concorrentes de verdade**
(não só a nível de serviço), autorização/ownership (cliente não vê pedido
alheio, staff não vê pedido de outro estabelecimento, role errada é 403), e
estabelecimento suspenso não aceita novo pedido mas continua com todos os
dados intactos para o próprio staff.

## 10 — Migrations aplicadas automaticamente no startup

Controlado por `Database:AutoMigrate` (default `true`). Simplicidade para o
estágio atual do projeto (implantação controlada, sem múltiplas réplicas
concorrentes rodando migração ao mesmo tempo). Pode ser desligado via
configuração quando o pipeline de CI/CD assumir a aplicação de migrations.

## 16 — Docker validado pela primeira vez (2026-09-07): 4 bugs reais encontrados e corrigidos

O WSL2/Docker Desktop deste ambiente, que estava quebrado desde o build
original (ver seção "Blocker" no histórico de progresso), foi corrigido pelo
usuário. Esta foi a **primeira execução real** de `docker compose up` contra
um Postgres de verdade — o Dockerfile e o `docker-compose.yml` existiam
desde o passo 21 mas nunca haviam sido testados. A validação encontrou 4
bugs reais (nenhum workaround, todos corrigidos na causa raiz):

1. **`.dockerignore` inexistente (causa raiz do `NETSDK1064`).** O
   `.gitignore` ignora `bin/`/`obj/` para o Git, mas isso é irrelevante para
   o contexto de build do Docker — sem um `.dockerignore`, `bin/`/`obj/` de
   todos os projetos (gerados por builds locais no Windows) eram enviados
   no contexto e `COPY src/ src/` (linha 12 do Dockerfile) os sobrescrevia
   por cima do `obj/` recém-gerado pelo `dotnet restore` do container.
   O `obj/project.assets.json` do host grava `packageFolders` como um
   caminho absoluto do Windows (`C:\Users\<user>\.nuget\packages\`) —
   inexistente no container Linux — então `dotnet publish --no-restore`
   falhava com "Package X, version Y was not found" para o primeiro pacote
   que tentasse resolver (`Microsoft.AspNetCore.OpenApi 10.0.11` no caso
   relatado; a versão em si existe no NuGet.org e restaura normalmente).
   Corrigido com um `.dockerignore` na raiz do repo (mesmo diretório do
   `context: .` do `docker-compose.yml`) excluindo `**/bin/`, `**/obj/`,
   `.git/`, `.env`, etc.
2. **`Scalar.AspNetCore` fixado em `2.9.4`, versão que nunca existiu no
   NuGet** (a série pula de `2.9.0` para `2.10.0` diretamente — confirmado
   via `api.nuget.org/v3-flatcontainer/scalar.aspnetcore/index.json`). Isso
   gerava o aviso `NU1603` ("... was not found; 2.10.0 was resolved
   instead") em todo restore, tornando o build não-reprodutível. Fixado
   para `2.10.0` (a versão que o NuGet já vinha resolvendo de fato).
3. **`RecurringJob.AddOrUpdate` (API estática do Hangfire) quebrava o
   startup fora do ambiente de teste** com
   `JobStorage instance has not been initialized`. Causa: este projeto usa
   `services.AddHangfire(...)` (registro via DI), que **não** define o
   singleton estático legado `JobStorage.Current` — só a API baseada em DI
   (`IRecurringJobManager`) funciona nesse modelo. Como os testes de
   integração rodam com `isTestingEnvironment = true` (que pula esse bloco
   inteiro), esse bug nunca tinha sido exercitado. Corrigido resolvendo
   `IRecurringJobManager` via `app.Services.CreateScope()` em vez da
   chamada estática — comportamento idêntico, só a forma de obter a
   instância muda.
4. **Usuário não-root sem permissão de escrita em `/app`.** O Dockerfile
   trocava para `USER appuser` *antes* do `COPY --from=build /app .` — o
   `WORKDIR /app` já havia criado o diretório como `root` antes do
   `useradd`, então os arquivos publicados ficavam acessíveis para leitura
   mas `Directory.CreateDirectory(uploadsRoot)` (`Program.cs`, upload local
   de arquivos) falhava com `UnauthorizedAccessException` ao tentar criar
   `/app/uploads`. Corrigido copiando e ajustando a posse
   (`chown -R appuser:appuser /app`) ainda como `root`, e só então trocando
   para `USER appuser` — mantém o container rodando como não-root (não é um
   `USER root` disfarçado), só corrige a ordem das operações.

Bug adicional, fora do Dockerfile mas descoberto no mesmo teste ponta a
ponta: `GET /openapi/v1.json` retornava 500 (`NullReferenceException`) em
Development — `Program.cs` fazia `document.Components ??= new()` mas nunca
inicializava `document.Components.SecuritySchemes` (que também nasce nulo),
então indexar `["Bearer"]` explodia. O compilador já sinalizava isso com o
aviso `CS8602`, nunca corrigido. `/scalar/v1` (a página HTML do Scalar)
retornava 200 mesmo com o JSON quebrado, porque só serve o wrapper estático
— só falhava ao carregar o spec de fato. Corrigido inicializando o
dicionário antes de indexá-lo.

## 17 — Consulta do código de confirmação pelo cliente: cifra reversível separada do hash

O código de 6 dígitos era, até este ponto, gerado e imediatamente
descartado após seu hash (`DeliveryConfirmation.CodeHash`, SHA-256 via
`ITokenService.HashToken`) ser persistido — o texto puro nunca era
guardado em lugar nenhum, por design (ver comentário original em
`OrderService.MarkReadyAsync`). O mobile precisa de um jeito do CUSTOMER
consultar esse código a qualquer momento (para ler em voz alta ao
entregador), e a especificação exige "hash armazenado no banco" +
"não retornar hash" — ou seja, o hash continua sendo a única coisa
validada na confirmação do entregador, mas o texto puro precisa poder
ser recuperado por quem tem permissão, o que um hash unidirecional nunca
permite.

**Decisão**: um segundo campo, `DeliveryConfirmation.EncryptedCode`, guarda
uma cópia do código cifrada de forma **reversível** (AES-GCM,
`IDeliveryCodeCipher`/`AesDeliveryCodeCipher`), gerada no mesmo instante
que o hash em `MarkReadyAsync`. A chave AES é derivada
(`SHA-256(Jwt:Secret + ":delivery-code-cipher")`) do `Jwt:Secret` já
existente e obrigatório em todo ambiente — evita introduzir uma nova
configuração/segredo a provisionar em produção, Docker, CI e testes.
`GET /api/orders/{id}/delivery-code` decifra esse campo só para o
CUSTOMER dono do pedido; a confirmação do entregador
(`DeliveryService.ConfirmAsync`) continua comparando exclusivamente o
hash — nenhum código em texto puro passa perto desse caminho. A API nunca
retorna `CodeHash` nem `EncryptedCode` brutos em resposta alguma.

**Por quê essa alternativa e não outras**: `IDataProtector` (ASP.NET Core
Data Protection) resolveria o mesmo problema mas adiciona um subsistema de
gerência de chaves inteiro (persistência do keyring, relevante ao escalar
horizontalmente) para um caso de uso pequeno; reutilizar o segredo do JWT
com AES-GCM direto é a solução mais simples que ainda é criptograficamente
sólida (AEAD, nonce aleatório por valor).

**Semântica de uso**: a consulta pelo cliente é **repetível** enquanto o
código estiver ativo (não é de leitura única) — só a confirmação em si é
de uso único (`ConfirmedAtUtc`). Após confirmada, uma nova consulta
retorna `409 DELIVERY_CODE_ALREADY_USED`. Expiração (`ExpiresAtUtc`, 24h)
e tentativas limitadas (`Attempts`/`MaxAttempts`, 5) já existiam para a
confirmação do entregador e continuam intocados — a consulta do cliente
não tem seu próprio contador de tentativas porque exige autenticação +
ownership do pedido, não é um alvo de força bruta do mesmo tipo que digitar
um código de 6 dígitos sem saber a quem pertence.

## 18 — Suporte a PICKUP: `Order.Type`, `AddressId` nullable, novo status `PickupCompleted`

A especificação original só previa entrega por motorista. Adicionar
retirada no estabelecimento (`Pickup`) como segundo modo de cumprimento do
pedido, aditivamente:

- `OrderType { Delivery, Pickup }` (persistido como `string`, decisão #02),
  `Order.Type`, default `Delivery` — pedidos existentes continuam válidos
  sem migração de dados além do `defaultValue` da coluna nova.
- `Order.AddressId` virou `Guid?` (era `Guid` obrigatório) — a única
  mudança de schema com alcance mais amplo, mas sem quebra: nenhum outro
  código fora de `OrderService`/`DeliveryService` lia esse campo
  diretamente, e ambos já foram ajustados.
- **Novo status terminal `PickupCompleted`**, em vez de reaproveitar
  `Delivered` — decisão deliberada: `Delivered` significa, em todo o
  restante do sistema (notificações, docs, o próprio nome), "um entregador
  entregou". Sobrecarregá-lo para também significar "o cliente retirou"
  obrigaria todo consumidor (app, relatórios futuros) a checar `Order.Type`
  só para saber o que um status já deveria dizer sozinho. Um valor dedicado
  custa uma linha de enum e mantém `status` autoexplicativo.
- `MarkReadyAsync` bifurca em `Order.Type`: `Pickup` fica em `READY` sem
  criar `Delivery`/`DeliveryConfirmation` (não há entregador, não há
  código); `Delivery` segue o fluxo existente inalterado.
- Novo `POST .../orders/{orderId}/complete-pickup` (staff): fecha o pedido
  `Pickup` (`READY → PickupCompleted`) — o equivalente comercial do
  entregador confirmando o código, mas sem entregador nem código
  envolvidos. Segue a mesma regra de pagamento que `ConfirmAsync` já usava
  (`Pix` vira `Paid` na conclusão; outras formas ficam como estavam) — não
  inventa uma regra nova.
- `DeliveryPricingSettings`/`DELIVERY_PRICING_NOT_CONFIGURED` só se aplicam
  a `Delivery` — um estabelecimento sem taxa de entrega configurada pode
  vender só para retirada sem bloqueio algum.

Nenhum flag "este estabelecimento aceita retirada" foi adicionado —
todo estabelecimento aceita `Pickup` implicitamente. Fora de escopo desta
etapa; se o negócio precisar restringir por estabelecimento, é aditivo
(`EstablishmentSettings.AllowsPickup bool`, default `true`) e não exige
tocar em nada do que foi decidido aqui.

Também instalado `libgssapi-krb5-2` na imagem de runtime (`aspnet:10.0`,
baseada em Debian): sem essa lib, toda tentativa de conexão do Npgsql
logava `Cannot load library libgssapi_krb5.so.2` durante a negociação
SASL/GSS. Não travava o app (havia fallback), mas poluía os logs com um
erro real de uma lib ausente — corrigido na causa, não silenciado.

**Vulnerabilidade conhecida, não corrigida nesta sessão:** `dotnet restore`
emite `NU1903` para `System.Security.Cryptography.Xml 10.0.0` (múltiplos
CVEs de alta severidade). É dependência transitiva de
`PedeLocal.Infrastructure` (via `System.Security.Cryptography.Xml`
referenciado diretamente — ver `.csproj`). Fora do escopo desta sessão
(era sobre o build do Docker), mas deve ser investigado antes de produção:
verificar se há versão corrigida publicada ou se a dependência pode ser
removida.

Resultado final, tudo executado de verdade (não assumido): `docker compose
build --no-cache api` limpo, `docker compose up -d` com os dois containers
estáveis (`postgres` healthy, `api` sem crash-loop), as 3 migrations
aplicadas contra o Postgres real do container (`InitialCreate`,
`DeliveryVersionConcurrencyToken`, `AddUserFcmToken` — 32 tabelas
confirmadas via `\dt`), `/scalar/v1` e `/openapi/v1.json` retornando 200
com o security scheme `Bearer` presente, `dotnet build` com 0 erros, e
`dotnet test` com **109/109 testes passando** (93 unit + 16 integration).

## 19 — Tempo real/notificações: lacunas fechadas, sem novo evento de cancelamento

A infraestrutura de SignalR (`PedeLocalHub`, `SignalRRealtimeNotifier`,
grupos `customer:{id}`/`establishment:{id}`/`driver:{id}`/
`city-drivers:{cityId}`) e a de notificações (`INotificationService`,
`AppNotificationService`, `NoOpPushSender`) já existiam desta branch em
diante. O trabalho desta etapa foi fechar lacunas reais, não recriar nada —
ver `docs/REALTIME_EVENTS.md` para a referência completa do resultado.

Lacunas encontradas e fechadas:

- **First-accept-wins não invalidava a lista dos demais entregadores**:
  `DeliveryAcceptedAsync` só notificava `establishment:{id}`. Passou a
  também emitir `DeliveryAccepted` para `city-drivers:{cityId}` — **sem**
  `driverId` no payload, para não vazar identidade do motorista vencedor
  para quem não está vinculado à entrega. Reaproveita o nome de evento já
  documentado (nenhum evento novo) — o cliente distingue os dois casos pela
  presença ou não de `driverId`. O aceite em si continua atômico só no
  banco (`Delivery.Version`); este evento é puramente um sinal de UI.
- **Falha de notificação podia derrubar a operação principal**: nem
  `SignalRRealtimeNotifier` nem `AppNotificationService` protegiam contra
  exceção do transporte/provedor. Corrigido: `SignalRRealtimeNotifier`
  captura e loga por envio individual (grupo+evento); `AppNotificationService`
  persiste a linha em `notifications` antes de tentar o push e captura falha
  do `IPushSender` sem deixar de salvar. Nenhuma dessas capturas loga dado
  sensível (token, código de entrega, senha) — só tipo de evento e
  identificadores.
- **Notificações faltando** para eventos já cobertos por status reais do
  domínio: `OrderCreated` nunca chegava ao staff do estabelecimento (só via
  SignalR, sem persistência/push); `Preparing`, `OutForDelivery` e
  `Cancelled` não geravam notificação ao cliente; `DeliveryAvailable` não
  notificava entregadores offline/não conectados ao hub. Todas fechadas
  reaproveitando `NotificationEventType` existente + 4 valores novos
  (`OrderPreparing`, `OrderOutForDelivery`, `OrderCancelled` — persistidos
  como `string`, então aditivos sem migração).

Deliberadamente **não** adicionado:

- Um evento `OrderCancelled`/`DeliveryStatusChanged` dedicado — o `status`
  dentro de `OrderStatusChanged` já é suficiente (`"Cancelled"`,
  `"PickedUp"`, `"OutForDelivery"` etc.); um evento extra só duplicaria a
  mesma informação por outro nome.
- Notificação para o estabelecimento quando ele mesmo cancela um pedido, ou
  para o entregador quando ele mesmo aceita uma entrega — o ator de uma
  ação não precisa ser notificado da própria ação.
- Broadcast de localização em tempo real do entregador — fora de escopo,
  não pedido por nenhum fluxo existente.
- Um grupo/canal exclusivo para `SUPER_ADMIN` — manteria a regra "nenhum
  comportamento implícito não documentado" para esse papel; acesso
  administrativo de leitura continua só via REST.

**Testes**: cobertura nova em três camadas — unit (`SignalRRealtimeNotifierTests`
com um fake de `IHubContext` escrito à mão, já que a solução não usa
biblioteca de mock; `AppNotificationServiceTests` para a resiliência de
push; fan-out de notificação em `OrderServiceTests`/`DeliveryServiceTests`
com `CapturingNotificationService`/`CapturingRealtimeNotifier`) e integração
(`HubAuthorizationTests` — conexões SignalR **reais** contra o
`WebApplicationFactory`, transporte LongPolling porque o `TestServer` não
tem WebSocket real: conexão sem token rejeitada, isolamento por cliente,
por estabelecimento e por cidade do entregador, todos verificados com um
evento negativo *e* um positivo na mesma conexão para descartar falso
negativo por conexão quebrada).

Resultado: `dotnet build` limpo, `dotnet test` com **142/142 testes
passando** (118 unit + 24 integration).
