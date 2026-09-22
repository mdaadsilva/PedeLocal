// Bootstrap helpers for k6 setup() phases — mirror tests/e2e/src/fixtures/world.ts closely so
// both suites exercise the exact same real API contracts (no invented endpoints, no shortcuts
// around auth/ownership). Every scenario's setup() runs ONCE before VUs start; the data it
// returns is shared read-only across all VUs for the rest of the run.
import { apiGet, apiSend, expectStatus, parseJson } from "./http.js";
import { config, uniqueSuffix } from "./config.js";

export function loginAdmin() {
  return login(config.adminEmail, config.adminPassword);
}

export function login(email, password) {
  const res = apiSend("POST", "/api/auth/login", { email, password }, null, "auth:login");
  if (!expectStatus(res, 200, "login")) {
    throw new Error(`setup(): login failed for ${email}: ${res.status} ${res.body}`);
  }
  return parseJson(res);
}

export function getCategories() {
  const res = apiGet("/api/establishment-categories", null, "public:categories");
  expectStatus(res, 200, "getCategories");
  return parseJson(res) || [];
}

export function createCity(adminToken, name, state = "SP") {
  const res = apiSend("POST", "/api/admin/cities", { name, state }, adminToken, "setup:createCity");
  if (!expectStatus(res, 201, "createCity")) {
    throw new Error(`setup(): createCity failed: ${res.status} ${res.body}`);
  }
  return parseJson(res);
}

export function createEstablishment(adminToken, body) {
  const res = apiSend("POST", "/api/admin/establishments", body, adminToken, "setup:createEstablishment");
  if (!expectStatus(res, 201, "createEstablishment")) {
    throw new Error(`setup(): createEstablishment failed: ${res.status} ${res.body}`);
  }
  return parseJson(res);
}

export function setEstablishmentStatus(adminToken, establishmentId, body) {
  const res = apiSend(
    "PATCH",
    `/api/admin/establishments/${establishmentId}/status`,
    body,
    adminToken,
    "setup:setEstablishmentStatus",
  );
  if (!expectStatus(res, 200, "setEstablishmentStatus")) {
    throw new Error(`setup(): setEstablishmentStatus failed: ${res.status} ${res.body}`);
  }
  return parseJson(res);
}

export function putDeliveryPricing(merchantToken, establishmentId) {
  const res = apiSend(
    "PUT",
    `/api/merchant/establishments/${establishmentId}/delivery-pricing`,
    // Flat fee model (Urban/Rural x Fixed/PerKm was removed) — 5 keeps
    // scenarios 03/04's `expectedFee = 5` valid, 50 mirrors the old
    // maxDistanceKm so it comfortably covers the ~150m customer address
    // bootstrapShop creates.
    { deliveryFee: 5, deliveryRadiusKm: 50 },
    merchantToken,
    "setup:putDeliveryPricing",
  );
  if (!expectStatus(res, 200, "putDeliveryPricing")) {
    throw new Error(`setup(): putDeliveryPricing failed: ${res.status} ${res.body}`);
  }
}

export function createMenuCategory(merchantToken, establishmentId, name) {
  const res = apiSend(
    "POST",
    `/api/merchant/establishments/${establishmentId}/menu-categories`,
    { name, sortOrder: 1 },
    merchantToken,
    "setup:createMenuCategory",
  );
  if (!expectStatus(res, 201, "createMenuCategory")) {
    throw new Error(`setup(): createMenuCategory failed: ${res.status} ${res.body}`);
  }
  return parseJson(res);
}

export function createProduct(merchantToken, establishmentId, menuCategoryId, name = "X-Burger Perf", price = 20, stock = null) {
  const res = apiSend(
    "POST",
    `/api/merchant/establishments/${establishmentId}/products`,
    {
      menuCategoryId,
      name,
      description: "Produto gerado pela suíte de performance",
      price,
      stockMode: stock === null ? "Uncontrolled" : "Controlled",
      stockQuantity: stock === null ? undefined : stock,
      optionGroups: [],
    },
    merchantToken,
    "setup:createProduct",
  );
  if (!expectStatus(res, 201, "createProduct")) {
    throw new Error(`setup(): createProduct failed: ${res.status} ${res.body}`);
  }
  return parseJson(res);
}

export function registerCustomer(input) {
  const res = apiSend("POST", "/api/auth/register/customer", input, null, "auth:registerCustomer");
  if (!expectStatus(res, 201, "registerCustomer")) {
    throw new Error(`setup(): registerCustomer failed: ${res.status} ${res.body}`);
  }
  return parseJson(res);
}

