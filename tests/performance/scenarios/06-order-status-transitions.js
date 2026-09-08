// CENÁRIO 6 — Status de pedido: transições concorrentes.
//
// Cada trial cria um pedido e testa dois ângulos de concorrência:
//
// 1. Alterações concorrentes PERMITIDAS: N chamadas simultâneas de `accept` no MESMO pedido
//    Pending (ex.: dois atendentes clicando ao mesmo tempo). `accept` é idempotente
//    (docs/ORDER_FLOW.md) — todas devem responder 200, e o histórico do pedido
//    (`GET /api/orders/{id}`) deve conter exatamente UMA entrada "Accepted", nunca uma por
//    chamada concorrente.
// 2. Transições INVÁLIDAS continuam rejeitadas sob concorrência: N chamadas simultâneas de
//    `ready` num pedido ainda em `Accepted` (pular `preparing`) devem receber 409
//    INVALID_ORDER_TRANSITION, todas — nenhuma deve "escapar" e aplicar a transição por causa da
//    corrida.
import { check, sleep } from "k6";
import http from "k6/http";
import { apiGet, apiSend, expectStatus, parseJson, url } from "../lib/http.js";
import { bootstrapShop, orderBody } from "../lib/setup.js";
import { trialsExecutor, maxVUsFor } from "../lib/profiles.js";
import { randomId } from "../lib/config.js";
import { Counter } from "k6/metrics";

const PROFILE_OPTS = { smokeVUs: 1, smokeIterations: 3, loadVUs: 3, loadIterations: 10, stressVUsDefault: 5, stressIterationsDefault: 20 };
const CONCURRENT_CALLS = parseInt(__ENV.STATUS_CONCURRENT_CALLS || "4", 10);

const transitionAnomalies = new Counter("pedelocal_transition_anomaly_total");

http.setResponseCallback(http.expectedStatuses(200, 201, 409, 429));

export const options = {
  scenarios: { statusTransitions: trialsExecutor(PROFILE_OPTS) },
  thresholds: {
    "http_req_duration{name:orders:accept}": ["p(95)<1000"],
    pedelocal_transition_anomaly_total: ["count==0"],
  },
};

export function setup() {
  const shop = bootstrapShop();
  return {
    token: shop.customer.tokens.accessToken,
    merchantToken: shop.merchant.tokens.accessToken,
    establishmentId: shop.establishment.id,
    shop,
  };
}

function batchPost(path, token) {
  const requests = [];
  for (let i = 0; i < CONCURRENT_CALLS; i++) {
    requests.push({
      method: "POST",
      url: url(path),
      body: null,
      params: { headers: { Authorization: `Bearer ${token}`, "Content-Type": "application/json" }, tags: { name: "orders:accept" } },
    });
  }
  return http.batch(requests);
}

export default function (data) {
  const orderRes = apiSend(
    "POST",
    "/api/orders",
    orderBody(data.shop, { quantity: 1, idempotencyKey: randomId() }),
    data.token,
    "orders:create",
  );
  if (!expectStatus(orderRes, 201, "create order")) {
    sleep(1);
    return;
  }
  const order = parseJson(orderRes);
  const acceptPath = `/api/merchant/establishments/${data.establishmentId}/orders/${order.id}/accept`;
  const readyPath = `/api/merchant/establishments/${data.establishmentId}/orders/${order.id}/ready`;

  // --- 1. Concurrent ALLOWED transition: every concurrent accept must succeed idempotently. ---
  const acceptResponses = batchPost(acceptPath, data.merchantToken);
  const allAccepted = acceptResponses.every((r) => r.status === 200);
  check(null, { "concurrent accept: every call succeeds (idempotent)": () => allAccepted });
  if (!allAccepted) {
    transitionAnomalies.add(1);
    console.error(`[scenario 06] not all concurrent accepts succeeded for order ${order.id}: ${acceptResponses.map((r) => r.status)}`);
  }

  const afterAcceptRes = apiGet(`/api/orders/${order.id}`, data.token, "orders:getAfterAccept");
  if (expectStatus(afterAcceptRes, 200, "get order after concurrent accept")) {
    const afterAccept = parseJson(afterAcceptRes);
    const acceptedEntries = (afterAccept.statusHistory || []).filter((h) => h.status === "Accepted");
    const exactlyOneHistoryEntry = acceptedEntries.length === 1;
    check(afterAccept, {
      "concurrent accept: exactly one Accepted history entry (no duplicate side effects)": () => exactlyOneHistoryEntry,
    });
    if (!exactlyOneHistoryEntry) {
      transitionAnomalies.add(1);
      console.error(`[scenario 06] order ${order.id} has ${acceptedEntries.length} Accepted history entries, expected 1`);
    }
  }

  // --- 2. Concurrent INVALID transition: skipping "preparing" must stay rejected under load. ---
  const readyResponses = batchPost(readyPath, data.merchantToken);
  const allRejected = readyResponses.every((r) => r.status === 409);
  check(null, { "concurrent invalid transition: every call is rejected (409)": () => allRejected });
  if (!allRejected) {
    transitionAnomalies.add(1);
    console.error(
      `[scenario 06] an invalid ready-before-preparing transition was NOT rejected for order ${order.id}: ${readyResponses.map((r) => r.status)}`,
    );
  }
  readyResponses.forEach((r) => {
    const body = parseJson(r);
    check(body, { "invalid transition: error code is INVALID_ORDER_TRANSITION": (b) => !!b && b.code === "INVALID_ORDER_TRANSITION" });
  });

  sleep(0.5);
}
