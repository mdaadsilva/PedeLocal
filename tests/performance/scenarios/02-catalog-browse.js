// CENÁRIO 2 — Catálogo: cidade → estabelecimentos → produtos → detalhes.
//
// Provavelmente o fluxo de maior volume real (todo cliente navega antes de pedir). Mede leitura
// concorrente em GET /api/cities (público), GET /api/establishments?cityId= (autenticado),
// GET /api/establishments/{id} e GET /api/establishments/{id}/products.
//
// Não existe um endpoint de "detalhe de produto" isolado na API (só a lista por
// estabelecimento — ver docs/API_CONTRACT.md) — "detalhes" aqui é o detalhe do estabelecimento +
// a lista de produtos, que é o que o app realmente busca ao abrir uma loja.
//
// Um único cliente autenticado (token obtido uma vez em setup()) é reaproveitado por todos os
// VUs — leitura não muta estado, então não há motivo para logar de novo a cada VU/iteração
// (evita pressão desnecessária no rate limit de auth para um cenário que não é sobre login).
import { check, sleep } from "k6";
import http from "k6/http";
import { apiGet, expectStatus, parseJson } from "../lib/http.js";
import { bootstrapShop } from "../lib/setup.js";
import { rampingExecutor } from "../lib/profiles.js";

// 201 covers setup()'s bootstrapShop() calls (createCity/createEstablishment/etc.); 429 is
// tracked separately rather than failing the run — see docs/PERFORMANCE_TESTING.md.
http.setResponseCallback(http.expectedStatuses(200, 201, 429));

export const options = {
  scenarios: { catalog: rampingExecutor({ smokeVUs: 5, loadVUs: 50, stressMaxVUsDefault: 150 }) },
  thresholds: {
    "http_req_duration{name:public:cities}": ["p(95)<400"],
    "http_req_duration{name:catalog:establishments}": ["p(95)<600"],
    "http_req_duration{name:catalog:establishmentDetail}": ["p(95)<500"],
    "http_req_duration{name:catalog:products}": ["p(95)<600"],
    http_req_failed: ["rate<0.01"],
  },
};

export function setup() {
  const shop = bootstrapShop();
  return { token: shop.customer.tokens.accessToken, cityId: shop.city.id, establishmentId: shop.establishment.id };
}

export default function (data) {
  const citiesRes = apiGet("/api/cities", null, "public:cities");
  expectStatus(citiesRes, 200, "GET /api/cities");
  check(parseJson(citiesRes), { "cities: is a list": (b) => Array.isArray(b) });

  const listRes = apiGet(`/api/establishments?cityId=${data.cityId}&page=1&pageSize=20`, data.token, "catalog:establishments");
  if (expectStatus(listRes, 200, "GET /api/establishments")) {
    check(parseJson(listRes), { "establishments: is paged": (b) => !!b && Array.isArray(b.items) });
  }

  const detailRes = apiGet(`/api/establishments/${data.establishmentId}`, data.token, "catalog:establishmentDetail");
  expectStatus(detailRes, 200, "GET /api/establishments/{id}");

  const productsRes = apiGet(`/api/establishments/${data.establishmentId}/products`, data.token, "catalog:products");
  if (expectStatus(productsRes, 200, "GET /api/establishments/{id}/products")) {
    check(parseJson(productsRes), { "products: is a list": (b) => Array.isArray(b) });
  }

  sleep(Math.random() * 2 + 1); // think time between page loads.
}
