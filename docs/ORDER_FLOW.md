# Fluxo de pedidos

## Máquina de estados

```text
PENDING
  → ACCEPTED        (comércio aceita — baixa estoque automaticamente)
  → PREPARING        (comércio marca em preparo)
  → READY            (comércio marca pronto)
        orderType = Delivery: cria a Delivery + código de confirmação, segue abaixo
        orderType = Pickup:   fica pronto para retirada, NÃO cria Delivery
  → WAITING_DRIVER    (Delivery apenas — transição automática logo após READY)
  → DRIVER_ASSIGNED   (Delivery apenas — um entregador aceitou a entrega)
  → PICKED_UP         (Delivery apenas — entregador retirou no estabelecimento)
  → OUT_FOR_DELIVERY  (Delivery apenas — entregador saiu para entregar)
  → DELIVERED         (Delivery apenas — código de confirmação validado)
  → PICKUP_COMPLETED  (Pickup apenas — comércio confirma que o cliente retirou)

PENDING/ACCEPTED/PREPARING/READY/WAITING_DRIVER → CANCELLED (só o comércio, ver abaixo)
```

`WAITING_DRIVER` → `DRIVER_ASSIGNED` → `PICKED_UP` → `OUT_FOR_DELIVERY` →
`DELIVERED` são disparadas pelas ações do **entregador** (ver
`docs/DELIVERY_FLOW.md`), não pelo comércio, e só existem para
`orderType = Delivery`. `DELIVERED` e `PICKUP_COMPLETED` são os dois status
terminais de sucesso — distintos de propósito para que o app nunca precise
adivinhar, a partir do status, se houve ou não um entregador envolvido.

## PEDIDO: entrega (`Delivery`) vs retirada (`Pickup`)

`CreateOrderRequest.orderType` (`"Delivery"` por padrão, ou `"Pickup"`)
decide o fluxo inteiro:

| | `Delivery` | `Pickup` |
|---|---|---|
| `addressId` | obrigatório | ignorado — envie `null` |
| Taxa de entrega | valor fixo configurado pelo estabelecimento | sempre `0` |
| Raio máximo | valida contra `deliveryRadiusKm` | não se aplica — sem checagem de distância |
| `calculatedDistanceKm` | calculado (só para validar o raio, não o preço) | `null` |
| `DELIVERY_PRICING_NOT_CONFIGURED` | pode bloquear o pedido | nunca se aplica |
| Ao ficar `READY` | cria `Delivery` + código de 6 dígitos | não cria nada — fica `READY` mesmo |
| Conclusão | entregador confirma código (`docs/DELIVERY_FLOW.md`) | comércio chama `complete-pickup` |
| Pagamento | igual — direto ao portador na entrega/retirada | igual — direto ao estabelecimento na retirada |

`POST .../orders/{orderId}/complete-pickup` (staff, só para pedidos
`Pickup`): `READY → PICKUP_COMPLETED`. Idempotente. `409
INVALID_ORDER_TRANSITION` se chamado num pedido `Delivery` ou fora de
`READY`.

## Preview (calcular subtotal/taxa antes de confirmar)

```http
POST /api/orders/preview
```

Mesmo corpo de `POST /api/orders` (o `idempotencyKey` é ignorado). Roda
**exatamente** a mesma validação e cálculo de taxa que a criação real —
mesmo código, não uma reimplementação — e responde sem persistir nada:

```json
{
  "establishmentId": "...",
  "type": "Delivery",
  "addressId": "...",
  "subtotal": 48.00,
  "deliveryFee": 7.00,
  "total": 55.00,
  "distanceKm": 1.13,
  "pricingRuleSnapshotJson": "{\"deliveryFee\":7.00,\"distanceKm\":1.13,\"deliveryRadiusKm\":8.00}",
  "items": [ ... ]
}
```

