# 01 — CLAUDE CODE — BACKEND COMPLETO — PEDELOCAL

## Missão

Você será o principal engenheiro responsável por construir **todo o backend funcional** da PedeLocal.

Nesta etapa, **não construir interfaces visuais completas**.

Seu foco é entregar:

- arquitetura;
- banco;
- API;
- autenticação;
- regras de negócio;
- segurança;
- integrações;
- testes;
- documentação;
- Docker;
- CI;
- contrato da API para o frontend.

O frontend será desenvolvido depois pelo Cursor.

---

# 1. Objetivo do produto

Criar uma plataforma de pedidos e entregas para cidades pequenas.

Cidades iniciais previstas:

- Guaimbê - SP
- Júlio Mesquita - SP
- Getulina - SP

O sistema deve permitir:

```text
CLIENTE
→ escolhe cidade
→ escolhe estabelecimento
→ escolhe produtos
→ cria pedido

ESTABELECIMENTO
→ recebe pedido
→ aceita
→ prepara
→ marca pronto

ENTREGADOR
→ vê entregas disponíveis da cidade
→ aceita uma
→ fica OCUPADO
→ retira
→ entrega
→ pede código ao cliente
→ confirma código
→ volta DISPONÍVEL
```

---

# 2. Stack obrigatória

## Backend

```text
.NET 10
C#
ASP.NET Core Web API
Entity Framework Core
PostgreSQL
SignalR
JWT
Refresh Token
FluentValidation
Serilog
OpenAPI / Swagger
```

## Infra local

```text
Docker
Docker Compose
Git
GitHub
```

## Notificações

Preparar integração com:

```text
Firebase Cloud Messaging
```

## Mapas

Preparar backend para armazenar:

```text
latitude
longitude
google_place_id
```

e cálculo de distância/rota futuro.

Na PedeLocal, o frontend poderá abrir Google Maps externamente.

---

# 3. Arquitetura

Usar:

```text
MODULAR MONOLITH
```

Não usar microserviços nesta PedeLocal.

Estrutura sugerida:

```text
src/
├── PedeLocal.Api/
├── PedeLocal.Application/
├── PedeLocal.Domain/
├── PedeLocal.Infrastructure/
└── PedeLocal.Contracts/

tests/
├── PedeLocal.UnitTests/
└── PedeLocal.IntegrationTests/

docs/
```

Módulos:

```text
Identity
Cities
Establishments
Catalog
Inventory
Orders
Drivers
Deliveries
Finance
Subscriptions
Notifications
Admin
Audit
```

---

# 4. Regras de arquitetura

Obrigatório:

- controllers finos;
- regras no Application/Domain;
- validação server-side;
- acesso a dados isolado;
- transações em operações críticas;
- autorização por role e ownership;
- logs estruturados;
- migrations versionadas;
- contratos de API bem definidos;
- testes para regras críticas.

Evitar:

- lógica em controllers;
- services gigantes;
- código duplicado;
- ifs por estabelecimento espalhados;
- secrets no código;
- uso de float para dinheiro.

---

# 5. Banco de dados

Utilizar PostgreSQL.

## Entidades principais

```text
users
roles
user_roles

cities

establishments
establishment_users
establishment_settings

establishment_subscriptions
establishment_payments

establishment_categories
menu_categories

products
product_option_groups
product_options

inventory_movements

addresses

orders
order_items
order_item_options
order_status_history

drivers
deliveries
delivery_confirmation

driver_receivables
driver_payment_batches

notifications

establishment_features

audit_logs
refresh_tokens
```

---

# 6. IDs

Preferir UUID/GUID para entidades públicas importantes.

Não utilizar ID incremental como mecanismo de segurança.

---

# 7. Valores monetários

C#:

```text
decimal
```

PostgreSQL:

```text
numeric
```

Nunca usar:

```text
float
double
```

para valores financeiros.

---

# 8. Datas e timezone

Persistência:

```text
UTC
```

Apresentação:

```text
America/Sao_Paulo
```

---

# 9. Usuários

Roles iniciais:

```text
SUPER_ADMIN
CUSTOMER
DRIVER
ESTABLISHMENT_ADMIN
```

Preparar arquitetura para:

```text
ESTABLISHMENT_MANAGER
ATTENDANT
STOCK_OPERATOR
```

---

# 10. Autenticação

