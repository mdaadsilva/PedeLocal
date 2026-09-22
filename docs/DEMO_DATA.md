# Dados de demonstração (seed DEV)

## Objetivo

Permitir subir o PedeLocal localmente com uma base de dados **fictícios** rica —
3 cidades, dezenas de estabelecimentos variados, clientes, entregadores,
~30 dias de histórico de pedidos e assinaturas — para testar visualmente
Admin/Merchant/Customer/Driver sem precisar cadastrar tudo manualmente. Não é
teste automatizado (isso é `tests/performance` e `tests/e2e`) — é infraestrutura
de **demonstração/exploração manual**.

## Segurança — leia antes de usar

- **Só roda em `ASPNETCORE_ENVIRONMENT=Development`.** Duas guardas
  independentes: `Program.cs` recusa (`Environment.Exit(1)`) antes mesmo de
  montar o host se `--seed-demo`/`--reset-demo` forem passados fora de
  Development; `DemoDataSeeder.SeedAsync`/`ResetAsync` verificam de novo por
  conta própria (`InvalidOperationException`) — mesmo que alguém chame essas
  funções de outro lugar no futuro, sem passar pelo `Program.cs`.
- **Nunca roda automaticamente.** Diferente do `DevelopmentDataSeeder`
  (que roda sozinho a cada `dotnet run` em Development), o seed de demo só
  executa se você passar `--seed-demo`/`--reset-demo` explicitamente na linha
  de comando.
- **Sem endpoint HTTP.** Não existe rota de API para disparar o seed — só CLI.
- **Reset nunca é genérico.** `--reset-demo` nunca faz um `DELETE` sem filtro —
  cada remoção é `WHERE Id IN (...)` com os ids determinísticos do próprio
  perfil (ver "Como identificar dado demo" abaixo). Nunca afeta
  `admin@pedelocal.dev`, a "Lanchonete Exemplo" ou qualquer outra linha criada
  pelo `DevelopmentDataSeeder`.
- **Senha DEV exclusiva de Development** — ver seção própria abaixo.

## Perfis

| | SMALL | DEMO (padrão) | LARGE |
|---|---|---|---|
| Cidades | 1 (Guaimbê) | 3 | 3 |
| Estabelecimentos | ~5 | ~60 | ~120 |
| Clientes | ~8 | ~50-70 | ~120-150 |
| Entregadores | ~3 | ~18-24 | ~35-45 |
| Histórico de pedidos | 3 dias | 30 dias + hoje | 30 dias + hoje |
| Uso recomendado | dev básico, rápido | teste manual/demo (padrão) | teste visual/performance local |

`LARGE` nunca é executado automaticamente por nada — só via
`--profile Large` explícito, e é mais lento (mais SaveChanges/rows).

Números reais observados numa execução `DEMO` local (Docker Postgres, banco
vazio): **3 cidades, 60 estabelecimentos, 147 usuários, 123 endereços, 656
produtos, 24 entregadores, 2414 pedidos, 1285 entregas, 57 assinaturas (3
estabelecimentos deliberadamente sem assinatura)**. Os números variam um pouco
a cada mudança de código (a geração é determinística por *versão* do código,
não um número fixo promitido) — o comando sempre imprime a contagem real ao
final.

## Cidades e estabelecimentos

As 3 cidades são as mesmas do `DevelopmentDataSeeder` (Guaimbê, Júlio
Mesquita, Getulina) — reaproveitadas por nome se já existirem, nunca
duplicadas. `DEMO` distribui os estabelecimentos aproximadamente 30/10/20
entre elas.

Tipos gerados (11, cobrindo os pedidos do time): Mercado, Supermercado,
Adega, Hamburgueria, Pizzaria, Restaurante, Farmácia, Conveniência, Açougue,
Padaria, Hortifruti — cada um com catálogo de produtos realista e específico
do tipo (ex.: Açougue vende cortes de carne, Adega vende bebidas). Nomes são
fictícios (`Mercadinho da Vila`, `Point da Pizza Sabor Real`, etc.) — nenhuma
marca real. Porte (pequeno/médio/grande) varia o tamanho do catálogo, o valor
da assinatura e o horário via `OperationalStatus`.

**"Horário de funcionamento" não existe como entidade no domínio hoje** — só
`OperationalStatus` (Open/ClosedManual/ClosedSchedule/Paused). O pedido de
"variar horários" foi mapeado para variar esse campo. Registrado aqui como
gap real, não como omissão silenciosa.