Isso garante que o total exibido no preview não pode divergir do total
cobrado por um `POST /api/orders` imediatamente depois **com o mesmo
corpo** — a única fonte possível de divergência é algo mudar *entre* as
duas chamadas (preço de produto, configuração de taxa, o próprio endereço),
o que é inerente a qualquer preview-então-confirma, não um bug. Sempre
confirme com o `total` retornado pela criação real, nunca cacheie o do
preview além da tela de checkout.

## Criar pedido (cliente)

```http
POST /api/orders
{
  "establishmentId": "...",
  "addressId": "...",                   // obrigatório se orderType = Delivery; null/omitido em Pickup
  "paymentMethod": "Pix",              // Pix | Cash | DebitOnDelivery | CreditOnDelivery
  "changeForAmount": null,              // só relevante para Cash
  "items": [
    {
      "productId": "...",
      "quantity": 2,
      "options": [ { "productOptionId": "..." } ]
    }
  ],
  "idempotencyKey": "uuid-gerado-pelo-cliente",  // opcional, mas recomendado
  "orderType": "Delivery",              // "Delivery" (padrão) | "Pickup"
  "notes": null                         // opcional, até 1000 caracteres
}
```

Validações do backend, nesta ordem — qualquer falha aborta o pedido inteiro
(nada é criado):

1. Estabelecimento existe, aceita pedidos (`EstablishmentSettings.acceptsNewOrders`),
   não está suspenso comercialmente e está operacionalmente aberto — isso
   inclui **não estar pausado** (`OperationalStatus.Paused`, ver "Pausa do
   estabelecimento" abaixo); tudo isso via `409 ESTABLISHMENT_CLOSED` (ou
   `ESTABLISHMENT_NOT_ACCEPTING_ORDERS`/`ESTABLISHMENT_SUSPENDED`
   conforme o caso), para `Delivery` e `Pickup` igualmente.
2. **Somente se `orderType = Delivery`**: endereço de entrega pertence ao
   cliente autenticado e é da **mesma cidade** do estabelecimento
   (`400 ADDRESS_REQUIRED` se `addressId` vier vazio).
3. Cada produto pertence ao estabelecimento, está disponível
   (`isAvailable`), e — se `stockMode = Controlled` — há estoque suficiente
   somando todas as linhas do mesmo produto no pedido.
4. Cada grupo de adicionais respeita `min`/`max` de seleção.
5. **Somente se `orderType = Delivery`**: distância até o endereço
   (haversine) validada contra `deliveryRadiusKm` do estabelecimento — só
   para decidir se a entrega é oferecida, **nunca** para calcular o preço
   (`400 OUTSIDE_DELIVERY_AREA` se exceder). Dentro do raio, `deliveryFee`
   é sempre o valor fixo configurado (`DeliveryPricingSettings.deliveryFee`),
   independente da distância exata. Pedidos `Pickup` pulam esta etapa
   inteira — `deliveryFee = 0` sempre, sem checagem de raio, e o
   estabelecimento não precisa ter `DeliveryPricingSettings` configurado.
6. `paymentMethod = Cash` com `changeForAmount` informado: valor deve ser
   ≥ `subtotal + deliveryFee` (`400 INVALID_CHANGE_AMOUNT` caso contrário).
   Qualquer outra forma de pagamento com `changeForAmount` preenchido:
   `400 CHANGE_AMOUNT_NOT_APPLICABLE` — troco só existe para dinheiro.

**Idempotência**: reenviar a mesma requisição com o mesmo `idempotencyKey`
(por esse cliente) retorna o pedido já criado em vez de duplicar — seguro
para retry automático em caso de timeout de rede.

**Snapshot**: `unitPrice`, `productName`, os nomes/preços dos adicionais
selecionados, e a própria `deliveryFee`/`pricingRuleSnapshotJson` são
congelados no pedido no momento da criação. Mudanças futuras no cardápio ou
na taxa de entrega do estabelecimento não alteram pedidos já feitos — se a
loja mudar a taxa de R$7 para R$10 amanhã, pedidos de hoje continuam com R$7.