Implementar:

```text
POST /api/auth/register/customer
POST /api/auth/register/driver
POST /api/auth/login
POST /api/auth/refresh
POST /api/auth/logout
POST /api/auth/forgot-password
POST /api/auth/reset-password
```

Requisitos:

- hash seguro;
- refresh token rotativo;
- expiração;
- revogação;
- rate limiting;
- recuperação por token de uso único;
- token de recuperação com expiração;
- logs adequados.

---

# 11. Cadastro do cliente

Campos:

```text
name
email
phone
password
```

Cidade pode ser selecionada depois.

Endereço não precisa ser obrigatório no cadastro.

---

# 12. Cadastro do entregador

Campos:

```text
name
cpf
phone
email
password
city_id
vehicle_type
pix_key_type
pix_key
```

Status inicial:

```text
PENDING_APPROVAL
```

Super Admin aprova.

---

# 13. Status do entregador

```text
OFFLINE
AVAILABLE
BUSY
BLOCKED
```

Regras:

```text
PENDING_APPROVAL
→ não pode trabalhar

APPROVED + ONLINE
→ AVAILABLE

ACCEPT DELIVERY
→ BUSY

CONFIRM DELIVERY
→ AVAILABLE
```

Na PedeLocal:

```text
1 entregador = no máximo 1 entrega ativa
```

---

# 14. Cidades

Entidade:

```text
City
- Id
- Name
- State
- IsActive
```

Endpoints:

```text
GET /api/cities
POST /api/admin/cities
PUT /api/admin/cities/{id}
PATCH /api/admin/cities/{id}/status
```

Somente Super Admin cria/edita cidade.

---

# 15. Estabelecimentos

Campos principais:

```text
id
city_id
name
slug
document
phone
email
category_id
address_id
operational_status
commercial_status
created_at
updated_at
```

Separar:

```text
operational_status
```

de:

```text
commercial_status
```

---

# 16. Status operacional

```text
OPEN
CLOSED_MANUAL
CLOSED_SCHEDULE
PAUSED
```

---

# 17. Status comercial

```text
ACTIVE
OVERDUE_GRACE
SUSPENDED_PAYMENT
SUSPENDED_ADMIN
INACTIVE
```

---

# 18. Cadastro de estabelecimento

Somente Super Admin na PedeLocal.

Endpoints:

```text
POST /api/admin/establishments
GET /api/admin/establishments
GET /api/admin/establishments/{id}
PUT /api/admin/establishments/{id}
PATCH /api/admin/establishments/{id}/status
```

Criar usuário administrador associado ao estabelecimento.

---

# 19. Busca do cliente

Endpoints públicos autenticados:

```text
GET /api/establishments?cityId=
GET /api/establishments/{id}
GET /api/establishments/{id}/products
```

Filtrar apenas estabelecimentos:

- da cidade;
- habilitados;
- visíveis;
- não suspensos para novos pedidos.

---

# 20. Categorias

Separar:

## Categoria do estabelecimento

```text
Mercado
Lanchonete
Adega
Pizzaria
Padaria
Açougue
```

## Categoria do cardápio

```text
Combos
Lanches
Bebidas
Limpeza
Frios
```

---

# 21. Produtos

Campos:

```text
id
establishment_id
menu_category_id
name
description
image_url
price
promotional_price
stock_mode
stock_quantity
is_available
created_at
updated_at
```

---

# 22. Estoque

Modo:

```text
UNCONTROLLED
CONTROLLED
```

Exemplo:

```text
X-Bacon
UNCONTROLLED
```

```text
Coca-Cola 2L
CONTROLLED
quantity = 15
```

---

# 23. Movimentação de estoque

Tabela:

```text
inventory_movements

id
product_id
type
quantity
previous_quantity
new_quantity
order_id
user_id
reason
created_at
```

Tipos:

```text
ENTRY
SALE
ADJUSTMENT
LOSS
RETURN
CANCELLATION_RESTORE
```

Nunca alterar estoque sem criar movimentação.

---

# 24. Momento da baixa

PedeLocal:

```text
pedido aceito pelo estabelecimento
→ baixar estoque
```

Se cancelar depois:

```text
→ restaurar
→ criar CANCELLATION_RESTORE
```

---

# 25. Produtos com adicionais

Modelar:

```text
product_option_groups
product_options
```

Exemplo:

