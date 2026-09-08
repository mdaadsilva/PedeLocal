# Tempo real (SignalR) e notificações

Referência completa do canal de tempo real e da camada de notificações.
Para as convenções gerais de auth/erros/paginação, ver `docs/FRONTEND_GUIDE.md`.

## Hub

```text
{baseUrl}/hubs/pedelocal?access_token=<accessToken>
```

WebSockets/SSE via `@microsoft/signalr` (web) ou equivalente Flutter. Como o
handshake de WebSocket do browser não permite header customizado, o access
token JWT vai via query string — aceito **apenas** para requisições sob
`/hubs` (`Program.cs`, `JwtBearerEvents.OnMessageReceived`); qualquer outra
rota da API continua exigindo `Authorization: Bearer`.

Uma conexão sem token válido (ausente, expirado ou malformado) é rejeitada —
o hub inteiro está sob `[Authorize]` (`PedeLocalHub`). Não há fallback
anônimo.

O browser chama o hub em origem cruzada (painel `:3000`/`:3001` → API
`:8080`). Sem as origens em `Cors:AllowedOrigins`, o `negotiate` falha com
`Failed to fetch`. Em Development o default é `http://localhost:3000` e
`http://localhost:3001`. Em Docker/produção a lista começa vazia — preencha
`CORS__ALLOWEDORIGINS__0` / `__1` no `.env` (ver `.env.example`). Não use
`AllowAnyOrigin`.

## Grupos

O servidor decide sozinho, a partir das claims do token e do banco, a quais
grupos a conexão pertence — **não existe um método `Subscribe` para o
cliente chamar**, e o cliente nunca informa `userId`/`establishmentId` para
entrar em um grupo. Isso é o que impede um CUSTOMER de escutar pedidos de
outro CUSTOMER, ou um DRIVER de escutar entregas de outra cidade.

`PedeLocalHub.OnConnectedAsync` resolve os grupos assim, para qualquer
usuário autenticado:

| Grupo | Quem entra | Como é decidido |
|---|---|---|
| `customer:{userId}` | todo usuário autenticado | sempre, a partir do `sub`/`NameIdentifier` do token — inofensivo para quem nunca fizer um pedido, já que nenhum evento chega a esse grupo a menos que exista um pedido com `customerUserId` igual |
| `establishment:{establishmentId}` | staff do estabelecimento (qualquer `StaffRole`) | uma entrada por linha em `establishment_users` para esse `userId` — cobre ESTABLISHMENT_ADMIN e os demais papéis de staff igualmente |
| `driver:{driverId}` | o próprio entregador | se existir um `Driver` com `UserId` igual ao do token |
| `city-drivers:{cityId}` | o próprio entregador | `Driver.CityId` — inclui entregadores de qualquer `ApprovalStatus`/`Status`; o filtro por "elegível" (aprovado + disponível) acontece na hora de emitir o evento, não na hora de entrar no grupo |

Não existe um grupo genérico "todos os admins" ou qualquer comportamento
implícito para `SUPER_ADMIN` — um usuário com esse papel recebe exatamente
os mesmos grupos que qualquer outro usuário autenticado receberia dado seus
próprios dados (nenhum, a menos que também seja staff/driver/tenha pedidos
próprios). Acesso administrativo de leitura continua exclusivamente via REST
(`/api/admin/*`).

## Eventos

Todo evento é *best-effort*: o transporte (`SignalRRealtimeNotifier`) nunca
propaga uma exceção de envio — falhas são logadas e engolidas (ver
"Confiabilidade" abaixo). Reconecte com backoff exponencial e, ao
reconectar, sempre busque o estado atual via REST — eventos perdidos durante
a desconexão não são reenviados (não há buffer/replay).

