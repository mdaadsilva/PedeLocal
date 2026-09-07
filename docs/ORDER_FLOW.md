# Fluxo de pedidos

## Máquina de estados

```text
PENDING
  → ACCEPTED        (comércio aceita — baixa estoque automaticamente)
  → PREPARING        (comércio marca em preparo)
  → READY            (comércio marca pronto — cria a Delivery + código de confirmação)
  → WAITING_DRIVER    (transição automática logo após READY)
  → DRIVER_ASSIGNED   (um entregador aceitou a entrega)
  → PICKED_UP         (entregador retirou no estabelecimento)
  → OUT_FOR_DELIVERY  (entregador saiu para entregar)
  → DELIVERED         (código de confirmação validado)

PENDING/ACCEPTED/PREPARING/READY/WAITING_DRIVER → CANCELLED (só o comércio, ver abaixo)
```

`WAITING_DRIVER` → `DRIVER_ASSIGNED` → `PICKED_UP` → `OUT_FOR_DELIVERY` →
`DELIVERED` são disparadas pelas ações do **entregador** (ver
`docs/DELIVERY_FLOW.md`), não pelo comércio.

## Criar pedido (cliente)

```http
POST /api/orders
{
  "establishmentId": "...",
  "addressId": "...",
  "paymentMethod": "Pix",              // Pix | Cash | DebitOnDelivery | CreditOnDelivery
  "changeForAmount": null,              // só relevante para Cash
  "items": [
    {
      "productId": "...",
      "quantity": 2,
      "options": [ { "productOptionId": "..." } ]
    }
  ],
  "idempotencyKey": "uuid-gerado-pelo-cliente"  // opcional, mas recomendado
}
```

Validações do backend, nesta ordem — qualquer falha aborta o pedido inteiro
(nada é criado):

1. Estabelecimento existe, aceita pedidos (`EstablishmentSettings.acceptsNewOrders`),
   não está suspenso comercialmente e está `OPEN` operacionalmente.
2. Endereço de entrega pertence ao cliente autenticado e é da **mesma
   cidade** do estabelecimento.
3. Cada produto pertence ao estabelecimento, está disponível
   (`isAvailable`), e — se `stockMode = Controlled` — há estoque suficiente
   somando todas as linhas do mesmo produto no pedido.
4. Cada grupo de adicionais respeita `min`/`max` de seleção.
5. Taxa de entrega calculada por distância (haversine) e configuração do
   estabelecimento; se a distância exceder `maxDistanceKm`,
   `400 DELIVERY_NOT_AVAILABLE`.

**Idempotência**: reenviar a mesma requisição com o mesmo `idempotencyKey`
(por esse cliente) retorna o pedido já criado em vez de duplicar — seguro
para retry automático em caso de timeout de rede.

**Snapshot**: `unitPrice`, `productName` e os nomes/preços dos adicionais
selecionados são congelados no pedido no momento da criação. Mudanças
futuras no cardápio não alteram pedidos já feitos.

## Ações do comércio

```text
GET  /api/merchant/establishments/{id}/orders?status=&page=&pageSize=
GET  /api/merchant/establishments/{id}/orders/{orderId}
POST /api/merchant/establishments/{id}/orders/{orderId}/accept
POST /api/merchant/establishments/{id}/orders/{orderId}/preparing
POST /api/merchant/establishments/{id}/orders/{orderId}/ready
POST /api/merchant/establishments/{id}/orders/{orderId}/cancel   { "reason": "..." }
```

Todas as transições são **idempotentes**: chamar `accept` duas vezes não
baixa estoque duas vezes; chamar em um pedido já em estado incompatível
retorna `409 INVALID_ORDER_TRANSITION`.

`ready` cria automaticamente:
- Um registro de `Delivery` com status `AVAILABLE`, visível para entregadores
  aprovados e online da mesma cidade.
- Um código de confirmação de 6 dígitos (nunca exposto pela API — apenas seu
  hash é armazenado; a entrega do código ao cliente é um TODO do módulo de
  notificações).

`cancel` só é permitido até `WAITING_DRIVER` (antes de um entregador
assumir). Se o pedido já tinha sido aceito, o estoque baixado é restaurado
automaticamente (`inventory_movements` tipo `CancellationRestore`).

## Consultar meus pedidos (cliente)

```text
GET /api/orders?page=&pageSize=
GET /api/orders/{id}
```

## Tempo real (SignalR)

Ver `docs/FRONTEND_GUIDE.md#signalr` — eventos `OrderCreated` e
`OrderStatusChanged` são emitidos nos grupos `establishment:{id}` e
`customer:{userId}` a cada transição.