```text
Grupo: Adicionais
min = 0
max = 3

Bacon +4
Queijo +3
Ovo +2
```

Outro exemplo:

```text
Grupo: Tamanho
required = true
min = 1
max = 1
```

---

# 26. Endereços

Tabela:

```text
addresses

id
city_id
type
label

street
number
district
complement
reference
zip_code

property_name
road
kilometer

latitude
longitude
google_place_id

created_at
updated_at
```

Tipo:

```text
URBAN
RURAL
```

---

# 27. Endereço rural

Para `RURAL`:

Obrigatório:

```text
latitude
longitude
reference
city_id
```

Opcional:

```text
property_name
road
kilometer
```

---

# 28. Endereço do estabelecimento

Todo estabelecimento deve ter:

```text
texto
+
latitude
+
longitude
```

---

# 29. Taxa de entrega

Cada estabelecimento configura.

Tabela:

```text
delivery_pricing_settings
```

Campos:

```text
establishment_id

urban_mode
urban_fixed_fee
urban_price_per_km
urban_min_fee

rural_mode
rural_fixed_fee
rural_price_per_km
rural_min_fee

max_distance_km
```

Modes:

```text
FIXED
PER_KM
```

---

# 30. Fórmula

Por km:

```text
fee = max(distance_km * price_per_km, minimum_fee)
```

Exemplo:

```text
6 km
R$ 3/km
mínimo R$ 20

18 → cobra 20
```

---

# 31. Modelo misto

Não precisa de enum MIXED.

Exemplo:

```text
urban_mode = FIXED
rural_mode = PER_KM
```

Isso já representa modelo misto.

---

# 32. Distância máxima

Se:

```text
distance > max_distance_km
```

retornar:

```text
DELIVERY_NOT_AVAILABLE
```

---

# 33. Snapshot da entrega

Ao criar pedido salvar:

```text
delivery_fee
calculated_distance
pricing_rule_snapshot
```

Mudanças futuras de taxa não alteram pedido antigo.

---

# 34. Pedidos

Tabelas:

```text
orders
order_items
order_item_options
order_status_history
```

Status:

```text
PENDING
ACCEPTED
PREPARING
READY
WAITING_DRIVER
DRIVER_ASSIGNED
PICKED_UP
OUT_FOR_DELIVERY
DELIVERED
CANCELLED
```

---

# 35. Snapshot de produto

`order_items` deve salvar:

```text
product_name
unit_price
quantity
options_snapshot
```

Nunca depender do produto atual para histórico.

---

# 36. Criação de pedido

Validar:

- cliente autenticado;
- estabelecimento ativo;
- cidade válida;
- produto pertence ao estabelecimento;
- produto disponível;
- estoque;
- quantidade;
- adicionais;
- endereço;
- taxa;
- forma de pagamento.

---

# 37. Formas de pagamento

PedeLocal:

```text
PIX
CASH
DEBIT_ON_DELIVERY
CREDIT_ON_DELIVERY
```

Campos:

```text
payment_method
payment_status
change_for_amount
```

Não armazenar cartão.

---

# 38. Fluxo do comércio

Endpoints:

```text
GET /api/merchant/orders
GET /api/merchant/orders/{id}

POST /api/merchant/orders/{id}/accept
POST /api/merchant/orders/{id}/preparing
POST /api/merchant/orders/{id}/ready
POST /api/merchant/orders/{id}/cancel
```

Ownership obrigatório.

---

# 39. Entregas disponíveis

Quando:

```text
order = READY
```

criar/ativar:

```text
delivery = AVAILABLE
```

para entregadores aprovados e disponíveis da mesma cidade.

---

# 40. Aceitação concorrente

Problema crítico.

Dois entregadores podem tentar aceitar simultaneamente.

Implementar atomicidade.

Condição:

```text
delivery.driver_id IS NULL
driver.status == AVAILABLE
delivery.status == AVAILABLE
```

Ao aceitar:

```text
delivery.driver_id = driver.id
driver.status = BUSY
delivery.status = ASSIGNED
```

Tudo em transação.

---

# 41. Teste de concorrência

Criar teste real:

```text
Driver A
Driver B
```

aceitam ao mesmo tempo.

Resultado esperado:

```text
1 sucesso
1 conflito
```

Nunca 2 sucessos.

---

# 42. Fluxo entregador

