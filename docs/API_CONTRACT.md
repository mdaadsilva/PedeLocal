# Contrato da API — índice de endpoints

O contrato completo (request/response schemas, status codes por endpoint) é
gerado automaticamente a partir do código em `{baseUrl}/openapi/v1.json`,
navegável em `{baseUrl}/scalar/v1` (Development/Staging). Este documento é o
índice de navegação — use-o para achar rapidamente o endpoint certo, depois
confira o schema exato no Scalar.

Convenções gerais, enums e o formato de erro padrão estão em
`docs/FRONTEND_GUIDE.md` e `docs/ERROR_CODES.md`.

## Auth — `/api/auth` (público, rate-limited)

| Método | Rota |
|---|---|
| POST | `/register/customer` |
| POST | `/register/driver` |
| POST | `/login` |
| POST | `/refresh` |
| POST | `/logout` |
| POST | `/forgot-password` |
| POST | `/reset-password` |

## Minha conta — `/api/me` (autenticado)

| Método | Rota |
|---|---|
| GET/PUT | `/profile` |
| PUT | `/fcm-token` |
| GET | `/notifications` |
| POST | `/notifications/{id}/read` |
| GET | `/data` (export LGPD) |
| DELETE | `/account` (anonimiza + desativa) |

## Endereços — `/api/me/addresses` (autenticado)

| Método | Rota |
|---|---|
| GET/POST | `/` |
| PUT/DELETE | `/{id}` |

## Cidades

| Método | Rota | Quem |
|---|---|---|
| GET | `/api/cities` | público |
| GET/POST | `/api/admin/cities` | SUPER_ADMIN |
| PUT/PATCH `/{id}`, `/{id}/status` | `/api/admin/cities` | SUPER_ADMIN |

## Estabelecimentos

| Método | Rota | Quem |
|---|---|---|
| GET | `/api/establishments?cityId=` | CUSTOMER — busca filtrada (visível, não suspenso) |
| GET | `/api/establishments/{id}` | CUSTOMER |
| GET | `/api/establishments/{id}/products` | CUSTOMER — catálogo público (só disponíveis) |
| GET | `/api/establishment-categories` | público |
| GET/POST | `/api/admin/establishments` | SUPER_ADMIN |
| GET/PUT `/{id}`, PATCH `/{id}/status` | `/api/admin/establishments` | SUPER_ADMIN |

## Catálogo (comerciante) — `/api/merchant/establishments/{establishmentId}`

| Método | Rota |
|---|---|
| GET/POST | `/menu-categories` |
| PUT | `/menu-categories/{categoryId}` |
| GET/POST | `/products` |
| GET/PUT | `/products/{productId}` |
| PATCH | `/products/{productId}/availability` |
| POST | `/products/{productId}/inventory/adjust` |
| GET | `/products/{productId}/inventory/movements` |
| GET/PUT | `/delivery-pricing` |

## Pedidos

| Método | Rota | Quem |
|---|---|---|
| POST | `/api/orders/preview` | CUSTOMER — mesmo corpo de `/api/orders`, não persiste |
| POST | `/api/orders` | CUSTOMER |
| GET | `/api/orders?page=` | CUSTOMER |
| GET | `/api/orders/{id}` | CUSTOMER |
| GET | `/api/orders/{id}/delivery-code` | CUSTOMER — só o dono do pedido |
| GET | `/api/merchant/establishments/{id}/orders?status=&page=` | staff |
| GET | `/api/merchant/establishments/{id}/orders/{orderId}` | staff |
| POST | `.../orders/{orderId}/accept` \| `/preparing` \| `/ready` \| `/cancel` | staff |
| POST | `.../orders/{orderId}/complete-pickup` | staff — só `orderType = Pickup` |
| GET | `/api/admin/orders?establishmentId=&status=` | SUPER_ADMIN |

## Entregadores

| Método | Rota | Quem |
|---|---|---|
| GET | `/api/driver/me` | DRIVER |
| POST | `/api/driver/me/status/online` \| `/offline` | DRIVER |
| GET | `/api/admin/drivers?approvalStatus=` | SUPER_ADMIN |
| POST | `/api/admin/drivers/{id}/approve` \| `/reject` \| `/block` \| `/unblock` | SUPER_ADMIN |

## Entregas

| Método | Rota | Quem |
|---|---|---|
| GET | `/api/driver/deliveries/available` | DRIVER |
| GET | `/api/driver/deliveries/current` | DRIVER |
| GET | `/api/driver/deliveries/{id}/details` | DRIVER — só o entregador vinculado à entrega |
| POST | `/api/driver/deliveries/{id}/accept` \| `/pickup` \| `/out-for-delivery` \| `/confirm` | DRIVER |
| GET | `/api/admin/deliveries?cityId=&status=` | SUPER_ADMIN |

## Financeiro (repasse a entregadores)

| Método | Rota | Quem |
|---|---|---|
| GET | `/api/merchant/establishments/{id}/driver-payments/receivables/pending?driverId=` | staff |
| POST | `.../driver-payments/batches` | staff |
| POST | `.../driver-payments/batches/{batchId}/report` | staff |
| GET | `/api/driver/receivables` | DRIVER |
| GET | `/api/driver/payment-batches` | DRIVER |
| POST | `/api/driver/payment-batches/{batchId}/confirm` | DRIVER |

## Assinatura do estabelecimento (mensalidade)

| Método | Rota | Quem |
|---|---|---|
| GET/PUT | `/api/admin/establishments/{id}/subscription` | SUPER_ADMIN |
| GET/POST | `/api/admin/establishments/{id}/subscription/payments` | SUPER_ADMIN |

## Admin geral

| Método | Rota |
|---|---|
| GET | `/api/admin/customers?page=` |
| GET | `/api/admin/audit-logs?entityType=&actorUserId=&page=` |
| GET | `/api/admin/establishments/{id}/features` |
| PUT | `/api/admin/establishments/{id}/features/{featureKey}` |

## Upload

| Método | Rota |
|---|---|
| POST | `/api/uploads/image` (multipart) |

## Tempo real

| Protocolo | Rota |
|---|---|
| SignalR (WebSocket) | `/hubs/pedelocal?access_token=<jwt>` |
