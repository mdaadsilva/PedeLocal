// Shared configuration for every k6 script in this suite — mirrors tests/e2e/src/config.ts's
// env-var-driven convention. Nothing here is a secret by itself; real credentials always come
// from the environment (see .env.example), never hardcoded.

function env(name, fallback) {
  const value = __ENV[name];
  return value !== undefined && value !== "" ? value : fallback;
}

function trimTrailingSlash(url) {
  return url.replace(/\/+$/, "");
}

export const config = {
  apiUrl: trimTrailingSlash(env("PEDELOCAL_API_URL", "http://localhost:8080")),

  adminEmail: env("PEDELOCAL_ADMIN_EMAIL", "admin@pedelocal.dev"),
  adminPassword: env("PEDELOCAL_ADMIN_PASSWORD", "Senha123!"),

  // Optional: reuse pre-existing accounts instead of self-registering fresh ones every setup()
  // run — useful in staging where an operator wants a stable, known pool of test accounts
  // instead of accumulating new users on every k6 run.
  customerEmail: env("PEDELOCAL_CUSTOMER_EMAIL", ""),
  customerPassword: env("PEDELOCAL_CUSTOMER_PASSWORD", ""),
  driverEmail: env("PEDELOCAL_DRIVER_EMAIL", ""),
  driverPassword: env("PEDELOCAL_DRIVER_PASSWORD", ""),

  // Password used for any account this suite registers itself (city/shop/customer/drivers).
  testPassword: env("PEDELOCAL_TEST_PASSWORD", "Senha123!"),

  // smoke | load | stress — see lib/profiles.js and docs/PERFORMANCE_TESTING.md.
  profile: env("PROFILE", "smoke"),
};

export function wsUrl() {
  return config.apiUrl.replace(/^http/, "ws") + "/hubs/pedelocal";
}

/** Short, collision-resistant suffix for test data names/emails — no Node crypto in k6. */
export function uniqueSuffix() {
  return `${Date.now().toString(36)}${Math.floor(Math.random() * 1e8).toString(36)}${__VU}`;
}

/** Good-enough unique string for idempotencyKey / one-off correlation ids — the API only
 * requires it be a client-unique string, not a real UUID. */
export function randomId() {
  return `k6-${Date.now().toString(36)}-${__VU}-${__ITER}-${Math.floor(Math.random() * 1e8).toString(36)}`;
}
