# Códigos de erro

Toda resposta de erro da API segue o mesmo formato:

```json
{
  "code": "ORDER_NOT_FOUND",
  "message": "Pedido não encontrado.",
  "traceId": "0HN...",
  "errors": null
}
```

- `code`: string estável, use para lógica no cliente (nunca faça `if (message === "...")`).
- `message`: texto em PT-BR, pode ser mostrado direto ao usuário.
- `traceId`: identificador da requisição — peça ao usuário para informar ao reportar um bug.
- `errors`: presente apenas em erros de validação (`422`), formato `{ "campo": ["mensagem"] }`.

## Status HTTP usados

| Status | Quando |
|---|---|
| 400 | Regra de negócio violada (`AppException` genérica) |
| 401 | Não autenticado / token inválido ou expirado |
| 403 | Autenticado, mas sem permissão (ownership, role, bloqueio) |
| 404 | Recurso não encontrado |
| 409 | Conflito de estado (transição inválida, corrida de concorrência, duplicidade) |
| 422 | Erro de validação de campos (FluentValidation) |
| 429 | Rate limit excedido |
| 500 | Erro inesperado (nunca deve conter stack trace) |

## Catálogo de códigos

### Autenticação / conta (`api/auth`, `api/me`)

| Código | Status | Significado |
|---|---|---|
| `INVALID_CREDENTIALS` | 401 | E-mail ou senha incorretos |
| `INVALID_REFRESH_TOKEN` | 401 | Refresh token inválido, expirado ou já usado/revogado |
| `INVALID_RESET_TOKEN` | 401 | Token de recuperação de senha inválido ou expirado |
| `EMAIL_ALREADY_REGISTERED` | 409 | E-mail já cadastrado |
| `CPF_ALREADY_REGISTERED` | 409 | CPF já cadastrado (entregador) |
| `INVALID_VEHICLE_TYPE` | 400 | Tipo de veículo inválido no cadastro de entregador |
| `INVALID_PIX_KEY_TYPE` | 400 | Tipo de chave PIX inválido |
| `UNAUTHENTICATED` | 401 | Endpoint exige usuário autenticado (token ausente/rejeitado antes do controller) |
| `USER_NOT_FOUND` | 404 | Usuário não encontrado |
| `NOTIFICATION_NOT_FOUND` | 404 | Notificação não encontrada |
| `NOTIFICATION_ACCESS_DENIED` | 403 | Notificação pertence a outro usuário |

### Cidades / Endereços

| Código | Status | Significado |
|---|---|---|
| `CITY_NOT_FOUND` | 404 | Cidade não encontrada ou inativa |
| `ADDRESS_NOT_FOUND` | 404 | Endereço não encontrado |
| `ADDRESS_ACCESS_DENIED` | 403 | Endereço pertence a outro cliente |
| `INVALID_ADDRESS_TYPE` | 400 | `type` diferente de `Urban`/`Rural` |
| `ADDRESS_CITY_MISMATCH` | 400 | Endereço de entrega não é da mesma cidade do estabelecimento |

### Estabelecimentos / Catálogo / Estoque

| Código | Status | Significado |
|---|---|---|
| `ESTABLISHMENT_NOT_FOUND` | 404 | Estabelecimento não encontrado |
| `ESTABLISHMENT_ACCESS_DENIED` | 403 | Usuário não é staff deste estabelecimento nem Super Admin |
| `CATEGORY_NOT_FOUND` | 404 | Categoria de estabelecimento não encontrada |
| `MENU_CATEGORY_NOT_FOUND` | 404 | Categoria de cardápio não encontrada |
| `PRODUCT_NOT_FOUND` | 404 | Produto não encontrado |
| `PRODUCT_UNAVAILABLE` | 409 | Produto indisponível no momento do pedido |
| `INVALID_STOCK_MODE` | 400 | `stockMode` inválido |
| `INVALID_OPERATIONAL_STATUS` / `INVALID_COMMERCIAL_STATUS` | 400 | Valor de status inválido no PATCH de status |
| `INVALID_MOVEMENT_TYPE` | 400 | Tipo de movimentação de estoque inválido (só Entry/Loss/Return/Adjustment são manuais) |
| `PRODUCT_NOT_STOCK_CONTROLLED` | 400 | Ajuste de estoque em produto sem controle de estoque |
| `INSUFFICIENT_STOCK` | 409 | Estoque insuficiente para a operação |

### Pedidos (`api/orders`, `api/merchant/.../orders`)

