# Onboarding do entregador

## Por que existe

Um simples cadastro (CPF + veículo + PIX) não é mais autorização para
trabalhar. Um entregador só pode ficar `ONLINE` e aceitar entregas quando:
o cadastro foi **aprovado por um SUPER_ADMIN**, a **CNH está válida** (não
vencida), e ele **não está bloqueado**.

## Estados

```text
Driver.onboardingStatus:  PendingDocuments → UnderReview → Approved | Rejected
Driver.status:            Offline | Available | Busy | Blocked   (presença em tempo real, sem relação com onboarding)
```

`onboardingStatus` e `status` são independentes de propósito:
`onboardingStatus` é a decisão de compliance (documentos aprovados ou não);
`status` é presença operacional (online/offline/ocupado) — **e também** o
mecanismo de suspensão administrativa (`Blocked`). Não existe um valor
`Suspended` separado: `/api/admin/drivers/{id}/block` e `.../unblock` **são**
suspender/reativar um entregador já aprovado.

Não existe, tampouco, um valor persistido `DocumentExpired`. Validade de CNH
é um fato temporal, não uma decisão de workflow — ver "Elegibilidade" abaixo.

### Transições

```text
PendingDocuments  -- driver envia CNH + veículo (os dois) -->  UnderReview
UnderReview       -- SUPER_ADMIN aprova                   -->  Approved
UnderReview       -- SUPER_ADMIN reprova (com motivo)     -->  Rejected
Rejected          -- driver reenvia CNH e/ou veículo       -->  UnderReview
Approved          -- driver troca o TIPO do veículo        -->  UnderReview  (recapacitação: Bike→Car muda o perfil de risco/capacidade)
```

Reenviar apenas a CNH (renovação de validade) ou trocar placa/modelo/cor do
veículo **não** tira um entregador `Approved` de aprovação — só a troca de
`vehicleType` força nova revisão.

## Elegibilidade (computada, não persistida)

`DriverEligibilityService.Evaluate(driver, license, now)` é a única fonte
de verdade sobre "pode trabalhar agora, e se não, por quê" — usada tanto em
`GoOnlineAsync` quanto em `DeliveryService.AcceptAsync`, para nunca haver
duas regras divergentes:

```text
Blocked            -- driver.status == Blocked (prioridade sobre tudo)
PendingDocuments   -- onboardingStatus == PendingDocuments, OU Approved sem nenhuma CNH no cadastro (inconsistência defensiva)
UnderReview        -- onboardingStatus == UnderReview
Rejected           -- onboardingStatus == Rejected
DocumentExpired    -- onboardingStatus == Approved, mas license.expiresAt < hoje
Eligible           -- onboardingStatus == Approved, license válida, não bloqueado
```

`GET /api/driver/me` e `GET /api/admin/drivers/{id}` expõem esse valor como
`eligibilityStatus` — o app do entregador deve mostrar essa string (não
`onboardingStatus` sozinho) para explicar por que o botão "Ficar online"
está desabilitado.

## CNH

**Não há integração com Senatran nem qualquer validação automática oficial
da CNH** — o que o entregador envia é o que fica registrado, e cabe a um
SUPER_ADMIN humano decidir. Nunca afirme (nem no app, nem em texto de erro)
que a CNH foi "validada" automaticamente.

```json
{
  "registrationNumber": "12345678900",
  "category": "A",
  "expiresAt": "2028-05-10",
  "hasEar": true,
  "documentImageUrl": "/uploads/xxxxx.jpg"
}
```

- `hasEar`: EAR (Exercício de Atividade Remunerada), relevante para
  motocicletas — opcional, `null` quando não aplicável.
- `documentImageUrl`: obtido primeiro via `POST /api/uploads/image`
  (infraestrutura de upload já existente — mesmo endpoint usado por fotos
  de produto), depois enviado aqui como referência. Este endpoint nunca
  recebe o arquivo diretamente.
- Submeter uma CNH já vencida é rejeitado na validação (`422`) — não faz
  sentido persistir um documento inútil desde o envio.

```text
PUT /api/driver/me/documents/license
PUT /api/driver/me/documents/vehicle
GET /api/driver/me/documents          -- CNH + veículo do próprio entregador
```

## Veículo

```json
{
  "type": "Motorcycle",
  "plate": "ABC1D23",
  "model": "Honda CG 160",
  "color": "Preto",
  "documentImageUrl": null
}
```

`plate` é opcional (bicicletas comumente não têm placa no Brasil).
Atualizar o veículo é sempre permitido; trocar `type` depois de `Approved`
volta o entregador para `UnderReview` (ver "Transições" acima).

## Validade / bloqueio automático

Documento vencido bloqueia **ficar online** e **aceitar nova entrega**
(`403 DRIVER_DOCUMENT_EXPIRED` nos dois casos) — a conta nunca é apagada
nem o `onboardingStatus` é alterado, só a elegibilidade computada muda.

`DriverLicenseResponse.daysUntilExpiration` (negativo quando já vencido) é
exposto para o app/admin construírem alertas de 30/15/7 dias e "vencido" —
esta etapa não inclui push dedicado para isso; o dado já está disponível
para quando um pipeline de alerta for implementado.

## Administração (SUPER_ADMIN)

```text
GET  /api/admin/drivers?onboardingStatus=UnderReview&page=
GET  /api/admin/drivers/{id}                 -- detalhes + CNH + veículo enviados até agora
POST /api/admin/drivers/{id}/approve         -- exige CNH e veículo já enviados e CNH não vencida
POST /api/admin/drivers/{id}/reject          { "reason": "..." }
POST /api/admin/drivers/{id}/block           -- suspende um entregador aprovado
POST /api/admin/drivers/{id}/unblock
```

`approve` falha com `422`-like `AppException` (`DRIVER_LICENSE_REQUIRED`,
`DRIVER_VEHICLE_REQUIRED`, `DRIVER_DOCUMENT_EXPIRED`) se o entregador ainda
não enviou os dois documentos ou a CNH já está vencida — impossível aprovar
um cadastro incompleto. Toda ação (`approve`/`reject`/`block`/`unblock`)
grava `AuditLog` com o SUPER_ADMIN autor.

Um DRIVER **nunca** consegue chamar essas rotas (`[Authorize(Roles =
SUPER_ADMIN)]` — um token DRIVER recebe `403` independente do payload, não
existe caminho para autoaprovação).

## Segurança

- CNH/veículo nunca aparecem em resposta nenhuma voltada a CUSTOMER ou
  MERCHANT — só `GET /api/driver/me*` (o próprio entregador) e
  `GET /api/admin/drivers/{id}` (SUPER_ADMIN).
- Um DRIVER só enxerga os próprios documentos — não existe rota de
  self-service para ver documentos de outro entregador por id.
- Ocorrências de entrega (`docs/DELIVERY_FLOW.md`) só podem ser criadas
  pelo entregador responsável pela entrega.