Cada estabelecimento tem ~85% de chance de suportar Delivery (tem
`DeliveryPricingSettings` com `deliveryFee` fixo entre R$5-15 e
`deliveryRadiusKm` entre 5-20km, variados por estabelecimento) — o resto é
só Pickup, do jeito real que o domínio já trata a ausência dessa
configuração (sem flag nova). Estoque: produtos candidatos a `Controlled`
(carnes, hortifruti, bebidas geladas, farmácia etc.) ficam `Controlled`
~55% das vezes, com estoque normal/baixo/zerado distribuído 70/20/10 — o
resto é `Uncontrolled`.

Uma pequena parcela dos estabelecimentos (~10%) fica `OperationalStatus.Paused`
— motivo variado (`HighDemand`/`NoDriverAvailable`/`TemporaryClosure`/`Other`)
e `pausedUntilUtc` distribuído entre "ainda pausado", "já venceu" (exercita
`EstablishmentPauseExpiryJob`/a checagem lazy de pausa vencida) e "indefinido"
(sem data, só resume manual).

## Contas de teste e credenciais

Todas as contas demo usam a **mesma senha de Development**:

```
Senha123!
```

É a mesma constante já usada pelo `DevelopmentDataSeeder` (`admin@pedelocal.dev`
etc.) — satisfaz a política real de senha (`AuthValidators.cs`: 8+ caracteres,
maiúscula, minúscula, número), é exclusiva de Development (as duas guardas de
segurança acima garantem isso) e nunca deve ser usada em Staging/Production.

### Tabela de credenciais (perfil DEMO)

| ROLE | CIDADE | E-MAIL | SENHA | ESTABELECIMENTO |
| --- | --- | --- | --- | --- |
| SUPER_ADMIN | Plataforma | admin@pedelocal.dev | Senha123! | — |
| CUSTOMER | Guaimbê | customer.guaimbe@pedelocal.dev | Senha123! | — |
| CUSTOMER | Getulina | customer.getulina@pedelocal.dev | Senha123! | — |
| CUSTOMER | Júlio Mesquita | customer.juliomesquita@pedelocal.dev | Senha123! | — |
| DRIVER | Guaimbê | driver.guaimbe@pedelocal.dev | Senha123! | — |
| DRIVER | Getulina | driver.getulina@pedelocal.dev | Senha123! | — |
| DRIVER | Júlio Mesquita | driver.juliomesquita@pedelocal.dev | Senha123! | — |
| ESTABLISHMENT_ADMIN | Guaimbê | merchant.guaimbe@pedelocal.dev | Senha123! | loja "vitrine" de Guaimbê (Supermercado) |
| ESTABLISHMENT_ADMIN | Getulina | merchant.getulina@pedelocal.dev | Senha123! | loja "vitrine" de Getulina (Pizzaria) |
| ESTABLISHMENT_ADMIN | Júlio Mesquita | merchant.juliomesquita@pedelocal.dev | Senha123! | loja "vitrine" de Júlio Mesquita (Restaurante) |

O nome exato de cada loja "vitrine" varia por execução (gerado
deterministicamente, mas a partir de um pool de nomes) — o comando sempre
imprime o nome real no resumo final. Cada `merchant.<cidade>@pedelocal.dev` só
administra essa loja (link real via `EstablishmentUsers`, testável em
`/api/merchant/establishments/{id}/orders` — 200 na loja própria, 403 em
qualquer outra).

Além dessas 10 contas previsíveis, o perfil DEMO cria dezenas de contas de
preenchimento — `cliente###@demo.pedelocal.dev`,
`entregador###@demo.pedelocal.dev`, `dono.<slug-da-loja>@demo.pedelocal.dev`
(uma por estabelecimento não-vitrine) — mesma senha, claramente identificáveis
pelo domínio `@demo.pedelocal.dev`.

### Onde logar

| Papel | App |
| --- | --- |
| SUPER_ADMIN | `apps/admin-web` |
| ESTABLISHMENT_ADMIN | `apps/merchant-web` |
| CUSTOMER | app Flutter mobile |
| DRIVER | app Flutter mobile |

## Assinaturas geradas

O domínio real só tem 3 valores de `SubscriptionPaymentStatus`
(`Active`/`OverdueGrace`/`SuspendedPayment`) e 5 de `CommercialStatus`
(`Active`/`OverdueGrace`/`SuspendedPayment`/`SuspendedAdmin`/`Inactive`) — o
seed usa exclusivamente esses valores, mapeados assim:

| Conceitual | `SubscriptionPaymentStatus` | `CommercialStatus` | `DueDate` |
| --- | --- | --- | --- |
| ACTIVE (maioria) | `Active` | `Active` | futuro |
| GRACE_PERIOD | `OverdueGrace` | `OverdueGrace` | 1-4 dias vencido (dentro dos 5 dias de carência) |
| EXPIRED | `SuspendedPayment` | `SuspendedPayment` | 6+ dias vencido (além da carência) |
| BLOCKED | `Active` (pagamento em dia) | `SuspendedAdmin` | futuro |

**BLOCKED** é mapeado para `CommercialStatus.SuspendedAdmin` — o único
bloqueio real independente de pagamento que existe no domínio hoje; a
assinatura em si continua `Active`, deixando claro que é um bloqueio
administrativo, não financeiro.

`GraceDays` é sempre `5`, respeitando a regra real
(`SubscriptionGracePeriodJob`). Não existe conceito de "Plano" (Essential/Pro/
Enterprise) no backend — não foi inventado um; `MonthlyAmount` varia só por
porte (pequeno R$59,90–79,90 / médio R$99,90–149,90 / grande R$199,90–299,90)
como sinal financeiro coerente. Estabelecimentos ACTIVE/GRACE_PERIOD/BLOCKED
ganham 1-3 `EstablishmentPayment` históricos.

**3 estabelecimentos (perfil DEMO) ficam deliberadamente sem nenhuma linha em
`EstablishmentSubscriptions`** — reproduz `SUBSCRIPTION_NOT_FOUND` de propósito
(`GET /api/admin/establishments/{id}/subscription` → 404). São sempre os
mesmos 3 (determinístico); o comando de seed imprime a contagem, e uma consulta
rápida aos estabelecimentos com esse 404 identifica exatamente quais são —
todo o resto (incluindo a "Lanchonete Exemplo" do `DevelopmentDataSeeder`) tem
assinatura configurada.

## Como identificar dado demo (idempotência e reset)

Não existe uma tabela "marcador" nova nem coluna `IsDemo` — a estratégia é
**id determinístico**: todo id de linha criada pelo seed vem de
`DemoIdFactory` (hash MD5 de uma string como
`"pedelocal-demo|Demo|order|1000482"`), namespaced por perfil. Rodar
`--seed-demo` de novo recalcula o mesmo plano e só insere o que ainda não
existe por esse id (idempotente — não duplica). `--reset-demo` recalcula o
mesmo plano e apaga só as linhas com esses ids, em ordem seguindo as
dependências reais de FK (histórico → pedidos/entregas → catálogo → loja →
usuários → endereços → cidade) — nunca um `DELETE` sem filtro.

As 10 contas previsíveis (customer/driver/merchant.\<cidade\>@pedelocal.dev)
têm id **independente de perfil** (mesmo id em SMALL/DEMO/LARGE, já que o
e-mail delas é fixo pelo enunciado da tarefa) — assim trocar de perfil não
tenta inserir um segundo usuário com o mesmo e-mail. Toda loja/produto/pedido
de preenchimento, e o slug de cada loja, já nascem com um sufixo numérico
próprio do perfil, então dois perfis nunca colidem em nenhum índice único real
(e-mail, CPF, slug).

**Recomendado**: rode um perfil por vez. Trocar de perfil sem resetar o
anterior primeiro **não quebra o banco** (idempotência acima garante isso),
mas pode deixar as 10 contas previsíveis vinculadas à loja "vitrine" do
**primeiro** perfil rodado (o link dono→loja não é substituído automaticamente
ao trocar de perfil). Para uma base limpa e coerente de um perfil específico:
`--reset-demo --profile <perfil-anterior>` antes de semear o novo.

## Comandos

```bash
# perfil recomendado (padrão se --profile for omitido)
dotnet run --project src/PedeLocal.Api -- --seed-demo
dotnet run --project src/PedeLocal.Api -- --seed-demo --profile Demo

# rápido, poucos dados — desenvolvimento do dia a dia
dotnet run --project src/PedeLocal.Api -- --seed-demo --profile Small

# volume maior — teste visual/performance local (nunca automático)
dotnet run --project src/PedeLocal.Api -- --seed-demo --profile Large

# apaga só os dados demo do perfil informado (não roda em Staging/Production)
dotnet run --project src/PedeLocal.Api -- --reset-demo --profile Demo
```

