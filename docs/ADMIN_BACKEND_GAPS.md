# Gaps do backend para o painel Super Admin

O painel em `apps/admin-web` usa apenas endpoints existentes. Este documento
lista o que falta para cobrir o produto sem gambiarras no frontend. Nada aqui
foi mockado.

## 1. Dashboard / métricas

- **Funcionalidade:** visão consolidada da plataforma (totais, período, faturamento).
- **Endpoint necessário:** `GET /api/admin/metrics?from=&to=`
- **Motivo:** hoje o painel agrega `cities`, `establishments`, `drivers`, `customers`, `orders` e um GET de assinatura por loja. `pageSize` máximo é 100; totais de pedidos “hoje” e volume financeiro ficam incompletos.
- **Dados esperados:** totais de cidades, lojas (ativas/bloqueadas), clientes, entregadores, pedidos no período, volume de pedidos, receita de assinaturas, assinaturas por `paymentStatus`.
- **Prioridade:** alta.

## 2. Listagem global de usuários por papel

- **Funcionalidade:** usuários com filtro `CUSTOMER | DRIVER | ESTABLISHMENT_ADMIN | SUPER_ADMIN`, status e cidade.
- **Endpoint necessário:** `GET /api/admin/users?role=&isActive=&cityId=&page=`
- **Motivo:** existe só `GET /api/admin/customers` (papel CUSTOMER) e `GET /api/admin/drivers` (perfil de entregador, não user genérico). Não dá para listar admins de loja nem Super Admins.
- **Dados esperados:** id, nome, e-mail, telefone, roles, isActive, cityId (quando couber), createdAtUtc. Sem senha, tokens ou PII extra.
- **Prioridade:** alta.

## 3. Filtros na listagem de estabelecimentos

- **Funcionalidade:** buscar e filtrar lojas no servidor.
- **Endpoint necessário:** `GET /api/admin/establishments?cityId=&commercialStatus=&operationalStatus=&q=&page=`
- **Motivo:** `GetAll` é só paginado por nome. O painel filtra no cliente após carregar todas as páginas.
- **Dados esperados:** mesmo `EstablishmentResponse`, com `totalCount` já filtrado.
- **Prioridade:** alta.

## 4. Responsável / staff do estabelecimento

- **Funcionalidade:** ver o admin da loja e a equipe.
- **Endpoint necessário:** `GET /api/admin/establishments/{id}/users`
- **Motivo:** o POST de criação recebe AdminName/Email/Phone/Password, mas o GET não devolve o usuário criado nem `establishment_users`.
- **Dados esperados:** userId, name, email, phoneNumber, role, isActive. Sem senha.
- **Prioridade:** alta.

## 5. Endereço no GET do estabelecimento

- **Funcionalidade:** editar loja sem o admin redigitar o endereço.
- **Endpoint necessário:** incluir objeto `address` em `EstablishmentResponse` **ou** `GET /api/admin/establishments/{id}/address`.
- **Motivo:** o contrato devolve só `addressId`. O PUT exige street/number/district/lat/lng.
- **Dados esperados:** street, number, district, complement, zipCode, latitude, longitude.
- **Prioridade:** média.

## 6. Listagem global de assinaturas

- **Funcionalidade:** fila de vencimentos, graça e bloqueios.
- **Endpoint necessário:** `GET /api/admin/subscriptions?paymentStatus=&cityId=&page=`
- **Motivo:** só existe GET/PUT por estabelecimento. O overview atual faz N+1.
- **Dados esperados:** establishmentId, name, monthlyAmount, billingDay, dueDate, graceDays, paymentStatus, graceEndsOn.
- **Prioridade:** alta.

## 7. Pedidos admin: detalhe e filtros

- **Funcionalidade:** consulta operacional da plataforma.
- **Endpoints necessários:**
  - `GET /api/admin/orders/{id}`
  - `GET /api/admin/orders?cityId=&type=&from=&to=&establishmentId=&status=&page=`
- **Motivo:** a listagem só filtra `establishmentId` e `status`. Não há GET por id. O detalhe atual usa `GET /api/merchant/establishments/{id}/orders/{orderId}` porque o guard libera SUPER_ADMIN.
- **Dados esperados:** `OrderResponse` completo + filtros server-side.
- **Prioridade:** alta.

## 8. Entregadores: cidade, disponibilidade e histórico

