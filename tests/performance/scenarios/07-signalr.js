// CENÁRIO 7 — SignalR: conexões simultâneas, entrega de eventos, estabilidade.
//
// k6 não tem um cliente SignalR pronto — implementa o protocolo JSON do Hub diretamente sobre
// `k6/ws` (módulo estável, não o experimental): POST .../negotiate (autenticado, negotiateVersion=1)
// para obter um connectionToken — negotiateVersion=1 devolve tanto `connectionId` (só exibição)
// quanto `connectionToken`, e é o `connectionToken` que a URL de transporte espera em `id=` (usar
// `connectionId` aí retorna 404 "No Connection with that ID" — detalhe de protocolo não
// documentado no nosso próprio docs/REALTIME_EVENTS.md, que fala só do access_token). Abre o
// WebSocket em `{hub}?id={connectionToken}&access_token={jwt}` (token via query string, mesma
// convenção documentada em docs/REALTIME_EVENTS.md — WebSocket de browser não permite header
// customizado no handshake), envia o handshake
// `{"protocol":"json","version":1}` terminado em `\x1e` (record separator do protocolo), espera
// o ack `{}`, e a partir daí escuta invocações (`{"type":1,"target":"...","arguments":[...]}`).
//
// Mede: taxa de conexões bem-sucedidas, latência do handshake, e — de ponta a ponta — se um
// evento de domínio real (`OrderStatusChanged`, disparado por um `accept` de pedido feito por
// ESTE MESMO VU logo após conectar) chega na conexão dentro de um timeout. Isso é uma checagem
// de entrega real, não uma mensagem sintética.
//
// LIMITAÇÃO DOCUMENTADA (ver docs/PERFORMANCE_TESTING.md#cenário-7-signalr): correlacionar
// eventos entre VUs *independentes* (ex.: "VU B recebe o evento causado pelo pedido do VU A")
// não é praticamente testável em k6 puro — VUs são processos JS isolados sem canal de
// coordenação em tempo real nativo. Por isso cada VU aqui dispara e escuta o PRÓPRIO evento
// (mesma conta de cliente, compartilhada entre VUs só para esta finalidade — o isolamento por
// usuário/grupo já está coberto por testes de integração .NET reais, ver
// tests/PedeLocal.IntegrationTests/Security/HubAuthorizationTests.cs). Reconexão automática do
// SDK cliente (backoff exponencial) também não é simulada aqui — é comportamento do
// `@microsoft/signalr` do lado cliente, fora do que este script de protocolo cru replica; este
// cenário mede o que o SERVIDOR entrega sob N conexões simultâneas, não o comportamento do SDK.
import ws from "k6/ws";
import http from "k6/http";
import { check, sleep } from "k6";
import { Counter, Rate, Trend } from "k6/metrics";
import { config, randomId, wsUrl } from "../lib/config.js";
import { apiSend, expectStatus, parseJson } from "../lib/http.js";
import { bootstrapShop, orderBody } from "../lib/setup.js";
import { rampingExecutor } from "../lib/profiles.js";

const RECORD_SEPARATOR = "\x1e";
const EVENT_WAIT_TIMEOUT_MS = 8000;

const connectSuccess = new Rate("pedelocal_ws_connect_success");
const handshakeDuration = new Trend("pedelocal_ws_handshake_ms", true);
const eventDelivered = new Rate("pedelocal_ws_event_delivered");
const eventLatency = new Trend("pedelocal_ws_event_delivery_ms", true);
const unexpectedClose = new Counter("pedelocal_ws_unexpected_close_total");

http.setResponseCallback(http.expectedStatuses(200, 201, 429));