Endpoints:

```text
GET /api/driver/deliveries/available

GET /api/driver/deliveries/current

POST /api/driver/deliveries/{id}/accept
POST /api/driver/deliveries/{id}/pickup
POST /api/driver/deliveries/{id}/out-for-delivery
POST /api/driver/deliveries/{id}/confirm
```

---

# 43. Código da entrega

Tabela:

```text
delivery_confirmation

delivery_id
code_hash
expires_at
attempts
max_attempts
confirmed_at
```

Gerar:

```text
4 ou 6 dígitos
```

com gerador criptograficamente seguro.

Armazenar hash.

---

# 44. Regras do código

- uso único;
- máximo de tentativas;
- expiração;
- apenas entregador responsável pode confirmar;
- só funciona na etapa correta.

Código correto:

```text
delivery = COMPLETED
order = DELIVERED
driver = AVAILABLE
completed_at = now
```

---

# 45. Financeiro do entregador

Tabela:

```text
driver_receivables

id
delivery_id
driver_id
establishment_id
amount
status
created_at
```

Status:

```text
PENDING
PAYMENT_REPORTED
RECEIVED
DISPUTED
```

---

# 46. Geração de recebível

Somente depois de:

```text
DELIVERY COMPLETED
```

Criar uma única vez.

Garantir idempotência.

---

# 47. Pagamento manual do entregador

Estabelecimento pode agrupar:

```text
5 entregas
R$ 35
```

Criar:

```text
driver_payment_batches
```

Fluxo:

```text
PENDING
→ PAYMENT_REPORTED
→ RECEIVED
```

---

# 48. Mensalidade do estabelecimento

Cobrança é externa à plataforma.

Tabela:

```text
establishment_subscriptions
```

Campos:

```text
id
establishment_id
monthly_amount
billing_day
due_date
grace_days
payment_status
created_at
updated_at
```

Padrão:

```text
grace_days = 5
```

---

# 49. Regra dos 5 dias

Exemplo:

```text
due_date = 10/10

11/10 = atraso 1
12/10 = atraso 2
13/10 = atraso 3
14/10 = atraso 4
15/10 = atraso 5
16/10 = suspender
```

Status:

```text
ACTIVE
→ OVERDUE_GRACE
→ SUSPENDED_PAYMENT
```

---

# 50. Suspensão

Ao suspender:

NÃO:

```text
DELETE establishment
DELETE products
DELETE orders
```

FAZER:

```text
commercial_status = SUSPENDED_PAYMENT
```

Bloquear:

```text
novos pedidos
```

Permitir concluir:

```text
pedidos já em andamento
```

---

# 51. Reativação

Super Admin registra pagamento.

Depois:

```text
commercial_status = ACTIVE
```

Dados permanecem intactos.

Registrar auditoria.

---

# 52. Pagamentos do estabelecimento

Tabela:

```text
establishment_payments

id
establishment_id
subscription_id
amount
payment_method
paid_at
registered_by_user_id
notes
receipt_url
```

---

# 53. Job de cobrança

Criar background job diário.

Responsável por:

- identificar vencidos;
- entrar em grace;
- avisar;
- suspender após 5 dias;
- nunca duplicar ação.

Deve ser idempotente.

Tecnologia:

```text
Hangfire
ou Quartz.NET
```

Escolher uma.

---

# 54. Super Admin

API deve suportar:

```text
cidades
estabelecimentos
assinaturas
pagamentos
entregadores
clientes
pedidos
entregas
categorias
features
auditoria
```

---

# 55. Feature flags por estabelecimento

Tabela:

```text
establishment_features
```

Campos:

```text
establishment_id
feature_key
enabled
settings_json
```

Exemplos:

```text
CUSTOM_REPORT
WHOLESALE_MODE
ADVANCED_STOCK
SPECIAL_ORDER_FLOW
```

Não fazer:

```text
if establishment_id == X
```

espalhado pelo sistema.

---

# 56. SignalR

Implementar hubs para:

- pedido novo;
- status do pedido;
- pedido pronto;
- entrega disponível;
- entrega aceita;
- entrega concluída;
- suspensão.

---

# 57. Firebase

Preparar abstração:

```text
INotificationService
```

Implementação:

```text
FirebaseNotificationService
```

Eventos:

```text
ORDER_CREATED
ORDER_ACCEPTED
ORDER_READY
DELIVERY_AVAILABLE
DELIVERY_ACCEPTED
ORDER_DELIVERED
PAYMENT_REPORTED
SUBSCRIPTION_WARNING
```

---

# 58. Google Maps

Backend não precisa implementar navegação.

Persistir coordenadas.

Preparar serviço para cálculo de distância futuro.

Criar abstração:

```text
IRouteDistanceService
```

PedeLocal pode usar implementação simples/mock se API externa ainda não estiver configurada.

Não acoplar regra de taxa diretamente a Google.

---

# 59. Segurança

Revisar OWASP Web e API.

Obrigatório testar:

```text
Broken Access Control
IDOR/BOLA
Mass Assignment
Injection
JWT
Refresh Token
Rate Limit
File Upload
Secrets
Sensitive Data Exposure
Race Conditions
```

---

# 60. Ownership

Exemplos:

```text
Cliente A
não lê pedido B
```

```text
Comércio A
não lê pedido do Comércio B
```

```text
Entregador A
não altera entrega B
```

---

# 61. Upload

Imagens e comprovantes.

Validar:

- MIME;
- tamanho;
- extensão;
- nome;
- acesso.

Não salvar binário no PostgreSQL.

Usar storage e guardar URL.

---

# 62. Auditoria

Tabela:

```text
audit_logs

id
actor_user_id
action
entity_type
entity_id
ip
user_agent
metadata
created_at
```

Eventos:

```text
PRICE_CHANGED
STOCK_ADJUSTED
ORDER_CANCELLED
DRIVER_APPROVED
ESTABLISHMENT_SUSPENDED
ESTABLISHMENT_REACTIVATED
PAYMENT_REGISTERED
FEATURE_CHANGED
```

---

# 63. Logs

Separar:

```text
application logs
security logs
audit logs
```

Não logar:

```text
password
full JWT
secret
card data
```

---

# 64. LGPD — requisitos técnicos mínimos

Preparar:

- exclusão de conta;
- correção de dados;
- minimização;
- auditoria;
- controle de acesso;
- retenção;
- anonimização quando aplicável;
- exportação futura.

Endpoints:

```text
DELETE /api/me/account
GET /api/me/data
PUT /api/me/profile
```

A exclusão deve respeitar retenções legítimas.

---

# 65. Rate limiting

Aplicar especialmente:

```text
login
forgot-password
reset-password
delivery-code
register
```

---

# 66. CORS

Somente origens autorizadas.

Não usar:

```text
AllowAnyOrigin
```

em produção sem justificativa.

---

# 67. Swagger

Disponível:

```text
development
staging
```

Produção:

proteger ou desabilitar conforme decisão.

---

# 68. OpenAPI / contrato simples da API

Muito importante.

Gerar contrato completo para o Cursor.

Cada endpoint deve documentar:

- request;
- response;
- status codes;
- auth;
- errors.

---

# 69. Padrão de erros

Criar resposta consistente:

```json
{
  "code": "ORDER_NOT_FOUND",
  "message": "Pedido não encontrado.",
  "traceId": "..."
}
```

Não retornar stack trace.

---

# 70. Status HTTP

Usar corretamente:

```text
200
201
204
400
401
403
404
409
422
429
500
```

---

# 71. Idempotência

Garantir em:

- confirmar entrega;
- cancelar;
- registrar pagamento;
- gerar recebível;
- aceitar pedido.

Cliques duplos não duplicam efeito.

---

# 72. Backups

Documentar estratégia.

PedeLocal:

- backup diário;
- retenção;
- teste de restore.

---

# 73. Docker

Criar:

```text
Dockerfile
docker-compose.yml
.env.example
```

Serviços:

```text
api
postgres
```

Opcional:

```text
pgadmin
```

---

# 74. Seed

Criar seed dev:

```text
Super Admin
Guaimbê
Júlio Mesquita
Getulina
1 estabelecimento exemplo
1 cliente exemplo
1 entregador exemplo
```

Nunca seed de credenciais fracas em produção.

---

# 75. Testes unitários

Cobrir:

- taxa urbana;
- taxa rural;
- mínimo;
- estoque;
- grace period;
- status;
- código;
- reativação;
- regras de pedido.

---

# 76. Testes de integração

Cobrir:

