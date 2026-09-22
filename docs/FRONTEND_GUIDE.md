# Guia para o frontend (Cursor)

Este documento é o ponto de partida para construir o Flutter (apps) e os
painéis web sobre esta API. Para o contrato completo endpoint-a-endpoint, use
o Scalar/OpenAPI gerado (ver abaixo) — este guia cobre as convenções
transversais que não aparecem no schema.

## Base URL

- Desenvolvimento local: `https://localhost:5001` (ou a porta que o
  `dotnet run` / `docker compose` expuser — `8080` dentro do container).
- Documentação interativa (Development/Staging apenas):
  `{baseUrl}/scalar/v1`. O documento OpenAPI cru fica em `{baseUrl}/openapi/v1.json`.
- Produção: a definir pelo time de infra (Swagger fica desabilitado nesse
  ambiente por padrão — ver seção 67 da spec original).

## Autenticação

Ver `docs/AUTH_FLOW.md` para o fluxo completo. Resumo:

```text
Authorization: Bearer <accessToken>
```

Access token expira em 15 min (padrão) — implemente refresh automático nas
respostas `401` (uma única tentativa de retry após `POST /api/auth/refresh`;
se o refresh também falhar, redirecione para o login).

## Papéis e áreas da API

```text
/api/auth/*          -- público (registro/login)
/api/cities           -- público (lista de cidades ativas)
/api/establishment-categories -- público

/api/me/*             -- qualquer usuário autenticado
/api/orders            -- CUSTOMER
/api/establishments*   -- CUSTOMER (busca/cardápio)

/api/merchant/*        -- staff do estabelecimento (qualquer papel vinculado
                           via establishment_users) OU SUPER_ADMIN

/api/driver/*           -- DRIVER

/api/admin/*             -- SUPER_ADMIN
```

Não existe um endpoint "sou eu mesmo dono deste recurso?" — a API sempre
retorna `403` para tentativas de acessar dados de outro usuário/estabelecimento
sem revelar se o recurso existe (ver `docs/ERROR_CODES.md`).

## Paginação

Endpoints de listagem grande aceitam `page` (1-based, padrão 1) e
`pageSize` (padrão 20, máx 100) via query string, e retornam:

```json
{
  "items": [...],
  "page": 1,
  "pageSize": 20,
  "totalCount": 137,
  "totalPages": 7
}
```

## Erros

Ver `docs/ERROR_CODES.md` — formato padrão, catálogo completo de `code`s.

## Idempotência

Ações que não devem duplicar efeito em cliques duplos/retry já são
idempotentes no backend (aceitar pedido, cancelar, confirmar entrega,
registrar pagamento, gerar recebível). Ainda assim, para **criar** um
pedido, envie um `idempotencyKey` (um GUID gerado no cliente) — protege
contra duplo-submit em conexões instáveis.

## SignalR

Hub: `{baseUrl}/hubs/pedelocal` (WebSockets/SSE via
`@microsoft/signalr` ou equivalente Flutter).

Autenticação: como WebSocket não permite header customizado no handshake do
browser, passe o access token via query string:

```text
{baseUrl}/hubs/pedelocal?access_token=<accessToken>
```

O servidor decide sozinho, a partir do token, em quais grupos a conexão
entra (cliente nunca escolhe grupo) — não existe um método `Subscribe` para
chamar.

Referência completa (grupos, todos os eventos e payloads, notificações push,
confiabilidade e o comportamento esperado de reconexão) em
`docs/REALTIME_EVENTS.md`. Resumo dos eventos que o cliente deve escutar
(`connection.on("EventName", handler)`):

| Evento | Payload | Quem recebe |
|---|---|---|
| `OrderCreated` | `{ orderId }` | staff do estabelecimento |
| `OrderStatusChanged` | `{ orderId, status }` | cliente do pedido + staff do estabelecimento |
| `DeliveryAvailable` | `{ deliveryId }` | entregadores aprovados/disponíveis da cidade |
| `DeliveryAccepted` | `{ deliveryId, driverId }` (staff) / `{ deliveryId }` sem `driverId` (demais entregadores da cidade) | staff do estabelecimento + entregadores da cidade (só para removerem da lista de disponíveis) |
| `DeliveryCompleted` | `{ orderId }` | cliente do pedido + staff do estabelecimento |
| `EstablishmentSuspended` | `{ establishmentId }` | staff do estabelecimento |

Reconecte com backoff exponencial; ao reconectar, sempre busque o estado
atual via REST (`GET /api/orders/{id}` etc.) — eventos perdidos durante a
desconexão não são reenviados.

## Push notifications (FCM)

Referência completa em `docs/PUSH_NOTIFICATIONS.md`. Resumo:

```http
PUT /api/me/device-tokens
{ "token": "<token do Firebase Cloud Messaging>", "platform": "Ios" }   -- ou "Android" | "Web"

DELETE /api/me/device-tokens
{ "token": "<token do Firebase Cloud Messaging>" }
```

