# Fluxo de entregas

## Pré-requisitos do entregador

Ver `docs/DRIVER_ONBOARDING.md` para o fluxo completo (estados, CNH,
veículo, elegibilidade). Resumo do que importa para aceitar/executar uma
entrega:

```text
eligibilityStatus != Eligible → não pode ficar online nem aceitar entrega
eligibilityStatus == Eligible → GoOnline disponível (Offline → Available)
```

```text
GET  /api/driver/me                  -- perfil + onboardingStatus + eligibilityStatus + status
POST /api/driver/me/status/online    -- Offline → Available (403 se não Eligible, 409 se Busy)
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

## Detalhes da entrega (entregador)

```text
GET /api/driver/deliveries/{id}/details
```

Tudo que o entregador precisa para executar a entrega, depois de aceitá-la
(chamável em qualquer status — `ASSIGNED`, `PICKED_UP`, `OUT_FOR_DELIVERY`,
`COMPLETED`): dados do estabelecimento (nome, telefone, endereço com
lat/lng), endereço de entrega (com lat/lng), itens do pedido (produto,
quantidade, adicionais), forma de pagamento, valores (subtotal/taxa/total),
observações do cliente (`notes`) e status atual da entrega.

Mesma checagem de vínculo usada em `pickup`/`out-for-delivery`/`confirm`:
só o entregador com `delivery.driverId == eu` pode chamar
(`403 DELIVERY_ACCESS_DENIED` para qualquer outro entregador, mesmo que
aprovado e da mesma cidade).

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
  `READY` (apenas pedidos `orderType = Delivery` — `Pickup` nunca gera
  código). Peça o código verbalmente ao cliente na entrega.
- Máximo de 5 tentativas incorretas (`409 DELIVERY_CODE_LOCKED` depois
  disso) e expira em 24h (`409 DELIVERY_CODE_EXPIRED`).
- Ao confirmar com sucesso: `delivery = COMPLETED`, `order = DELIVERED`,
  `driver = AVAILABLE`, e um `driver_receivable` (valor = taxa de entrega do
  pedido) é criado automaticamente — uma única vez, mesmo em retries.

### Cliente consulta o próprio código

```text
GET /api/orders/{orderId}/delivery-code
```

Só o **CUSTOMER dono do pedido** pode chamar (`403 ORDER_ACCESS_DENIED`
para qualquer outro usuário — mesma checagem de ownership de
`GET /api/orders/{id}`). Resposta:

```json
{ "code": "123456", "expiresAtUtc": "2026-09-08T18:30:00Z" }
```

- `404 DELIVERY_CODE_NOT_FOUND`: pedido ainda não chegou a `READY`
  (nenhuma `Delivery` criada ainda), ou é um pedido `Pickup` (nunca tem
  código).
- `409 DELIVERY_CODE_EXPIRED`: passaram as 24h.
- `409 DELIVERY_CODE_ALREADY_USED`: a entrega já foi confirmada pelo
  entregador — não há mais nada a mostrar.
- Pode ser chamado quantas vezes o cliente quiser enquanto o código estiver
  ativo (não é de uso único por leitura — só a confirmação em si é de uso
  único, via `ConfirmedAtUtc`).

**Design**: o hash (`CodeHash`) continua sendo a única coisa comparada
quando o entregador confirma — nada muda nesse caminho. Como um hash é
irreversível por definição, esta consulta usa uma cópia separada,
criptografada de forma reversível (`IDeliveryCodeCipher`, AES-GCM,
chave derivada do `Jwt:Secret` já existente), guardada só para este
endpoint. A API nunca retorna o hash em nenhuma resposta. Ver
`docs/DECISIONS.md` #17 para a decisão completa.

## Ocorrências na entrega

Registro simples de problema operacional relatado pelo entregador durante
uma entrega em andamento — imutável (sem edição/exclusão, é histórico do
que foi relatado no momento).

```text
POST /api/driver/deliveries/{id}/occurrences   { "type": "CustomerNotFound", "note": "..." }
GET  /api/driver/deliveries/{id}/occurrences
GET  /api/merchant/establishments/{id}/orders/{orderId}/occurrences
GET  /api/admin/deliveries/{id}/occurrences
```

Tipos: `CustomerNotFound`, `CustomerNotResponding`, `WrongAddress`,
`PaymentProblem`, `VehicleProblem`, `Other`. `note` é opcional (até 500
caracteres).

Criar uma ocorrência exige ser o entregador responsável pela entrega
(`403 DELIVERY_ACCESS_DENIED` caso contrário — mesma checagem de
`pickup`/`confirm`/etc.). Visualizar exige ser esse mesmo entregador, staff
do estabelecimento da entrega, ou SUPER_ADMIN
(`403 OCCURRENCE_ACCESS_DENIED` para qualquer outro).

## Troco (pagamento em dinheiro)

`Order.changeForAmount` já é validado na criação do pedido
(`docs/ORDER_FLOW.md`) — o entregador só precisa **consultar** quanto
troco levar, via `GET /api/driver/deliveries/{id}/details` (campo
`order.changeForAmount`, `null` quando não se aplica). Não existe endpoint
separado — é parte do mesmo payload de detalhes que já mostra forma de
pagamento e valores.

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

## Tempo real (SignalR) e notificações

Ver `docs/REALTIME_EVENTS.md` para a referência completa. Resumo:
`DeliveryAvailable` (`{ deliveryId }`) vai para `city-drivers:{cityId}`
quando a entrega fica disponível. Ao ser aceita, `DeliveryAccepted` vai com
payload completo (`{ deliveryId, driverId }`) para `establishment:{id}` e,
**também**, com payload reduzido (`{ deliveryId }`, sem `driverId`) para
`city-drivers:{cityId}` — sinal para os demais entregadores da cidade
removerem a entrega da lista de disponíveis (o aceite em si continua
atômico no banco via `Delivery.Version`, não depende deste evento).
`DeliveryCompleted` (`{ orderId }`) vai para `establishment:{id}` +
`customer:{userId}`. As transições intermediárias (retirado no
estabelecimento, saiu para entrega) chegam via `OrderStatusChanged`
(`docs/ORDER_FLOW.md`), não por um evento de entrega dedicado.

Notificações push/histórico: cada entregador aprovado e disponível na
cidade é notificado quando uma entrega fica disponível; o cliente é
notificado quando o entregador sai para entrega e quando a entrega é
concluída.