export function createAddress(customerToken, cityId, latitude, longitude) {
  const res = apiSend(
    "POST",
    "/api/me/addresses",
    {
      cityId,
      type: "Urban",
      label: "Casa Perf",
      street: "Rua do Cliente",
      number: "50",
      district: "Centro",
      zipCode: "17370000",
      latitude,
      longitude,
    },
    customerToken,
    "setup:createAddress",
  );
  if (!expectStatus(res, 201, "createAddress")) {
    throw new Error(`setup(): createAddress failed: ${res.status} ${res.body}`);
  }
  return parseJson(res);
}

export function registerDriver(input) {
  const res = apiSend("POST", "/api/auth/register/driver", input, null, "auth:registerDriver");
  if (!expectStatus(res, 201, "registerDriver")) {
    throw new Error(`setup(): registerDriver failed: ${res.status} ${res.body}`);
  }
  return parseJson(res);
}

export function approveDriver(adminToken, driverId) {
  const res = apiSend("POST", `/api/admin/drivers/${driverId}/approve`, undefined, adminToken, "setup:approveDriver");
  if (!expectStatus(res, 200, "approveDriver")) {
    throw new Error(`setup(): approveDriver failed: ${res.status} ${res.body}`);
  }
  return parseJson(res);
}

/** Onboarding now requires CNH + vehicle on file before a SUPER_ADMIN can approve
 * (DRIVER_LICENSE_REQUIRED / DRIVER_VEHICLE_REQUIRED otherwise) — called with the driver's
 * OWN token, before approveDriver. */
export function submitDriverLicense(driverToken) {
  const expiresAt = new Date();
  expiresAt.setFullYear(expiresAt.getFullYear() + 3);
  const res = apiSend(
    "PUT",
    "/api/driver/me/documents/license",
    {
      registrationNumber: `PERF${Date.now()}`.slice(0, 32),
      category: "A",
      expiresAt: expiresAt.toISOString().slice(0, 10),
      hasEar: true,
      documentImageUrl: null,
    },
    driverToken,
    "setup:submitDriverLicense",
  );
  if (!expectStatus(res, 200, "submitDriverLicense")) {
    throw new Error(`setup(): submitDriverLicense failed: ${res.status} ${res.body}`);
  }
}

export function submitDriverVehicle(driverToken) {
  const res = apiSend(
    "PUT",
    "/api/driver/me/documents/vehicle",
    {
      type: "Motorcycle",
      plate: `PERF${Date.now()}`.slice(0, 16),
      model: "CG 160",
      color: "Preto",
      documentImageUrl: null,
    },
    driverToken,
    "setup:submitDriverVehicle",
  );
  if (!expectStatus(res, 200, "submitDriverVehicle")) {
    throw new Error(`setup(): submitDriverVehicle failed: ${res.status} ${res.body}`);
  }
}

export function driverOnline(driverToken) {
  const res = apiSend("POST", "/api/driver/me/status/online", undefined, driverToken, "setup:driverOnline");
  if (!expectStatus(res, 200, "driverOnline")) {
    throw new Error(`setup(): driverOnline failed: ${res.status} ${res.body}`);
  }
  return parseJson(res);
}

function uniquePhone(prefix, suffix) {
  const digits = suffix.replace(/[^0-9]/g, "").slice(-8).padStart(8, "0");
  return `${prefix}${digits}`.slice(0, 11);
}

function uniqueCpf(suffix) {
  const digits = suffix.replace(/[^0-9]/g, "").padEnd(11, "1").slice(0, 11);
  return digits.replace(/^0/, "1");
}

/** Full isolated shop — one city/establishment/product/merchant, plus a customer+address ready
 * to place orders. Uses PEDELOCAL_CUSTOMER_EMAIL/PASSWORD instead of self-registering when both
 * are set (staging convenience — a stable pool of accounts instead of piling up new ones on
 * every run), exactly the same override the login scenario (01) supports. */