Chame `PUT` sempre que o token do FCM for gerado/rotacionado pelo app —
idempotente, então pode ser chamado em todo start do app sem se preocupar
com duplicar. Um mesmo usuário pode ter vários tokens simultâneos (um por
dispositivo); chame `DELETE` no logout/desinstalação para parar de receber
push naquele aparelho especificamente.

Cada evento que gera push também vira uma notificação em
`GET /api/me/notifications`, mesmo sem nenhum token registrado ou com o
provedor fora do ar — o histórico nunca depende do push ter funcionado.

O app Flutter (`apps/mobile`) registra/remove o device token nesses
endpoints após login e no logout. Setup do projeto Firebase no aparelho:
`apps/mobile/README.md`. O backend só envia push de verdade com
`Firebase:Enabled=true` e service account — `docs/PUSH_NOTIFICATIONS.md`.

## Enums de status (nomes exatos usados na API — sempre em `PascalCase`)

```text
Order.status:        Pending, Accepted, Preparing, Ready, WaitingDriver,
                      DriverAssigned, PickedUp, OutForDelivery, Delivered,
                      PickupCompleted, Cancelled
Order.type:           Delivery, Pickup
Order.paymentMethod:  Pix, Cash, DebitOnDelivery, CreditOnDelivery
Order.paymentStatus:  Pending, Paid, NotApplicable

Delivery.status:      Available, Assigned, PickedUp, OutForDelivery, Completed, Cancelled
DeliveryOccurrence.type: CustomerNotFound, CustomerNotResponding, WrongAddress, PaymentProblem, VehicleProblem, Other

Driver.onboardingStatus: PendingDocuments, UnderReview, Approved, Rejected
Driver.eligibilityStatus: Eligible, PendingDocuments, UnderReview, Rejected, Blocked, DocumentExpired   -- computado, ver docs/DRIVER_ONBOARDING.md
Driver.status:         Offline, Available, Busy, Blocked
Driver.vehicleType:    Bike, Motorcycle, Car
Driver.pixKeyType:     Cpf, Email, Phone, Random

Establishment.operationalStatus: Open, ClosedManual, ClosedSchedule, Paused
Establishment.commercialStatus:  Active, OverdueGrace, SuspendedPayment, SuspendedAdmin, Inactive
Establishment.pauseReason:       HighDemand, NoDriverAvailable, TemporaryClosure, Other   -- null quando não pausado

Product.stockMode:    Uncontrolled, Controlled
Address.type:         Urban, Rural

DriverReceivable.status:    Pending, PaymentReported, Received, Disputed
DriverPaymentBatch.status:  Pending, PaymentReported, Received

EstablishmentSubscription.paymentStatus: Active, OverdueGrace, SuspendedPayment
```

## Dinheiro

Todos os valores monetários são `decimal` no backend / `number` no JSON,
nunca string. Formate para exibição no cliente (nunca confie em ponto
flutuante para somas — se precisar somar no cliente, use um tipo decimal
apropriado, não `float`/`double`).

## Datas

Toda data/hora da API é UTC, formato ISO 8601
(`"2026-09-07T18:30:00Z"`). Converta para `America/Sao_Paulo` na
apresentação — o backend nunca faz essa conversão por você, exceto
internamente para decidir o "dia" nas regras de assinatura/grace period.

## Modelo de endereço

```json
{
  "id": "...",
  "cityId": "...",
  "type": "Urban",              // ou "Rural"
  "label": "Casa",
  "street": "Rua A", "number": "100", "district": "Centro",
  "complement": null, "zipCode": "17370000",
  "reference": null,
  "propertyName": null, "road": null, "kilometer": null,
  "latitude": -21.850000, "longitude": -49.900000,
  "googlePlaceId": null
}
```

- `type = Urban`: `street`/`number`/`district` obrigatórios; campos rurais
  ficam `null`.
- `type = Rural`: `reference` obrigatório; `propertyName`/`road`/`kilometer`
  opcionais; campos urbanos ficam `null`.
- `latitude`/`longitude` **sempre obrigatórios**, para ambos os tipos —
  usados no cálculo de distância/taxa de entrega. Se o app não tiver um
  seletor de mapa ainda, use a geocodificação do CEP/endereço como
  fallback, mas nunca envie `0,0`.

## Upload de imagens

```http
POST /api/uploads/image   (multipart/form-data, campo "file")
201 Created
{ "url": "/uploads/<guid>.jpg" }
```

Aceita apenas JPG/PNG/WEBP, até 5MB, validado por conteúdo real do arquivo
(não confia na extensão nem no `Content-Type` declarado). Use a `url`
retornada diretamente em `imageUrl` do produto ou `receiptUrl` do pagamento.

## Rate limits

Login/registro/refresh/forgot-password/reset-password/confirmação de
entrega/upload compartilham uma política mais restritiva (padrão: 5
requisições/60s por IP). Excedido → `429`. Demais endpoints usam o limite
global (padrão: 100 requisições/60s por IP). Ambos configuráveis via
`RateLimiting:*` no `appsettings`.