- **Funcionalidade:** operação de frota no admin.
- **Endpoints necessários:**
  - `GET /api/admin/drivers?cityId=&status=&approvalStatus=&page=`
  - `GET /api/admin/drivers/{id}`
  - `GET /api/admin/drivers/{id}/deliveries?page=`
- **Motivo:** a listagem filtra só `approvalStatus`. Não há detalhe nem histórico. A “entrega atual” é inferida de `GET /api/admin/deliveries` (máx. 100).
- **Dados esperados:** dados públicos do entregador (sem CPF/Pix na listagem), status, cityId, entrega corrente e histórico resumido.
- **Prioridade:** média.

## 9. Entregas admin mais ricas

- **Funcionalidade:** acompanhar entrega com contexto.
- **Endpoint necessário:** `GET /api/admin/deliveries/{id}` devolvendo `AdminDeliveryResponse` + nomes/endereços, **ou** enriquecer `DeliveryResponse`.
- **Motivo:** `GetForAdminAsync` devolve `DeliveryResponse` (IDs e timestamps). `AdminDeliveryResponse` existe no contrato e não é usado pelo controller.
- **Dados esperados:** ids atuais + nome da loja, cidade, entregador, status do pedido.
- **Prioridade:** média.

## 10. Faturamento da plataforma

- **Funcionalidade:** receita PedeLocal (assinaturas) vs GMV de pedidos.
- **Endpoint necessário:** `GET /api/admin/finance/summary?from=&to=`
- **Motivo:** não há settlement/receita da plataforma. O dashboard soma `order.total` da página carregada e deixa explícito que isso não é faturamento PedeLocal.
- **Dados esperados:** GMV de pedidos, taxa/assinaturas recebidas, inadimplência.
- **Prioridade:** média.

## 11. Catálogo de feature flags

- **Funcionalidade:** saber quais chaves existem antes de gravar.
- **Endpoint necessário:** `GET /api/admin/feature-keys`
- **Motivo:** `PUT .../features/{featureKey}` aceita qualquer string. O painel só oferece as chaves de `FeatureKeys` no domínio (`CUSTOM_REPORT`, `WHOLESALE_MODE`, `ADVANCED_STOCK`, `SPECIAL_ORDER_FLOW`) e as já persistidas.
- **Dados esperados:** key, label, description, defaultEnabled.
- **Prioridade:** baixa.

## 12. Auditoria: ação e período

- **Funcionalidade:** investigar um evento.
- **Endpoint necessário:** `GET /api/admin/audit-logs?action=&from=&to=&entityType=&actorUserId=&page=`
- **Motivo:** hoje só `entityType` e `actorUserId`.
- **Dados esperados:** o `AuditLogResponse` atual, já filtrado.
- **Prioridade:** média.

## 13. Horários / visibilidade da loja no admin

- **Funcionalidade:** ver se a loja aceita pedidos além do status comercial.
- **Endpoint necessário:** incluir no GET admin (ou um GET settings) `acceptsNewOrders`, horários e `isVisible` se esses campos passarem a existir para o admin.
- **Motivo:** `EstablishmentResponse` admin não tem hours, description nem `CanReceiveNewOrders`.
- **Dados esperados:** flags e grade de horários já usados no merchant, se forem persistidos.
- **Prioridade:** baixa.

## O que o painel já cobre com contratos reais

| Área | Endpoints |
| --- | --- |
| Auth | `POST /api/auth/login`, `refresh`, `logout`, `GET /api/me/profile` |
| Cidades | `GET/POST /api/admin/cities`, `PUT /{id}`, `PATCH /{id}/status` |
| Estabelecimentos | `GET/POST /api/admin/establishments`, `GET/PUT /{id}`, `PATCH /{id}/status` |
| Categorias | `GET /api/establishment-categories` |
| Assinaturas | `GET/PUT /api/admin/establishments/{id}/subscription`, `GET/POST .../payments` |
| Features | `GET/PUT /api/admin/establishments/{id}/features/{featureKey}` |
| Clientes | `GET /api/admin/customers` |
| Entregadores | `GET /api/admin/drivers`, `POST /{id}/approve\|reject\|block\|unblock` |
| Pedidos | `GET /api/admin/orders`, detalhe via `GET /api/merchant/establishments/{id}/orders/{orderId}` |
| Entregas | `GET /api/admin/deliveries` |
| Auditoria | `GET /api/admin/audit-logs` |
