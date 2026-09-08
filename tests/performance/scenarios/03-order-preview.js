// CENÁRIO 3 — Preview de pedido: POST /api/orders/preview sob carga concorrente.
//
// Vários clientes calculando subtotal/taxa/total ao mesmo tempo. Preview não persiste nada
// (docs/ORDER_FLOW.md) e roda exatamente o mesmo cálculo de POST /api/orders — então, com
// preço/taxa fixos no setup(), o valor esperado é DETERMINÍSTICO: todo VU, em toda iteração,
// deve receber o mesmo subtotal/taxa/total. Qualquer divergência sob carga é um bug real (ex.
// leitura de configuração de preço não seria thread-safe, cache incorreto, etc.).
import { check, sleep } from "k6";
import http from "k6/http";
import { apiSend, expectStatus, parseJson } from "../lib/http.js";
import { bootstrapShop, orderBody } from "../lib/setup.js";
import { rampingExecutor } from "../lib/profiles.js";
import { Counter } from "k6/metrics";

const QUANTITY = 2;
const inconsistentPreview = new Counter("pedelocal_preview_inconsistent_total");

// 201 covers setup()'s bootstrapShop() calls; 429 is tracked separately rather than failing the
// run — see docs/PERFORMANCE_TESTING.md.
http.setResponseCallback(http.expectedStatuses(200, 201, 429));

export const options = {
  scenarios: { preview: rampingExecutor({ smokeVUs: 5, loadVUs: 40, stressMaxVUsDefault: 100 }) },
  thresholds: {
    "http_req_duration{name:orders:preview}": ["p(95)<700", "p(99)<1500"],
    http_req_failed: ["rate<0.01"],
    pedelocal_preview_inconsistent_total: ["count==0"],
  },
};

export function setup() {
  const shop = bootstrapShop();
  const expectedSubtotal = shop.product.price * QUANTITY;
  const expectedFee = 5; // Fixed urban fee configured by lib/setup.js#putDeliveryPricing.
  return {
    token: shop.customer.tokens.accessToken,
    body: orderBody(shop, { quantity: QUANTITY }),
    expected: { subtotal: expectedSubtotal, deliveryFee: expectedFee, total: expectedSubtotal + expectedFee },
  };
}

export default function (data) {
  const res = apiSend("POST", "/api/orders/preview", data.body, data.token, "orders:preview");
  if (!expectStatus(res, 200, "POST /api/orders/preview")) {
    sleep(1);
    return;
  }

  const preview = parseJson(res);
  const consistent =
    !!preview &&
    preview.subtotal === data.expected.subtotal &&
    preview.deliveryFee === data.expected.deliveryFee &&
    preview.total === data.expected.total &&
    preview.total === preview.subtotal + preview.deliveryFee;

  check(preview, {
    "preview: subtotal/fee/total match the deterministic expected values": () => consistent,
  });
  if (!consistent) {
    inconsistentPreview.add(1);
    console.error(`Inconsistent preview: expected ${JSON.stringify(data.expected)}, got ${JSON.stringify(preview)}`);
  }

  sleep(1);
}
