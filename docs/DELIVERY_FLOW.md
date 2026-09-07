# Fluxo de entregas

## Pré-requisitos do entregador

```text
PENDING_APPROVAL → não pode trabalhar (aprovação do Super Admin)
APPROVED + status ONLINE → AVAILABLE
```

```text
GET  /api/driver/me                  -- perfil + approvalStatus + status
POST /api/driver/me/status/online    -- Offline → Available (falha se PendingApproval/Blocked/Busy)
POST /api/driver/me/status/offline   -- Available → Offline (falha se Busy)
```

Um entregador só pode ter **uma entrega ativa por vez** — `status = BUSY`
bloqueia tanto aceitar outra entrega quanto voltar a ficar `OFFLINE`.

## Ver e aceitar entregas disponíveis

```text
GET  /api/driver/deliveries/available    -- entregas AVAILABLE na cidade do entregador
GET  /api/driver/deliveries/current      -- entrega ativa do entregador (204 se nenhuma)
POST /api/driver/deliveries/{id}/accept
```

### A corrida de aceite (crítico)

Dois entregadores podem tentar aceitar a mesma entrega ao mesmo tempo. O
backend garante atomicidade com um token de concorrência otimista
(`deliveries.version`, incrementado a cada aceite bem-sucedido):

- O primeiro `accept` a persistir vence: `delivery.driverId` é setado,
  `delivery.status = ASSIGNED`, `driver.status = BUSY`.
- Qualquer segunda tentativa — mesmo que tenha lido o estado "livre" quase no
  mesmo instante — recebe `409 DELIVERY_ALREADY_TAKEN` e **nenhum efeito
  colateral é aplicado** (o entregador que perdeu a corrida continua
  `AVAILABLE`).

**No cliente**: trate `409 DELIVERY_ALREADY_TAKEN` como esperado e comum em
horários de pico — apenas remova a entrega da lista e volte para
`GET /api/driver/deliveries/available`, sem mostrar como erro alarmante.

## Retirar, sair para entrega, confirmar

```text
POST /api/driver/deliveries/{id}/pickup             -- ASSIGNED → PICKED_UP
POST /api/driver/deliveries/{id}/out-for-delivery    -- PICKED_UP → OUT_FOR_DELIVERY
POST /api/driver/deliveries/{id}/confirm             -- OUT_FOR_DELIVERY → COMPLETED
  { "code": "123456" }
```

Cada ação só é aceita a partir do status anterior correto
(`409 INVALID_DELIVERY_TRANSITION` caso contrário) e apenas pelo entregador
responsável (`403 DELIVERY_ACCESS_DENIED` para qualquer outro). Todas são
idempotentes (repetir a mesma ação já concluída retorna 200 sem reaplicar
efeitos).

### Código de confirmação

- 6 dígitos, gerado com RNG criptográfico no momento em que o pedido fica
  `READY`. Peça o código verbalmente ao cliente na entrega.
- Máximo de 5 tentativas incorretas (`409 DELIVERY_CODE_LOCKED` depois
  disso) e expira em 24h (`409 DELIVERY_CODE_EXPIRED`).
- Ao confirmar com sucesso: `delivery = COMPLETED`, `order = DELIVERED`,
  `driver = AVAILABLE`, e um `driver_receivable` (valor = taxa de entrega do
  pedido) é criado automaticamente — uma única vez, mesmo em retries.

## Financeiro do entregador

```text
GET  /api/driver/receivables               -- meus recebíveis (por entrega)
GET  /api/driver/payment-batches           -- meus lotes de pagamento
POST /api/driver/payment-batches/{id}/confirm
```

O **estabelecimento** agrupa recebíveis pendentes em um lote e reporta o
pagamento (fora da plataforma — PIX, dinheiro etc.):

```text
GET  /api/merchant/establishments/{id}/driver-payments/receivables/pending?driverId=
POST /api/merchant/establishments/{id}/driver-payments/batches           { driverId, receivableIds[] }
POST /api/merchant/establishments/{id}/driver-payments/batches/{id}/report
```

Fluxo do lote: `PENDING → PAYMENT_REPORTED` (estabelecimento) `→ RECEIVED`
(entregador confirma). O status de cada recebível dentro do lote acompanha o
status do lote.

## Tempo real (SignalR)

`DeliveryAvailable` (grupo `city-drivers:{cityId}`), `DeliveryAccepted` e
`DeliveryCompleted` (grupo `establishment:{id}` + `customer:{userId}`). Ver
`docs/FRONTEND_GUIDE.md#signalr`.
