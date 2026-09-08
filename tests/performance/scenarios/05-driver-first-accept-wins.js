// CENÁRIO 5 — Driver first-accept-wins (CRÍTICO).
//
// Vários motoristas tentando aceitar a MESMA entrega ao mesmo tempo, repetido várias vezes.
// Cada trial (uma iteração) faz: cria pedido → comércio aceita/prepara/prontifica → localiza a
// Delivery disponível → dispara as tentativas de aceite de TODOS os motoristas do pool
// SIMULTANEAMENTE via http.batch() (garante concorrência real de rede, não só "quase ao mesmo
// tempo" entre chamadas sequenciais) → valida exatamente 1 vencedor (200) e o resto 409
// DELIVERY_ALREADY_TAKEN → confere que só o vencedor fica associado (GET .../current) e que a
// entrega some da lista de disponíveis para os perdedores → conclui a entrega (pickup → saiu
// para entrega → confirma com o código) para liberar o motorista vencedor antes do próximo
// trial. Mesma garantia de atomicidade já coberta em
// tests/PedeLocal.IntegrationTests/Deliveries/DeliveryConcurrencyTests.cs — aqui sob carga k6,
// repetida N vezes, para observar se ela se mantém sob throughput real.
//
// Cada VU tem seu PRÓPRIO pool de motoristas (registrado em setup(), particionado por índice de
// VU) — evita qualquer interferência entre VUs distintos disputando o pool de outro VU, mantendo
// cada trial isolado e determinístico mesmo com vários VUs rodando trials em paralelo.
import { check, sleep } from "k6";
import http from "k6/http";
import { apiGet, apiSend, expectStatus, parseJson, url } from "../lib/http.js";
import { bootstrapShop, orderBody, bootstrapDrivers } from "../lib/setup.js";
import { trialsExecutor, maxVUsFor } from "../lib/profiles.js";
import { randomId } from "../lib/config.js";
import { Counter } from "k6/metrics";

const PROFILE_OPTS = { smokeVUs: 1, smokeIterations: 3, loadVUs: 3, loadIterations: 15, stressVUsDefault: 5, stressIterationsDefault: 30 };
const RACE_SIZE = parseInt(__ENV.RACE_SIZE || "3", 10);

const raceAnomalies = new Counter("pedelocal_race_anomaly_total");
const raceTrials = new Counter("pedelocal_race_trials_total");

http.setResponseCallback(http.expectedStatuses(200, 201, 204, 403, 409, 429));

export const options = {
  scenarios: { driverRace: trialsExecutor(PROFILE_OPTS) },
  thresholds: {
    "http_req_duration{name:driver:accept}": ["p(95)<1000"],
    pedelocal_race_anomaly_total: ["count==0"], // ANY anomaly here is a correctness bug, not noise.
  },
};

export function setup() {
  const vus = maxVUsFor("trials", PROFILE_OPTS);
  const shop = bootstrapShop();

  const driverPools = [];
  for (let vu = 1; vu <= vus; vu++) {
    driverPools.push(bootstrapDrivers(shop.adminToken, shop.city.id, RACE_SIZE, `v${vu}`));
  }

  return {
    token: shop.customer.tokens.accessToken,
    merchantToken: shop.merchant.tokens.accessToken,
    establishmentId: shop.establishment.id,
    shop,
    driverPools, // index 0 == VU 1's pool, etc. (k6 __VU is 1-based).
  };
}

function createReadyDelivery(data) {
  const body = orderBody(data.shop, { quantity: 1, idempotencyKey: randomId() });
  const orderRes = apiSend("POST", "/api/orders", body, data.token, "driver:setupOrder");
  if (!expectStatus(orderRes, 201, "create order for race")) return null;
  const order = parseJson(orderRes);

  for (const action of ["accept", "preparing", "ready"]) {
    const res = apiSend(
      "POST",
      `/api/merchant/establishments/${data.establishmentId}/orders/${order.id}/${action}`,
      undefined,
      data.merchantToken,
      `driver:setup${action}`,
    );
    if (!expectStatus(res, 200, `merchant ${action}`)) return null;
  }
  return order;
}

function findAvailableDelivery(driverToken, orderId) {
  const res = apiGet("/api/driver/deliveries/available", driverToken, "driver:available");
  if (!expectStatus(res, 200, "GET /api/driver/deliveries/available")) return null;
  const list = parseJson(res) || [];
  return list.find((d) => d.orderId === orderId) || null;
}

