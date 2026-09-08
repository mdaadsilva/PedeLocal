# Push notifications (Firebase Cloud Messaging)

Infraestrutura real de push via Firebase Admin SDK, complementar ao SignalR
(`docs/REALTIME_EVENTS.md`) — SignalR continua responsável pelo tempo real
enquanto o app está conectado; push cobre app em background, fechado, ou
com a conexão SignalR caída.

## Arquitetura

```text
OrderService / DeliveryService / FinanceService / SubscriptionGracePeriodJob
        │  chamam
        ▼
INotificationService.NotifyAsync(userId, type, title, body, data)
        │  (Application, PedeLocal.Application.Common.Interfaces)
        ▼
AppNotificationService (Infrastructure)
        │  1. sempre persiste uma linha em `notifications`
        │  2. busca os device_tokens ATIVOS do usuário
        │  3. chama IPushSender.SendAsync uma vez por device
        ▼
IPushSender (Application.Common.Interfaces)
        │
        ├── FirebaseFcmPushSender   (Firebase:Enabled = true, credenciais OK)
        └── NoOpPushSender          (padrão — loga, não envia de verdade)
```

Nenhuma camada de negócio (`OrderService`, `DeliveryService`, etc.)
referencia o SDK do Firebase — apenas `INotificationService`. Trocar de
no-op para push real, ou trocar de provedor no futuro, é só uma mudança de
registro em `Infrastructure/DependencyInjection.cs`.

`DeviceTokenService` (Application) é uma abstração separada de
`AppNotificationService` — cuida só do CRUD de tokens (registrar/remover),
nunca de envio.

## Device tokens

Um usuário pode ter vários dispositivos ativos ao mesmo tempo (celular +
tablet, reinstalação do app, etc.) — modelado como uma linha por
dispositivo em `device_tokens`, não mais um único campo em `users` (ver
"Migration" abaixo).

```csharp
class DeviceToken {
    Guid Id;
    Guid UserId;        // sempre derivado do JWT — nunca aceito do cliente
    string Token;        // o token FCM em si — chave natural, único no banco
    DevicePlatform Platform;  // Unknown | Ios | Android | Web
    bool IsActive;
    DateTime CreatedAtUtc;
    DateTime UpdatedAtUtc;
}
```

- `Token` tem índice **único** — evita duplicidade por construção. Registrar
  um token que já existe (mesmo que pertencesse a outro usuário — um
  aparelho compartilhado ou resetado, por exemplo) **reatribui** a linha
  para o usuário atual em vez de duplicar. Isso é esperado: tokens FCM são
  únicos por instalação do app, não por conta.
- `IsActive=false` marca um token que o Firebase reportou como
  inválido/não registrado (ver "Token inválido" abaixo) — a linha
  permanece (histórico), mas nenhum push é mais enviado para ele.
- Suporta qualquer papel autenticado — usado hoje por CUSTOMER e DRIVER,
  mas nada no modelo restringe por papel.

### Endpoints

```http
PUT /api/me/device-tokens
Authorization: Bearer <accessToken>
{ "token": "<token FCM>", "platform": "Ios" }   -- ou "Android" | "Web" | "Unknown"

204 No Content
```

Registra (ou atualiza) um token — **idempotente**: chamar de novo com o
mesmo token apenas atualiza `platform`/`updatedAtUtc`/reativa se estava
inativo, nunca duplica. Chame isso sempre que o token do FCM for
gerado/rotacionado pelo app (normalmente a cada start).

```http
DELETE /api/me/device-tokens
Authorization: Bearer <accessToken>
{ "token": "<token FCM>" }

204 No Content
```