## Pausa do estabelecimento

O comércio pode pausar temporariamente o recebimento de **novos** pedidos
(`Delivery` e `Pickup` igualmente) — pedidos já em andamento seguem
normalmente até o fim.

```text
POST /api/merchant/establishments/{id}/pause    { "reason": "HighDemand", "pausedUntilUtc": null }
POST /api/merchant/establishments/{id}/resume
```

`reason`: `HighDemand`, `NoDriverAvailable`, `TemporaryClosure`, `Other`.
`pausedUntilUtc` é opcional — omitido, a pausa só termina com um `resume`
manual; informado, um job (`EstablishmentPauseExpiryJob`, a cada 15 min)
retoma automaticamente, e mesmo antes do job rodar uma pausa já vencida
nunca bloqueia um pedido novo. Requer ser staff do estabelecimento
(`403 ESTABLISHMENT_ACCESS_DENIED` caso contrário) — mesma checagem de todo
endpoint `/api/merchant/establishments/{id}/*`.

## Ações do comércio

```text
GET  /api/merchant/establishments/{id}/orders?status=&page=&pageSize=
GET  /api/merchant/establishments/{id}/orders/{orderId}
POST /api/merchant/establishments/{id}/orders/{orderId}/accept
POST /api/merchant/establishments/{id}/orders/{orderId}/preparing
POST /api/merchant/establishments/{id}/orders/{orderId}/ready
POST /api/merchant/establishments/{id}/orders/{orderId}/complete-pickup   -- só orderType = Pickup
POST /api/merchant/establishments/{id}/orders/{orderId}/cancel   { "reason": "..." }
```

Todas as transições são **idempotentes**: chamar `accept` duas vezes não
baixa estoque duas vezes; chamar em um pedido já em estado incompatível
retorna `409 INVALID_ORDER_TRANSITION`.

`ready`, se `orderType = Delivery`, cria automaticamente:
- Um registro de `Delivery` com status `AVAILABLE`, visível para entregadores
  aprovados e online da mesma cidade.
- Um código de confirmação de 6 dígitos. O hash é o único valor comparado na
  confirmação do entregador; separadamente, uma cópia cifrada
  (reversível, `IDeliveryCodeCipher`) é guardada só para o
  `GET /api/orders/{id}/delivery-code` do cliente (ver `docs/DELIVERY_FLOW.md`
  e `docs/DECISIONS.md` #17). A API nunca retorna o hash.

Se `orderType = Pickup`, `ready` apenas marca o pedido `READY` — sem
`Delivery`, sem código. O comércio conclui com `complete-pickup` quando o
cliente retirar (ver seção acima).

`cancel` só é permitido até `WAITING_DRIVER` (antes de um entregador
assumir) — para `Pickup`, isso significa até `READY` inclusive. Se o pedido
já tinha sido aceito, o estoque baixado é restaurado automaticamente
(`inventory_movements` tipo `CancellationRestore`).

## Consultar meus pedidos (cliente)

```text
GET /api/orders?page=&pageSize=
GET /api/orders/{id}
GET /api/orders/{id}/delivery-code   -- ver docs/DELIVERY_FLOW.md
```

## Tempo real (SignalR) e notificações

Ver `docs/REALTIME_EVENTS.md` para a referência completa. Resumo: `OrderCreated`
é emitido em `establishment:{id}` na criação; `OrderStatusChanged` (payload
`{ orderId, status }`) é emitido em `customer:{userId}` + `establishment:{id}`
a cada transição de status, incluindo `Cancelled` — não há um evento
`OrderCancelled` separado, o `status` já carrega essa informação.

Push/histórico (`INotificationService`) acompanha as transições
voltadas ao cliente (aceito, em preparo, pronto, saiu para entrega,
entregue/retirado, cancelado) e notifica todo o staff do estabelecimento
(`establishment_users`) na criação do pedido.