| Evento | Payload | Grupo(s) | Quando |
|---|---|---|---|
| `OrderCreated` | `{ orderId }` | `establishment:{id}` | cliente cria um pedido |
| `OrderStatusChanged` | `{ orderId, status }` | `customer:{userId}` + `establishment:{id}` | toda transição de `Order.Status` (Accepted, Preparing, Ready/WaitingDriver, DriverAssigned, PickedUp, OutForDelivery, Delivered, PickupCompleted, Cancelled) |
| `DeliveryAvailable` | `{ deliveryId }` | `city-drivers:{cityId}` | pedido `Delivery` fica `WAITING_DRIVER` (uma `Delivery` nova, status `Available`, foi criada) |
| `DeliveryAccepted` | `{ deliveryId, driverId }` | `establishment:{id}` | um entregador aceita a entrega — payload completo, o estabelecimento pode identificar quem vai retirar |
| `DeliveryAccepted` | `{ deliveryId }` (sem `driverId`) | `city-drivers:{cityId}` | mesmo aceite — sinal para os *demais* entregadores da cidade removerem essa entrega da lista de disponíveis. Sem identidade do motorista: um entregador não vinculado não tem por que saber quem ganhou a corrida (ver "Segurança" abaixo). O aceite em si continua garantido atômico no banco (`Delivery.Version`, docs/DELIVERY_FLOW.md) — este evento é só um sinal de UI, nunca a fonte de verdade |
| `DeliveryCompleted` | `{ orderId }` | `customer:{userId}` + `establishment:{id}` | entregador confirma o código de entrega |
| `EstablishmentSuspended` | `{ establishmentId }` | `establishment:{id}` | job diário de assinatura suspende o estabelecimento por atraso (`SubscriptionGracePeriodJob`) |

Não existe um evento `OrderCancelled`/`DeliveryStatusChanged` separado —
cancelamento e as transições PickedUp/OutForDelivery já são cobertos por
`OrderStatusChanged` com o `status` correspondente (`"Cancelled"`,
`"PickedUp"`, `"OutForDelivery"`). Um evento dedicado só duplicaria
informação que o `status` já carrega — ver decisão #19 em
`docs/DECISIONS.md`.

Não há evento de localização em tempo real do entregador — fora de escopo
desta etapa (ver "Driver status" abaixo).

### Driver status

Disponibilidade do entregador (`Offline → Available → Busy → Available`),
entrega atual e o bloqueio "indisponível enquanto está em uma entrega" já
são impostos no domínio (`DriverService`, `DeliveryService.AcceptAsync`/
`ConfirmAsync`) e refletidos via REST (`GET /api/driver/me`,
`GET /api/driver/deliveries/current`) — não existe um evento SignalR dedicado
para "motorista ficou disponível/ocupado". O próprio app do entregador já
sabe seu status pela resposta de cada chamada que ele fez (aceitar,
confirmar); não há necessidade de um segundo canal para a mesma informação.

## Quem recebe cada evento (resumo por papel)

| Papel | Recebe |
|---|---|
| CUSTOMER | `OrderStatusChanged`/`DeliveryCompleted` só dos próprios pedidos (`customer:{seu userId}`) |
| DRIVER | `DeliveryAvailable`/`DeliveryAccepted` (sem identidade de quem aceitou) só da própria cidade (`city-drivers:{sua cidade}`) |
| ESTABLISHMENT_ADMIN / demais staff | `OrderCreated`/`OrderStatusChanged`/`DeliveryAccepted` (completo)/`DeliveryCompleted`/`EstablishmentSuspended` só do(s) próprio(s) estabelecimento(s) (`establishment:{id}`) |
| SUPER_ADMIN | nada além do que seus próprios dados de usuário justificarem (ver tabela de grupos) — sem canal admin-wide nesta etapa |

## Notificações (push + histórico)

`INotificationService.NotifyAsync` (`PedeLocal.Application.Common.Interfaces`)
é a API de negócio: sempre persiste uma linha em `notifications`
(`GET /api/me/notifications` funciona mesmo sem token de push registrado ou
com o provedor de push fora do ar) e, se o usuário tiver um `FcmToken`
salvo, delega o envio a `IPushSender`.

