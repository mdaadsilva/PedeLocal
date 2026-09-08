// CENÁRIO 4 — Criação de pedidos: múltiplos pedidos simultâneos.
//
// Valida, sob carga concorrente real:
// - idempotência: o mesmo idempotencyKey enviado duas vezes (retry de cliente) nunca duplica o
//   pedido — as duas respostas devem trazer o mesmo orderId;
// - valores corretos: subtotal/taxa/total do pedido criado batem com o valor determinístico
//   esperado (mesmo cálculo do cenário 3);
// - estoque nunca fica negativo: o produto usa StockMode=Controlled com uma quantidade inicial
//   pequena e conhecida (calculada a partir do teto de VUs do perfil ativo) — vários pedidos
//   concorrentes disputam esse estoque no aceite do comércio (é no aceite, não na criação, que
//   o estoque é de fato debitado — docs/ORDER_FLOW.md), exercitando o token de concorrência
//   otimista de Product.Version (docs/SECURITY_AUDIT.md #4). 409 (PRODUCT_STOCK_CONFLICT ou
//   INSUFFICIENT_STOCK) é um resultado ESPERADO quando o estoque acaba, não uma falha do teste.
// - banco não entra em estado inconsistente: teardown() confere, via API (nunca acesso direto
//   ao banco — k6 não tem driver de banco), que `estoqueFinal == estoqueInicial - pedidosAceitos`
//   e que `estoqueFinal >= 0`.
//
// "Não gerar volume absurdo localmente": os perfis desta suíte nunca ultrapassam dezenas de VUs
// (load) ou um teto configurável e modesto (stress) — ver lib/profiles.js.
import { check, sleep } from "k6";
import http from "k6/http";
import { apiSend, expectStatus, parseJson } from "../lib/http.js";
import { bootstrapShop, orderBody, listAllMerchantOrders, getMerchantProduct } from "../lib/setup.js";
import { rampingExecutor, maxVUsFor } from "../lib/profiles.js";
import { randomId } from "../lib/config.js";
import { Counter } from "k6/metrics";

const PROFILE_OPTS = { smokeVUs: 5, loadVUs: 25, stressMaxVUsDefault: 60 };
const QUANTITY = 1;

const duplicateOrderDetected = new Counter("pedelocal_duplicate_order_total");
const valueMismatchDetected = new Counter("pedelocal_order_value_mismatch_total");
const stockConflicts = new Counter("pedelocal_stock_conflict_total");
const acceptedOrders = new Counter("pedelocal_orders_accepted_total");

http.setResponseCallback(http.expectedStatuses(200, 201, 409, 429));

export const options = {
  scenarios: { orderCreation: rampingExecutor(PROFILE_OPTS) },
  thresholds: {
    "http_req_duration{name:orders:create}": ["p(95)<1200"],
    http_req_failed: ["rate<0.02"], // 409 (stock exhausted) is excluded via setResponseCallback above.
    pedelocal_duplicate_order_total: ["count==0"],
    pedelocal_order_value_mismatch_total: ["count==0"],
  },
};

export function setup() {
  const maxVUs = maxVUsFor("ramping", PROFILE_OPTS);
  // Deliberately tight relative to max concurrency: guarantees some requests genuinely contend
  // for the last units, exercising the negative-stock guard instead of everyone trivially fitting.
  const initialStock = Math.max(1, Math.ceil(maxVUs / 2));

  const shop = bootstrapShop({ stockQuantity: initialStock });
  const expectedSubtotal = shop.product.price * QUANTITY;
  const expectedFee = 5;

  return {
    token: shop.customer.tokens.accessToken,
    merchantToken: shop.merchant.tokens.accessToken,
    establishmentId: shop.establishment.id,
    productId: shop.product.id,
    shop,
    initialStock,
    expected: { subtotal: expectedSubtotal, deliveryFee: expectedFee, total: expectedSubtotal + expectedFee },
  };
}

export default function (data) {
  const idempotencyKey = randomId();
  const body = orderBody(data.shop, { quantity: QUANTITY, idempotencyKey });

  const first = apiSend("POST", "/api/orders", body, data.token, "orders:create");
  if (first.status === 409) {
    // Expected once the deliberately tight initial stock (see setup()) has been fully consumed
    // by earlier iterations' accepts — creation itself checks current stock too (docs/ORDER_FLOW.md),
    // not just acceptance. Not a bug: the teardown() consistency check below is what actually
    // proves stock never went negative/inconsistent, regardless of how many creates this hits.
    stockConflicts.add(1);
    sleep(1);
    return;
  }
  if (!expectStatus(first, 201, "POST /api/orders (first)")) {
    sleep(1);
    return;
  }
  const firstOrder = parseJson(first);

  // Simulate a client retry (network blip, double-submit) with the SAME idempotencyKey.
  const retry = apiSend("POST", "/api/orders", body, data.token, "orders:createRetry");
  if (expectStatus(retry, 201, "POST /api/orders (retry)")) {
    const retryOrder = parseJson(retry);
    const isSameOrder = !!retryOrder && retryOrder.id === firstOrder.id;
    check(retryOrder, { "idempotent retry returns the SAME orderId, never a duplicate": () => isSameOrder });
    if (!isSameOrder) {
      duplicateOrderDetected.add(1);
      console.error(`Duplicate order from idempotent retry: first=${firstOrder.id} retry=${retryOrder?.id}`);
    }
  }

  const valuesOk =
    firstOrder.subtotal === data.expected.subtotal &&
    firstOrder.deliveryFee === data.expected.deliveryFee &&
    firstOrder.total === data.expected.total;
  check(firstOrder, { "order values match the deterministic expected total": () => valuesOk });
  if (!valuesOk) {
    valueMismatchDetected.add(1);
    console.error(`Order value mismatch: expected ${JSON.stringify(data.expected)}, got order ${firstOrder.id}`);
  }

  const acceptRes = apiSend(
    "POST",
    `/api/merchant/establishments/${data.establishmentId}/orders/${firstOrder.id}/accept`,
    undefined,
    data.merchantToken,
    "orders:accept",
  );
  if (acceptRes.status === 200) {
    acceptedOrders.add(1);
  } else if (acceptRes.status === 409) {
    stockConflicts.add(1); // Expected once stock runs out — see the module comment above.
  } else {
    expectStatus(acceptRes, 200, "POST .../accept"); // records the real failure via the usual check().
  }

  sleep(1);
}

export function teardown(data) {
  const accepted = listAllMerchantOrders(data.merchantToken, data.establishmentId, "Accepted");
  const acceptedQuantityForProduct = accepted.reduce((sum, order) => {
    const item = order.items.find((i) => i.productId === data.productId);
    return sum + (item ? item.quantity : 0);
  }, 0);

  const product = getMerchantProduct(data.merchantToken, data.establishmentId, data.productId);
  const expectedFinalStock = data.initialStock - acceptedQuantityForProduct;

  check(product, {
    "teardown: final stock is never negative": (p) => p.stockQuantity >= 0,
    "teardown: final stock matches initialStock - acceptedQuantity exactly (no lost/duplicated updates)": (p) =>
      p.stockQuantity === expectedFinalStock,
  });

  console.log(
    `[scenario 04] initialStock=${data.initialStock} acceptedOrders=${accepted.length} ` +
      `acceptedQuantity=${acceptedQuantityForProduct} finalStock=${product.stockQuantity} (expected ${expectedFinalStock})`,
  );
}