- autenticação;
- pedido;
- banco;
- estoque;
- entregador;
- entrega;
- pagamento manual;
- assinatura;
- autorização.

---

# 77. QA de segurança automatizado

Criar testes:

```text
customer cannot access another customer order
merchant cannot access another merchant order
driver cannot confirm another driver delivery
suspended establishment cannot receive new order
```

---

# 78. Teste E2E conceitual backend

Criar automação de API:

```text
1 criar cliente
2 listar cidade
3 listar loja
4 criar pedido
5 comércio aceitar
6 preparar
7 pronto
8 entregador aceitar
9 pickup
10 confirmar código
11 validar delivered
12 validar driver available
13 validar receivable criado
```

---

# 79. CI

GitHub Actions.

Pipeline:

```text
dotnet restore
dotnet build
dotnet test
```

Depois:

```text
docker build
```

---

# 80. Documentação obrigatória para o Cursor

Ao finalizar backend, criar:

```text
docs/API_CONTRACT.md
docs/FRONTEND_GUIDE.md
docs/AUTH_FLOW.md
docs/ORDER_FLOW.md
docs/DELIVERY_FLOW.md
docs/ERROR_CODES.md
```

---

# 81. FRONTEND_GUIDE.md

Deve explicar:

```text
base URL
auth
refresh token
roles
endpoints
pagination
errors
SignalR
FCM
status enums
money
dates
address model
```

---

# 82. Não implementar visual agora

Nesta etapa:

NÃO construir:

- telas Flutter completas;
- design final;
- dashboard visual final;
- componentes web definitivos.

Permitido apenas:

- Swagger;
- pequenas páginas técnicas se estritamente necessárias;
- exemplos de payload.

---

# 83. Ordem de desenvolvimento Claude

Executar nesta ordem:

```text
01 setup
02 database
03 identity
04 cities
05 establishments
06 catalog
07 inventory
08 addresses
09 delivery pricing
10 orders
11 drivers
12 deliveries
13 delivery code
14 finance
15 subscriptions
16 admin
17 SignalR
18 notifications
19 security
20 tests
21 Docker
22 CI
23 docs frontend
```

Não pular testes críticos.

---

# 84. Prompt principal para o Claude Code

Use este documento como fonte principal.

```text
Leia integralmente 01_claude_backend_completo.md.

Você será responsável por implementar todo o backend da plataforma.

Regras:

1. siga a ordem definida na documentação;
2. use .NET 10 + PostgreSQL;
3. mantenha monólito modular;
4. não construa o frontend visual;
5. implemente um módulo por vez;
6. rode build e testes ao terminar cada módulo;
7. corrija os erros antes de avançar;
8. faça commits claros se tiver permissão;
9. nunca exponha secrets;
10. mantenha documentação atualizada;
11. não invente regra de negócio fora deste documento;
12. segurança e autorização devem estar no backend;
13. crie testes para regras críticas;
14. gere contrato OpenAPI correto;
15. ao final, gere documentação completa para o frontend.

Ao encontrar ambiguidade:
- escolha a solução mais simples e segura;
- registre a decisão em docs/DECISIONS.md;
- não interrompa o trabalho por detalhes não críticos.

Meta final:
deixar uma API funcional e testada pronta para o Cursor construir o Flutter e os painéis web.
```

---

# 85. Critério de conclusão

Claude só encerra quando:

```text
[ ] solution compila
[ ] migrations funcionam
[ ] Docker sobe
[ ] autenticação funciona
[ ] cidades funcionam
[ ] estabelecimentos funcionam
[ ] produtos funcionam
[ ] estoque funciona
[ ] endereço funciona
[ ] taxa funciona
[ ] pedidos funcionam
[ ] entregadores funcionam
[ ] concorrência funciona
[ ] código funciona
[ ] financeiro funciona
[ ] mensalidade funciona
[ ] suspensão funciona
[ ] reativação funciona
[ ] autorização funciona
[ ] testes passam
[ ] OpenAPI completo
[ ] docs frontend completas
```

---

# 86. Resultado esperado

Ao final da fase Claude deve existir:

```text
BACKEND COMPLETO
+
BANCO COMPLETO
+
REGRAS COMPLETAS
+
SEGURANÇA
+
TESTES
+
DOCKER
+
OPENAPI
+
DOCUMENTAÇÃO PARA O CURSOR
```

Só depois iniciar a fase visual.
