# PedeLocal Mobile (Flutter)

App oficial do **PedeLocal** — *Da sua cidade, pra você* — para **CUSTOMER** e **DRIVER** no mesmo binário. A navegação segue a role autenticada (`GET /api/me/profile`).

SignalR cobre realtime com o app aberto. **Firebase Cloud Messaging** cobre push com o app em background ou fechado.

## Requisitos

- Flutter SDK (este repositório usa o SDK em `C:\flutter_sdk` no ambiente de desenvolvimento)
- Backend local em `http://localhost:8080` (emulador Android: `http://10.0.2.2:8080`)
- Pacotes já declarados: `firebase_core` e `firebase_messaging` (não atualizar versões sem necessidade)

## Firebase — o que você precisa gerar (fora do Git)

O repositório **não** contém credenciais Firebase e **não** deve receber arquivos inventados. Sem esses arquivos o app **continua compilável**: REST e SignalR funcionam; o push simplesmente não registra token.

### 1. Projeto no Firebase Console

1. Abra [https://console.firebase.google.com/](https://console.firebase.google.com/)
2. Crie (ou reutilize) um projeto
3. Ative **Cloud Messaging** (Build → Cloud Messaging)
4. O backend precisa da **service account** (chave privada JSON) nas variáveis `Firebase__Enabled` / `Firebase__CredentialsJson` — ver `docs/PUSH_NOTIFICATIONS.md`. Isso é do servidor, não deste app.

### 2. App Android

- **Package name / applicationId:** `com.pedelocal.pedelocal_mobile`  
  (`android/app/build.gradle.kts`)
- No Console: Adicionar app Android com esse package
- Baixe `google-services.json`
- Coloque em:

```text
apps/mobile/android/app/google-services.json
```

O plugin `com.google.gms.google-services` só é aplicado se esse arquivo existir — o build Android não quebra na ausência dele.

O arquivo está no `.gitignore`. **Nunca commitar.**

### 3. App iOS

- **Bundle ID:** `com.pedelocal.pedelocalMobile`  
  (`PRODUCT_BUNDLE_IDENTIFIER` em `ios/Runner.xcodeproj`)
- No Console: Adicionar app iOS com esse bundle
- Baixe `GoogleService-Info.plist`
- Coloque em:

```text
apps/mobile/ios/Runner/GoogleService-Info.plist
```

Também gitignorado. **Nunca commitar.**

No Xcode, ainda é necessário:

1. Adicionar o `GoogleService-Info.plist` ao target Runner (se o Xcode não o vir automaticamente)
2. Capability **Push Notifications**
3. Capability **Background Modes → Remote notifications** (`UIBackgroundModes` já está no `Info.plist`)
4. Chave APNs (`.p8`) no Firebase Console → Project settings → Cloud Messaging

Não chame `FirebaseApp.configure()` no `AppDelegate` sem o plist real — a inicialização é feita em Dart e falha de forma controlada se o arquivo não existir.

### 4. Como testar push

1. Backend com `Firebase__Enabled=true` e service account válida
2. Arquivos do Console no Android e/ou iOS, como acima
3. Login como `CUSTOMER` ou `DRIVER`
4. Confirme no backend que `PUT /api/me/device-tokens` retornou 204 (o app não loga o token)
5. Coloque o app em background ou feche
6. Dispare um evento real (aceitar pedido, nova entrega, etc.)
7. Toque na notificação: o app abre a tela autorizada e busca os dados na API

O Console do Firebase também permite enviar uma mensagem de teste para o token do aparelho. Use só `data` com as chaves reais (`orderId`, `deliveryId`, `eventType`) — não invente campos.

## Endpoints de device token

Contrato em `docs/PUSH_NOTIFICATIONS.md` e `docs/API_CONTRACT.md`:

```http
PUT /api/me/device-tokens
Authorization: Bearer <accessToken>
{ "token": "<fcm>", "platform": "Android" }

DELETE /api/me/device-tokens
Authorization: Bearer <accessToken>
{ "token": "<fcm>" }
```

`platform` aceito: `Ios` | `Android` | `Web` | `Unknown` (PascalCase).  
O app envia `Android` ou `Ios` conforme o SO. **Não envia `userId`** — o backend deriva do JWT.

- Após login / sessão restaurada: obtém o token FCM e faz `PUT` (idempotente)
- `FirebaseMessaging.instance.onTokenRefresh`: novo `PUT` só se o token mudou
- Logout: `DELETE` **antes** de revogar a sessão local e de desconectar o SignalR

Falha ao registrar token **não impede o login**.

## Comportamento

### Foreground (app aberto)

- SignalR continua atualizando o estado
- FCM em foreground mostra um Snackbar discreto (título/corpo da mensagem)
- O mesmo evento SignalR + FCM não gera dois snackbars (janela curta, chave por pedido/entrega)
- Se o hub SignalR estiver desconectado, um refetch silencioso é disparado (mesmo tick de reconexão já usado pelas telas)
- Não navega sozinho e não interrompe checkout/fluxo

### Background / app fechado

- O sistema exibe a notificação (title/body enviados pelo backend)
- O isolate de background só faz parse do payload — sem rede, sem JWT, sem navegação
- Toque: `onMessageOpenedApp` (background) ou `getInitialMessage()` (terminated)

### Clique na notificação

A role vem da sessão local (confirmada na API), nunca do payload.

| Papel | Payload | Destino |
|---|---|---|
| CUSTOMER | `orderId` | `/orders/{orderId}` (acompanhamento) |
| DRIVER | `DeliveryAvailable` / `deliveryId` | `/home/driver` (lista/entrega atual; a API autoriza) |
| DRIVER | `PaymentReported` | `/driver/receivables` |

Sem autenticação, ou cliente sem cidade escolhida, a navegação fica pendente até o contexto existir. A tela sempre busca o recurso na API — a notificação é só uma referência.

## Permissões

- **Android 13+:** `POST_NOTIFICATIONS` no manifesto; `requestPermission()` do `firebase_messaging` na primeira sessão autenticada
- **iOS:** `requestPermission()` (alert/badge/sound)
- A permissão é pedida **uma vez** (flag em `SharedPreferences`). Negar não derruba o app; o token pode não existir até o usuário habilitar nas configurações do sistema

## Variáveis / configs

Nenhuma variável extra no Flutter além das já usadas (`API` / Maps).  
Push no aparelho depende dos arquivos do Console.  
Push de verdade no servidor depende de `Firebase__Enabled` + credenciais — `docs/PUSH_NOTIFICATIONS.md`.

## Segurança

Nunca:

- logar JWT ou o token FCM completo (só o comprimento, se necessário)
- commitar `google-services.json`, `GoogleService-Info.plist` ou service account
- confiar em `role` / `userId` vindos da notificação
- criar bypass de autorização — a API continua sendo a autoridade

## Validação

```powershell
cd apps/mobile
flutter analyze
flutter test
```