Cada execução sobe o host normalmente (migrações + `DbSeeder` +
`DevelopmentDataSeeder`, exatamente como um `dotnet run` comum em
Development), aplica o seed/reset pedido, imprime um resumo com as contagens
reais e a tabela de credenciais, e **sai sem subir o servidor HTTP**.

## Como testar cada papel

- **Super Admin** (`apps/admin-web`, login `admin@pedelocal.dev`): dashboard
  deve mostrar as 3 cidades, ~60 estabelecimentos, mix de status de assinatura
  (ativas/carência/suspensas/bloqueadas), pedidos de hoje e dos últimos 30
  dias, volume por cidade/forma de pagamento/DELIVERY vs PICKUP.
- **Merchant** (`apps/merchant-web`, login `merchant.<cidade>@pedelocal.dev`):
  a loja "vitrine" daquela cidade tem catálogo rico, estoque variado e pedidos
  de HOJE em todos os status relevantes (novo, aceito, preparando, pronto,
  em entrega, concluído, cancelado) — pensada para nunca parecer vazia numa
  demonstração ao vivo.
- **Customer** (app mobile, login `customer.<cidade>@pedelocal.dev`): histórico
  de pedidos naquela cidade, endereço já cadastrado.
- **Driver** (app mobile, login `driver.<cidade>@pedelocal.dev`): sempre
  `Approved`, com CNH válida e veículo cadastrados, pronto para uso
  imediato; alguns motoristas de preenchimento ficam deliberadamente com
  uma entrega em andamento ("entrega atual") para demonstrar esse estado.
  Os entregadores de preenchimento cobrem toda a distribuição de onboarding
  (ver seção "Entregadores" abaixo) — útil para testar a tela de revisão do
  Super Admin sem precisar cadastrar um entregador novo manualmente.

### Entregadores (CNH, veículo, onboarding)

Distribuição dos entregadores de preenchimento (ver `docs/DRIVER_ONBOARDING.md`
para o significado de cada estado): ~55% `Approved` com CNH válida, ~10%
`Approved` mas com CNH **já vencida** (demonstra `DocumentExpired` sem mexer
no `onboardingStatus`), ~15% `UnderReview` (CNH + veículo enviados,
aguardando revisão), ~10% `PendingDocuments` (nada enviado ainda), ~5%
`Rejected` (com motivo de reprovação preenchido), ~5% `Approved` porém
`Blocked` (suspenso pelo admin). CNH e placas são sempre sintéticas —
nenhum documento real.

## Limitações e gaps conhecidos

- Sem entidade de horário de funcionamento no domínio — ver seção acima.
- Trocar de perfil sem resetar antes não corrompe dados, mas pode deixar os
  donos "vitrine" vinculados à loja do perfil anterior — ver seção acima.
- Nomes de estabelecimento (não o slug, que é sempre único) podem
  ocasionalmente se repetir entre dois estabelecimentos do mesmo tipo — é só
  duplicidade cosmética de display name, nunca de identificador.
- `SUBSCRIPTION_NOT_FOUND` continua sendo uma resposta 404 crua da API para
  esses 3 estabelecimentos propositais — o frontend tratar isso de forma mais
  amigável (em vez de um erro genérico) é um gap de produto real, não algo
  este seed deveria mascarar.

## Testes

`tests/PedeLocal.UnitTests/Demo/DemoDataPlannerTests.cs` — testa o
planejador puro (sem banco): determinismo, estoque nunca negativo,
Subtotal+DeliveryFee=Total em todo pedido, Delivery só com
`DeliveryPricingSettings`, motorista sempre da mesma cidade da entrega, no
máximo uma entrega ativa por motorista, assinaturas só com enum real, janela
de carência de 5 dias, as contagens por perfil, cobertura de todo estado de
onboarding do entregador (incluindo CNH vencida), estabelecimentos pausados
com motivo e expiração variados, e taxa/raio de entrega sempre positivos e
variados entre estabelecimentos.

`tests/PedeLocal.IntegrationTests/Demo/DemoDataSeederTests.cs` — testa contra
um `PedeLocalDbContext` real (InMemory) via o container de DI da aplicação:
guarda de ambiente (recusa fora de Development), idempotência (`SeedAsync` 2x
não duplica), contas previsíveis autenticam de verdade, ownership do merchant,
estoque/cidade coerentes, cobertura de assinatura, CNH/veículo seedados sem
órfãos, `ResetAsync` remove só as linhas demo (CNH/veículo inclusive)
preservando o `DevelopmentDataSeeder` intacto, reset sem seed prévio é no-op
seguro, e troca de perfil sem reset não viola constraint única nenhuma.
