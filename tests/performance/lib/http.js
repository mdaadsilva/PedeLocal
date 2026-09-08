import http from "k6/http";
import { check } from "k6";
import { Counter } from "k6/metrics";
import { config } from "./config.js";

/** Requests that got a 429 are tracked separately from real failures — the whole point of
 * respecting rate limits (per this suite's rule: never weaken them for testing) is that hitting
 * one under load is an expected, informative outcome, not a bug. Threshold on `http_req_failed`
 * only reflects unexpected errors; check docs/PERFORMANCE_TESTING.md for how the two interact. */
export const rateLimited = new Counter("pedelocal_rate_limited_total");

export function jsonHeaders(token) {
  const headers = { "Content-Type": "application/json" };
  if (token) {
    headers["Authorization"] = `Bearer ${token}`;
  }
  return headers;
}

export function url(path) {
  return `${config.apiUrl}${path}`;
}

/** GET with bearer auth, tagged for per-endpoint metrics breakdown. */
export function apiGet(path, token, name) {
  const res = http.get(url(path), { headers: jsonHeaders(token), tags: { name: name || path } });
  trackRateLimit(res);
  return res;
}

/** POST/PUT/DELETE JSON with bearer auth, tagged for per-endpoint metrics breakdown. */
export function apiSend(method, path, body, token, name) {
  const res = http.request(method, url(path), body !== undefined ? JSON.stringify(body) : null, {
    headers: jsonHeaders(token),
    tags: { name: name || path },
  });
  trackRateLimit(res);
  return res;
}

function trackRateLimit(res) {
  if (res.status === 429) {
    rateLimited.add(1, { name: res.request ? res.request.tags?.name : undefined });
  }
}

/** Common check: status is one of `expected`, logs the body on mismatch (skipped for 429 — that
 * path is tracked via `rateLimited` instead of failing the check, per this suite's rate-limit
 * policy). Returns whether the response was usable (i.e. not itself a rate-limit rejection). */
export function expectStatus(res, expected, label) {
  const allowed = Array.isArray(expected) ? expected : [expected];
  if (res.status === 429) {
    return false;
  }
  const ok = check(res, {
    [`${label || res.request?.url}: status is ${allowed.join("|")}`]: (r) => allowed.includes(r.status),
  });
  if (!ok) {
    console.error(`Unexpected status ${res.status} for ${label || res.url}: ${res.body?.slice(0, 300)}`);
  }
  return ok;
}

export function parseJson(res) {
  try {
    return res.json();
  } catch (e) {
    console.error(`Failed to parse JSON from ${res.url}: ${String(e)}`);
    return null;
  }
}