export function bootstrapShop(options = {}) {
  const suffix = uniqueSuffix();
  const password = config.testPassword;

  const adminTokens = loginAdmin();
  const adminToken = adminTokens.accessToken;

  const categories = getCategories();
  const category = categories.find((c) => c.name === "Lanchonete") || categories[0];
  if (!category) {
    throw new Error("setup(): no establishment category seeded — is the target API's DbSeeder up to date?");
  }

  const city = createCity(adminToken, `Perf ${suffix}`, "SP");

  const merchantEmail = `loja-perf-${suffix}@perf.pedelocal.dev`;
  const establishment = createEstablishment(adminToken, {
    cityId: city.id,
    name: `Loja Perf ${suffix}`,
    document: `PERF${suffix}`.slice(0, 32),
    phone: uniquePhone("14991", suffix),
    email: `contato-perf-${suffix}@perf.pedelocal.dev`,
    categoryId: category.id,
    street: "Rua da Loja",
    number: "100",
    district: "Centro",
    complement: null,
    zipCode: "17370000",
    latitude: -21.85,
    longitude: -49.9,
    adminName: `Admin Perf ${suffix}`,
    adminEmail: merchantEmail,
    adminPhone: uniquePhone("14992", suffix),
    adminPassword: password,
  });
  setEstablishmentStatus(adminToken, establishment.id, { operationalStatus: "Open" });

  const merchantTokens = login(merchantEmail, password);
  putDeliveryPricing(merchantTokens.accessToken, establishment.id);
  const menu = createMenuCategory(merchantTokens.accessToken, establishment.id, "Lanches");
  const product = createProduct(
    merchantTokens.accessToken,
    establishment.id,
    menu.id,
    "X-Burger Perf",
    20,
    options.stockQuantity !== undefined ? options.stockQuantity : null,
  );

  let customerEmail = config.customerEmail;
  let customerPassword = config.customerPassword || password;
  if (!customerEmail) {
    customerEmail = `cliente-perf-${suffix}@perf.pedelocal.dev`;
    customerPassword = password;
    registerCustomer({
      name: `Cliente Perf ${suffix}`,
      email: customerEmail,
      phone: uniquePhone("14993", suffix),
      password: customerPassword,
    });
  }
  const customerTokens = login(customerEmail, customerPassword);
  const address = createAddress(customerTokens.accessToken, city.id, -21.851, -49.901);

  return {
    suffix,
    adminToken,
    city,
    establishment,
    product,
    merchant: { email: merchantEmail, password, tokens: merchantTokens },
    customer: { email: customerEmail, password: customerPassword, tokens: customerTokens },
    address,
  };
}

/** Registers `count` approved, online drivers in `cityId` — used by the catalog/browse-style
 * setup where drivers aren't needed, and by scenario 05's per-VU racing pools. */
export function bootstrapDrivers(adminToken, cityId, count, labelPrefix = "d") {
  const drivers = [];
  for (let i = 0; i < count; i++) {
    const suffix = `${uniqueSuffix()}${labelPrefix}${i}`;
    const email = `motorista-perf-${suffix}@perf.pedelocal.dev`;
    const registered = registerDriver({
      name: `Motorista Perf ${suffix}`,
      cpf: uniqueCpf(suffix),
      phone: uniquePhone("14994", suffix),
      email,
      password: config.testPassword,
      cityId,
      vehicleType: "Motorcycle",
      pixKeyType: "Email",
      pixKey: email,
    });
    const tokens = login(email, config.testPassword);
    submitDriverLicense(tokens.accessToken);
    submitDriverVehicle(tokens.accessToken);
    approveDriver(adminToken, registered.driverId);
    driverOnline(tokens.accessToken);
    drivers.push({ email, driverId: registered.driverId, tokens });
  }
  return drivers;
}

export function getMerchantProduct(merchantToken, establishmentId, productId) {
  const res = apiGet(`/api/merchant/establishments/${establishmentId}/products/${productId}`, merchantToken, "verify:getProduct");
  if (!expectStatus(res, 200, "getMerchantProduct")) {
    throw new Error(`teardown(): getMerchantProduct failed: ${res.status} ${res.body}`);
  }
  return parseJson(res);
}

/** Pages through every merchant order in `status` — modest data volumes expected from this
 * suite's own profiles (never thousands, per this suite's rules), so a simple page loop is fine. */
export function listAllMerchantOrders(merchantToken, establishmentId, status) {
  const all = [];
  let page = 1;
  for (;;) {
    const res = apiGet(
      `/api/merchant/establishments/${establishmentId}/orders?status=${status}&page=${page}&pageSize=100`,
      merchantToken,
      "verify:listOrders",
    );
    if (!expectStatus(res, 200, "listAllMerchantOrders")) {
      throw new Error(`teardown(): listAllMerchantOrders failed: ${res.status} ${res.body}`);
    }
    const body = parseJson(res);
    all.push(...(body?.items || []));
    if (!body || page >= body.totalPages) {
      break;
    }
    page += 1;
  }
  return all;
}

export function orderBody(shop, overrides = {}) {
  const orderType = overrides.orderType || "Delivery";
  const addressId = orderType === "Pickup" ? null : overrides.addressId || shop.address.id;
  return {
    establishmentId: shop.establishment.id,
    addressId,
    paymentMethod: overrides.paymentMethod || "Pix",
    changeForAmount: null,
    items: [{ productId: shop.product.id, quantity: overrides.quantity || 2, options: [] }],
    idempotencyKey: overrides.idempotencyKey,
    orderType,
    notes: overrides.notes || "pedido k6",
  };
}