export const options = {
  scenarios: { signalr: rampingExecutor({ smokeVUs: 3, loadVUs: 20, stressMaxVUsDefault: 60 }) },
  thresholds: {
    pedelocal_ws_connect_success: ["rate>0.98"],
    pedelocal_ws_handshake_ms: ["p(95)<1000"],
    // event delivery threshold is intentionally lenient at first — see docs/PERFORMANCE_TESTING.md.
    pedelocal_ws_event_delivered: ["rate>0.95"],
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

function negotiate(token) {
  const res = apiSend("POST", "/hubs/pedelocal/negotiate?negotiateVersion=1", undefined, token, "signalr:negotiate");
  if (!expectStatus(res, 200, "SignalR negotiate")) return null;
  return parseJson(res);
}

/** Creates and immediately accepts an order for THIS VU's customer — the accept triggers
 * OrderStatusChanged on the customer:{userId} group the WS connection just joined. */
function triggerOwnStatusChangeEvent(data) {
  const body = orderBody(data.shop, { quantity: 1, idempotencyKey: randomId() });
  const orderRes = apiSend("POST", "/api/orders", body, data.token, "signalr:createOrder");
  if (!expectStatus(orderRes, 201, "create order for SignalR trigger")) return null;
  const order = parseJson(orderRes);

  const acceptRes = apiSend(
    "POST",
    `/api/merchant/establishments/${data.establishmentId}/orders/${order.id}/accept`,
    undefined,
    data.merchantToken,
    "signalr:acceptOrder",
  );
  if (!expectStatus(acceptRes, 200, "accept order for SignalR trigger")) return null;
  return order.id;
}

export default function (data) {
  const negotiated = negotiate(data.token);
  if (!negotiated || !negotiated.connectionToken) {
    connectSuccess.add(false);
    sleep(1);
    return;
  }

  const socketUrl = `${wsUrl()}?id=${negotiated.connectionToken}&access_token=${data.token}`;
  const connectStart = Date.now();
  let handshakeAcked = false;
  let expectedOrderId = null;
  let delivered = false;

  const wsRes = ws.connect(socketUrl, {}, function (socket) {
    socket.on("open", () => {
      socket.send(JSON.stringify({ protocol: "json", version: 1 }) + RECORD_SEPARATOR);
    });

    socket.on("message", (raw) => {
      for (const chunk of raw.split(RECORD_SEPARATOR)) {
        if (!chunk) continue;
        let parsed;
        try {
          parsed = JSON.parse(chunk);
        } catch (e) {
          continue;
        }

        if (!handshakeAcked && Object.keys(parsed).length === 0) {
          handshakeAcked = true;
          handshakeDuration.add(Date.now() - connectStart);
          expectedOrderId = triggerOwnStatusChangeEvent(data);
          continue;
        }

        if (parsed.type === 6) {
          // Server keep-alive ping — reply in kind (SignalR JSON protocol) to avoid the server
          // closing an otherwise-idle connection during a longer STRESS run.
          socket.send(JSON.stringify({ type: 6 }) + RECORD_SEPARATOR);
          continue;
        }

        if (parsed.type === 1 && parsed.target === "OrderStatusChanged") {
          const [payload] = parsed.arguments || [];
          if (payload && expectedOrderId && payload.orderId === expectedOrderId) {
            delivered = true;
            eventLatency.add(Date.now() - connectStart);
            socket.close();
          }
        }
      }
    });

    socket.on("error", (e) => {
      console.error(`[scenario 07] WS error: ${String(e)}`);
    });

    socket.setTimeout(() => {
      if (!delivered) {
        socket.close();
      }
    }, EVENT_WAIT_TIMEOUT_MS);
  });

  const connected = !!wsRes && wsRes.status === 101;
  connectSuccess.add(connected);
  check(wsRes, { "SignalR: WebSocket upgrade succeeds (101)": () => connected });

  if (connected && !handshakeAcked) {
    unexpectedClose.add(1);
    console.error("[scenario 07] connection closed before the SignalR handshake was acknowledged");
  }

  eventDelivered.add(delivered);
  check(null, { "SignalR: OrderStatusChanged is delivered to this connection within the timeout": () => delivered });

  sleep(1);
}