| Evento de notificação | Destinatário | Quando |
|---|---|---|
| `OrderCreated` | cada staff do estabelecimento (`establishment_users`) | pedido criado |
| `OrderAccepted` | cliente | comércio aceita o pedido |
| `OrderPreparing` | cliente | comércio marca em preparo |
| `OrderReady` | cliente | comércio marca pronto (Delivery ou Pickup) |
| `OrderOutForDelivery` | cliente | entregador sai para entrega |
| `OrderDelivered` | cliente | entrega confirmada **ou** retirada confirmada (`complete-pickup`) |
| `OrderCancelled` | cliente | comércio cancela o pedido |
| `DeliveryAvailable` | cada entregador aprovado e disponível na cidade | nova `Delivery` disponível |
| `DeliveryAccepted` | cliente | entregador aceita a entrega |
| `PaymentReported` | entregador | estabelecimento reporta pagamento de um lote |
| `SubscriptionWarning` | admin do estabelecimento | assinatura entra em grace period |

Não há notificação de "novo pedido cancelado" para o estabelecimento: o
cancelamento (`POST .../orders/{orderId}/cancel`) é sempre uma ação do
próprio staff — não faz sentido notificar o ator da própria ação. Não há
notificação para o entregador confirmando o próprio aceite — pelo mesmo
motivo.

**Evitar duplicidade**: toda ação que dispara notificação é chamada no
máximo uma vez por transição real de estado — as mesmas checagens de
idempotência que guardam os endpoints (`AcceptAsync`, `CancelAsync`, etc.
retornam cedo se já estiverem no status alvo) impedem reenvio em
double-submit/retry.

### Push (Firebase)

Referência completa (device tokens, endpoints, variáveis de ambiente,
configuração do Firebase Console) em `docs/PUSH_NOTIFICATIONS.md` — este
documento cobre só o transporte SignalR. Resumo: `IPushSender` é a
abstração sobre o provedor real (Firebase Cloud Messaging), com uma
implementação real (`FirebaseFcmPushSender`) e um fallback no-op
(`NoOpPushSender`) usado sempre que `Firebase:Enabled` não está configurado
— nenhuma credencial falsa foi inserida em lugar nenhum, e nenhuma camada
de negócio (`INotificationService`, `OrderService`, `DeliveryService`)
precisa saber qual das duas está ativa.

## Confiabilidade

SignalR e push são canais *adicionais*, nunca a fonte de verdade — o estado
real sempre vive no banco e é sempre consultável via REST. Consequências:

- `SignalRRealtimeNotifier` captura qualquer exceção de envio por grupo/evento
  e loga como warning; nunca deixa a exceção subir para quem disparou o
  evento (`OrderService`, `DeliveryService`). Criar/aceitar/cancelar um
  pedido continua válido mesmo se o hub estiver com problema.
- `AppNotificationService.NotifyAsync` sempre persiste a linha em
  `notifications` primeiro; se `IPushSender.SendAsync` lançar (provedor
  fora do ar, token inválido etc.), o erro é logado e a notificação
  continua salva — só `SentAtUtc` fica `null`, indicando que o push nunca
  saiu.
- Nenhum dado sensível (token JWT, código de confirmação de entrega, senha)
  é logado nessas capturas — apenas tipo de evento, grupo/usuário-alvo e a
  mensagem de exceção do provedor.

## Reconexão esperada no cliente

1. Ao detectar desconexão, reconectar com backoff exponencial (o SDK do
   SignalR já oferece isso via `withAutomaticReconnect()`).
2. Ao reconectar (ou ao abrir o app), sempre buscar o estado atual via REST
   antes de confiar em eventos futuros — não há replay de eventos perdidos
   durante a janela desconectada.
3. Um evento chegando fora de ordem (rede instável) nunca deve fazer o
   cliente regredir um status já mais avançado — sempre trate `status`
   recebido como "o que era no momento do envio", e prefira um novo
   `GET /api/orders/{id}` se a ordem parecer suspeita.