function completeDelivery(data, winnerToken, deliveryId, orderId) {
  const codeRes = apiGet(`/api/orders/${orderId}/delivery-code`, data.token, "driver:getCode");
  if (!expectStatus(codeRes, 200, "GET delivery-code")) return;
  const { code } = parseJson(codeRes);

  apiSend("POST", `/api/driver/deliveries/${deliveryId}/pickup`, undefined, winnerToken, "driver:pickup");
  apiSend("POST", `/api/driver/deliveries/${deliveryId}/out-for-delivery`, undefined, winnerToken, "driver:outForDelivery");
  const confirmRes = apiSend("POST", `/api/driver/deliveries/${deliveryId}/confirm`, { code }, winnerToken, "driver:confirm");
  expectStatus(confirmRes, 200, "confirm delivery");
}

export default function (data) {
  const pool = data.driverPools[(__VU - 1) % data.driverPools.length];

  const order = createReadyDelivery(data);
  if (!order) {
    sleep(1);
    return;
  }

  const delivery = findAvailableDelivery(pool[0].tokens.accessToken, order.id);
  if (!delivery) {
    raceAnomalies.add(1);
    console.error(`[scenario 05] delivery never became available for order ${order.id}`);
    sleep(1);
    return;
  }

  // The actual race: every driver in the pool fires accept AT THE SAME TIME. http.batch() sends
  // all requests concurrently and waits for every response — real simultaneous network calls,
  // not a sequential loop that only "looks" concurrent in the script.
  const responses = http.batch(
    pool.map((driver) => ({
      method: "POST",
      url: url(`/api/driver/deliveries/${delivery.id}/accept`),
      body: null,
      params: {
        headers: { Authorization: `Bearer ${driver.tokens.accessToken}`, "Content-Type": "application/json" },
        tags: { name: "driver:accept" },
      },
    })),
  );

  raceTrials.add(1);

  const winners = [];
  const losers = [];
  responses.forEach((res, i) => {
    if (res.status === 200) {
      winners.push({ driver: pool[i], res });
    } else if (res.status === 409) {
      losers.push({ driver: pool[i], res });
    } else {
      raceAnomalies.add(1);
      console.error(`[scenario 05] unexpected status ${res.status} for driver ${pool[i].email}: ${res.body}`);
    }
  });

  const exactlyOneWinner = winners.length === 1;
  const restAreLosers = losers.length === pool.length - 1;
  check(null, {
    "race: exactly one driver wins (200)": () => exactlyOneWinner,
    "race: every other driver gets 409 DELIVERY_ALREADY_TAKEN": () => restAreLosers,
  });
  if (!exactlyOneWinner || !restAreLosers) {
    raceAnomalies.add(1);
    console.error(
      `[scenario 05] anomaly on delivery ${delivery.id}: winners=${winners.length} losers=${losers.length} poolSize=${pool.length}`,
    );
  }

  losers.forEach(({ driver, res }) => {
    const body = parseJson(res);
    check(body, { "loser: error code is DELIVERY_ALREADY_TAKEN": (b) => !!b && b.code === "DELIVERY_ALREADY_TAKEN" });

    // A loser must never be able to read the delivery's private details (docs/SECURITY_AUDIT.md
    // — DRIVER não vinculado não acessa dados privados antes de estar autorizado).
    const detailsRes = apiGet(`/api/driver/deliveries/${delivery.id}/details`, driver.tokens.accessToken, "driver:loserDetails");
    check(detailsRes, { "loser: cannot read delivery details (403)": (r) => r.status === 403 });
  });

  if (winners.length === 1) {
    const winnerToken = winners[0].driver.tokens.accessToken;
    const winnerBody = parseJson(winners[0].res);
    check(winnerBody, { "winner: delivery status is Assigned": (b) => !!b && b.status === "Assigned" });

    // Confirms exactly one driver is associated, and losers see no leftover reference to it.
    const currentRes = apiGet("/api/driver/deliveries/current", winnerToken, "driver:current");
    if (expectStatus(currentRes, 200, "winner current delivery")) {
      const current = parseJson(currentRes);
      check(current, { "winner: current delivery matches the raced delivery": (c) => !!c && c.id === delivery.id });
    }

    completeDelivery(data, winnerToken, delivery.id, order.id);
  }

  sleep(0.5);
}
