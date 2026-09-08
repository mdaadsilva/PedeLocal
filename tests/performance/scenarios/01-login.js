// CENÁRIO 1 — Login: autenticação simultânea com carga gradual e concorrência.
//
// Mede p50/p95/p99, throughput e taxa de erro de POST /api/auth/login. Usa um pequeno POOL de
// contas VÁLIDAS pré-existentes (via PEDELOCAL_CUSTOMER_EMAIL/PASSWORD) ou, na ausência delas,
// registra um pool pequeno em setup() — nunca tenta senha errada nem varre credenciais
// (proibido pelo enunciado: "não realizar brute force").
//
// POST /api/auth/login está sob a policy `auth` (rate limit mais restritivo, default 5/60s por
// IP — Program.cs). Este script NUNCA tenta contornar isso; 429 é medido separadamente
// (`pedelocal_rate_limited_total`) e NÃO conta como falha de threshold. Para medir o
// comportamento do login sob concorrência real (perfis load/stress), configure
// RateLimiting__AuthPermitLimit no ambiente de teste — exatamente a mesma orientação já usada
// pelo E2E (docs/E2E_TESTING.md) — nunca o default de produção. Ver docs/PERFORMANCE_TESTING.md.
import http from "k6/http";
import { check, sleep } from "k6";
import { config, uniqueSuffix } from "../lib/config.js";
import { apiSend, expectStatus, parseJson, rateLimited } from "../lib/http.js";
import { registerCustomer } from "../lib/setup.js";
import { rampingExecutor, maxVUsFor } from "../lib/profiles.js";

const LOGIN_POOL_SIZE = parseInt(__ENV.LOGIN_POOL_SIZE || "5", 10);

// 200/201 cover both the scenario itself (login) and setup()'s own registerCustomer calls; 429
// is tracked separately (pedelocal_rate_limited_total) rather than failing the run — see the
// module comment above and docs/PERFORMANCE_TESTING.md.
http.setResponseCallback(http.expectedStatuses(200, 201, 429));

export const options = {
  scenarios: { login: rampingExecutor({ smokeVUs: 5, loadVUs: 30, stressMaxVUsDefault: 80 }) },
  thresholds: {
    // Initial, deliberately unaggressive thresholds — refine after a real staging baseline.
    // See docs/PERFORMANCE_TESTING.md#thresholds.
    // p99 is deliberately generous: BCrypt (cost factor 12, by design — docs/AUTH_FLOW.md) makes
    // login CPU-bound and inherently slower than a typical read endpoint; a local smoke run
    // already showed p99 ~1.5s at only 5 VUs. Refine after a real staging baseline — see
    // docs/PERFORMANCE_TESTING.md#gargalo-observado-login-bcrypt.
    "http_req_duration{name:auth:login}": ["p(95)<1200", "p(99)<2500"],
    http_req_failed: ["rate<0.01"],
  },
};

export function setup() {
  if (config.customerEmail && config.customerPassword) {
    return { accounts: [{ email: config.customerEmail, password: config.customerPassword }] };
  }

  const maxVUs = maxVUsFor("ramping", { smokeVUs: 5, loadVUs: 30, stressMaxVUsDefault: 80 });
  const poolSize = Math.max(1, Math.min(LOGIN_POOL_SIZE, maxVUs));
  const accounts = [];
  for (let i = 0; i < poolSize; i++) {
    const suffix = `${uniqueSuffix()}l${i}`;
    const email = `login-perf-${suffix}@perf.pedelocal.dev`;
    registerCustomer({
      name: `Login Perf ${suffix}`,
      email,
      phone: `1499${String(i).padStart(7, "0")}`.slice(0, 11),
      password: config.testPassword,
    });
    accounts.push({ email, password: config.testPassword });
  }
  return { accounts };
}

export default function (data) {
  const account = data.accounts[__VU % data.accounts.length];

  const res = apiSend("POST", "/api/auth/login", account, null, "auth:login");

  if (res.status === 429) {
    rateLimited.add(1, { name: "auth:login" });
  } else {
    const ok = expectStatus(res, 200, "login");
    if (ok) {
      const body = parseJson(res);
      check(body, {
        "login: returns accessToken": (b) => !!b && typeof b.accessToken === "string" && b.accessToken.length > 0,
        "login: returns refreshToken": (b) => !!b && typeof b.refreshToken === "string" && b.refreshToken.length > 0,
      });
    }
  }

  sleep(1);
}