| Código | Status | Significado |
|---|---|---|
| `ORDER_NOT_FOUND` | 404 | Pedido não encontrado |
| `ORDER_ACCESS_DENIED` | 403 | Pedido pertence a outro cliente |
| `ORDER_EMPTY` | 400 | Pedido sem itens |
| `INVALID_PAYMENT_METHOD` | 400 | Forma de pagamento inválida |
| `INVALID_ITEM_QUANTITY` | 400 | Quantidade de item ≤ 0 |
| `INVALID_OPTION_SELECTION` | 400 | Seleção de adicionais fora do min/max do grupo, ou opção inexistente |
| `INVALID_CHANGE_AMOUNT` | 400 | Valor de troco menor que o total do pedido |
| `ESTABLISHMENT_NOT_ACCEPTING_ORDERS` | 409 | `EstablishmentSettings.AcceptsNewOrders = false` |
| `ESTABLISHMENT_SUSPENDED` | 409 | Estabelecimento suspenso (comercial) |
| `ESTABLISHMENT_CLOSED` | 409 | Estabelecimento fechado no momento (operacional) |
| `DELIVERY_PRICING_NOT_CONFIGURED` | 409 | Estabelecimento sem taxa de entrega configurada |
| `DELIVERY_NOT_AVAILABLE` | 400 | Distância acima do `maxDistanceKm` configurado |
| `INVALID_ORDER_TRANSITION` | 409 | Ação não permitida no status atual do pedido |

### Entregadores / Entregas (`api/driver/*`, `api/admin/drivers`)

| Código | Status | Significado |
|---|---|---|
| `DRIVER_PROFILE_NOT_FOUND` | 404 | Usuário autenticado não tem perfil de entregador |
| `DRIVER_NOT_FOUND` | 404 | Entregador não encontrado (rotas admin) |
| `DRIVER_NOT_APPROVED` | 403 | Cadastro do entregador ainda não aprovado |
| `DRIVER_BLOCKED` | 403 | Entregador bloqueado pelo admin |
| `DRIVER_ON_DELIVERY` | 409 | Ação de status bloqueada porque há entrega em andamento |
| `DRIVER_ALREADY_ON_DELIVERY` | 409 | Tentou aceitar uma entrega já estando ocupado |
| `DELIVERY_NOT_FOUND` | 404 | Entrega não encontrada |
| `DELIVERY_ACCESS_DENIED` | 403 | Entregador não é o responsável por esta entrega |
| `DELIVERY_ALREADY_TAKEN` | 409 | **A corrida de aceite (seção 40-41)** — outro entregador já aceitou primeiro |
| `INVALID_DELIVERY_TRANSITION` | 409 | Ação fora da ordem (pickup antes de accept, etc.) |
| `DELIVERY_CODE_NOT_FOUND` | 404 | Sem código de confirmação gerado para esta entrega |
| `DELIVERY_CODE_EXPIRED` | 409 | Código expirado (24h) |
| `DELIVERY_CODE_LOCKED` | 409 | Excedeu o número máximo de tentativas (5) |
| `INVALID_DELIVERY_CODE` | 400 | Código digitado não confere |

### Financeiro / Assinatura

| Código | Status | Significado |
|---|---|---|
| `BATCH_EMPTY` | 400 | Lote de pagamento sem recebíveis selecionados |
| `RECEIVABLE_NOT_FOUND` | 404 | Recebível não encontrado |
| `RECEIVABLE_MISMATCH` | 400 | Recebíveis de entregadores/estabelecimentos diferentes no mesmo lote |
| `RECEIVABLE_ALREADY_BATCHED` | 409 | Recebível já pertence a outro lote |
| `PAYMENT_BATCH_NOT_FOUND` | 404 | Lote de pagamento não encontrado |
| `PAYMENT_BATCH_ACCESS_DENIED` | 403 | Lote pertence a outro entregador |
| `PAYMENT_NOT_REPORTED` | 409 | Entregador tentou confirmar recebimento antes do estabelecimento reportar |
| `SUBSCRIPTION_NOT_FOUND` | 404 | Assinatura não configurada para o estabelecimento |
| `INVALID_DELIVERY_FEE_MODE` | 400 | `urbanMode`/`ruralMode` inválido (deve ser `Fixed` ou `PerKm`) |

### Upload

| Código | Status | Significado |
|---|---|---|
| `FILE_REQUIRED` | 400 | Nenhum arquivo enviado |
| `EMPTY_FILE` | 400 | Arquivo vazio |
| `FILE_TOO_LARGE` | 400 | Acima de 5MB |
| `INVALID_FILE_TYPE` | 400 | Extensão ou conteúdo (magic bytes) não é uma imagem válida (jpg/png/webp) |

### Genéricos

| Código | Status | Significado |
|---|---|---|
| `VALIDATION_ERROR` | 422 | Um ou mais campos inválidos — ver `errors` |
| `INTERNAL_ERROR` | 500 | Erro não tratado |
