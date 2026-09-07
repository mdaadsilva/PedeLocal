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