Remove um token — chame no logout/desinstalação daquele dispositivo
específico. Idempotente e silenciosamente um no-op se o token não existir
ou pertencer a outro usuário — **nunca** remove o token de outro usuário, e
nunca revela se o token existe sob outra conta (mesmo comportamento "sem
oráculo" do resto da API, ver `docs/ERROR_CODES.md`).

Ambos os endpoints exigem `Authorization: Bearer` — sem token, `401`. O
`userId` usado é **sempre** o do JWT (`ICurrentUserService`); o corpo da
requisição nunca carrega um `userId`.

## Eventos que geram push

Todo evento abaixo primeiro passa por `INotificationService.NotifyAsync`,
então: (1) persiste em `notifications` (histórico, `GET /api/me/notifications`
— sempre funciona, com ou sem device token, com ou sem Firebase
configurado); (2) dispara um push por device token ativo do destinatário.

### Cliente

| `NotificationEventType` | Quando | Disparado por |
|---|---|---|
| `OrderCreated` | não aplicável ao cliente (vai para o staff — ver abaixo) | — |
| `OrderAccepted` | comércio aceita o pedido | `OrderService.AcceptAsync` |
| `OrderPreparing` | comércio marca em preparo | `OrderService.MarkPreparingAsync` |
| `OrderReady` | comércio marca pronto (Delivery ou Pickup) | `OrderService.MarkReadyAsync` |
| `OrderOutForDelivery` | entregador sai para entrega | `DeliveryService.OutForDeliveryAsync` |
| `DeliveryAccepted` | entregador aceita a entrega | `DeliveryService.AcceptAsync` |
| `OrderDelivered` | entrega confirmada **ou** retirada confirmada | `DeliveryService.ConfirmAsync` / `OrderService.CompletePickupAsync` |
| `OrderCancelled` | comércio cancela o pedido | `OrderService.CancelAsync` |

### Entregador

| `NotificationEventType` | Quando | Elegibilidade |
|---|---|---|
| `DeliveryAvailable` | pedido `Delivery` fica pronto (nova `Delivery` criada) | todo entregador `Approved` + `Available` **na mesma cidade** do pedido — mesma regra do grupo SignalR `city-drivers:{cityId}` |
| `PaymentReported` | estabelecimento reporta pagamento de um lote | o entregador dono do lote |

Não há notificação de cancelamento para o entregador: por regra de negócio
(`docs/ORDER_FLOW.md`), um pedido só pode ser cancelado até
`WAITING_DRIVER` — antes de qualquer entregador aceitar — então nunca existe
um entregador ativo para avisar.

### Estabelecimento

| `NotificationEventType` | Quando | Destinatário |
|---|---|---|
| `OrderCreated` | cliente cria um pedido | cada staff do estabelecimento (`establishment_users`, qualquer `StaffRole`) |
| `SubscriptionWarning` | assinatura vence / é suspensa | admin do estabelecimento |

Não há notificação "pedido cancelado" para o estabelecimento: cancelamento
é sempre uma ação do próprio staff — não faz sentido notificar o autor da
própria ação.

## Payloads

Cada push carrega **só identificadores**, nunca dados pessoais de terceiros
— o app usa o `orderId`/`deliveryId` para buscar o resto via REST
(`GET /api/orders/{id}` etc.), a mesma prática já usada para SignalR
(`docs/REALTIME_EVENTS.md`).

```json
// Eventos de pedido (cliente/estabelecimento)
{ "orderId": "...", "eventType": "OrderAccepted" }

// Eventos de entrega (cliente)
{ "orderId": "...", "deliveryId": "...", "eventType": "DeliveryAccepted" }

// Nova entrega disponível (entregador) — deliberadamente SEM nome/endereço/
// telefone do cliente: o entregador ainda não aceitou a entrega.
{ "deliveryId": "...", "eventType": "DeliveryAvailable" }
```

O mesmo objeto vira `DataJson` na linha de `notifications` (serializado uma
única vez, reaproveitado para as duas coisas) e o `data` da mensagem FCM
(`Message.Data`).

## Confiabilidade

Uma falha de push **nunca** derruba a operação de negócio que a disparou —
criar pedido, mudar status, aceitar entrega, concluir entrega continuam
válidos mesmo com o Firebase fora do ar ou mal configurado:

- `AppNotificationService.NotifyAsync` sempre persiste a linha em
  `notifications` **antes** de tentar qualquer push.
- O envio é por device token, isoladamente: uma falha em um token nunca
  impede o envio para os demais tokens do mesmo usuário, nem interrompe o
  restante da requisição HTTP que originou a notificação.
- `FirebaseFcmPushSender`/`NoOpPushSender` nunca deixam uma exceção "normal"
  (rede, quota, outage do provedor) propagar sem tratamento — `AppNotificationService`
  captura, loga como warning (tipo do evento + userId, nunca o token) e
  segue para o próximo device.
- Se `Firebase:Enabled=true` mas as credenciais não carregam no startup
  (JSON inválido, arquivo ausente, `GOOGLE_APPLICATION_CREDENTIALS` não
  configurado), a aplicação **não falha ao subir** — cai para
  `NoOpPushSender` automaticamente e loga um erro explicando o motivo
  (`Infrastructure/DependencyInjection.cs`).

### Token inválido

Quando o Firebase reporta que um token é inválido/não registrado
(`MessagingErrorCode.Unregistered` ou `InvalidArgument` — app desinstalado,
token malformado, etc.), `FirebaseFcmPushSender` traduz isso em
`InvalidPushTokenException`. `AppNotificationService` reage marcando aquele
`DeviceToken.IsActive = false` — a linha continua existindo (histórico),
mas nenhum push futuro é tentado para ela até o dispositivo se registrar de
novo (o que reativa automaticamente, ver `PUT /api/me/device-tokens`
acima). Qualquer outra exceção (rede, quota, outage) **não** desativa o
token — é tratada como falha transitória, elegível para a próxima
tentativa.

O token em si **nunca aparece em log** em nenhum desses caminhos — apenas
seu comprimento (debug) ou o código de erro do provedor (info/warning).

## Variáveis de ambiente

Nenhuma credencial em `appsettings*.json` — só variáveis de ambiente /
secret management (Docker secret, Kubernetes secret, variável de ambiente
do provedor de hosting etc.), seguindo o mesmo padrão já usado para
`Jwt__Secret`/`ConnectionStrings__Default` (ver `.env.example`).

| Variável | Obrigatória | Descrição |
|---|---|---|
| `Firebase__Enabled` | não (padrão `false`) | `true` para tentar push real; `false`/ausente usa sempre `NoOpPushSender` |
| `Firebase__CredentialsJson` | uma das duas, se `Enabled=true` | o JSON completo da service account, como uma única string |
| `Firebase__CredentialsPath` | uma das duas, se `Enabled=true` | caminho para um arquivo JSON de service account montado no container/host (alternativa a `CredentialsJson` quando a plataforma só suporta montar arquivo, não variável multi-linha) |

Se `Enabled=true` e nenhuma das duas estiver definida, cai para as
Application Default Credentials do Google (`GoogleCredential.GetApplicationDefault()`),
que por sua vez honra a variável padrão `GOOGLE_APPLICATION_CREDENTIALS` —
útil em ambientes GCP-nativos (Cloud Run, GKE) onde a identidade já vem do
runtime.

**Nunca**: commitar `service-account.json` no repositório, colar a
credencial em `appsettings.json`/`appsettings.Development.json`, ou usar
uma credencial de exemplo/placeholder que pareça real.

## Configuração no Firebase Console

Passos necessários, fora deste repositório, para obter uma credencial real:

1. Criar (ou reaproveitar) um projeto no [Firebase Console](https://console.firebase.google.com/).
2. Ativar o **Cloud Messaging** para esse projeto (geralmente já vem
   ativado por padrão em projetos novos).
3. **Configurações do projeto → Contas de serviço → Gerar nova chave
   privada** — baixa um arquivo JSON de service account.
4. Nunca commitar esse arquivo. Colar o conteúdo (uma linha, JSON válido)
   em `Firebase__CredentialsJson` no secret manager do ambiente de deploy,
   ou montar o arquivo e apontar `Firebase__CredentialsPath` para ele.
5. Definir `Firebase__Enabled=true` nesse ambiente.

A integração Flutter (CUSTOMER/DRIVER) está em `apps/mobile` — ver
`apps/mobile/README.md` para arquivos do Console, package/bundle id e
comportamento foreground/background. Sem `google-services.json` /
`GoogleService-Info.plist` o app mobile continua compilável; só o push
no aparelho fica inativo.

## Comportamento quando o Firebase não está configurado

Padrão em todo ambiente sem `Firebase__Enabled=true` (inclusive
Development e os testes automatizados): `NoOpPushSender` — loga a
tentativa (nunca o token) e retorna sem enviar nada de verdade. Nenhuma
outra parte do sistema muda de comportamento:

- `notifications`/`GET /api/me/notifications` continuam funcionando
  normalmente (o histórico nunca depende de push real ter saído).
- `PUT/DELETE /api/me/device-tokens` continuam funcionando normalmente —
  os tokens ficam registrados, só não recebem push de verdade até o
  Firebase ser habilitado.
- Nenhuma exceção, nenhum efeito colateral, nenhuma degradação de qualquer
  outro endpoint.

## Integração Flutter (apps/mobile)

Implementado para CUSTOMER e DRIVER. Detalhes operacionais (Console,
onde colocar os arquivos, teste) em `apps/mobile/README.md`.

1. Permissão via `requestPermission` (uma vez) e token via `getToken()`.
2. `PUT /api/me/device-tokens` após autenticação e em `onTokenRefresh`.
3. `DELETE /api/me/device-tokens` no logout, **antes** de limpar a sessão.
4. Payload `data` (`orderId`/`deliveryId`/`eventType`/`batchId`) só para
   navegar; os dados da tela vêm da REST autorizada.
5. Foreground: snackbar discreto, sem duplicar SignalR. Background/
   terminated: handler oficial do Firebase + `onMessageOpenedApp` /
   `getInitialMessage()`.

## Migration

`AddDeviceTokensRemoveUserFcmToken` — cria `device_tokens` (com os índices
descritos acima) e remove a antiga coluna `users.FcmToken` (modelo de um
único token por usuário, substituído por esta etapa). Qualquer token já
registrado sob o modelo antigo é preservado: a migration faz um `INSERT`
de `users.FcmToken IS NOT NULL` para `device_tokens` (com `Platform =
'Unknown'`, já que a plataforma nunca era capturada antes) antes de
remover a coluna — nenhum token pré-existente é descartado silenciosamente.
Validado contra Postgres real (não só InMemory) antes do merge.
