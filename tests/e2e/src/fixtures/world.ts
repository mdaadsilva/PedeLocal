import { randomBytes } from "node:crypto";
import { e2eConfig } from "../config";
import { ApiClient } from "../api/client";
import type { Address, AuthTokens, City, Establishment, Product } from "../api/types";

export type Actor = {
  email: string;
  password: string;
  tokens: AuthTokens;
};

export type IsolatedShop = {
  suffix: string;
  city: City;
  establishment: Establishment;
  product: Product;
  merchant: Actor;
  customer: Actor;
  address: Address;
  adminToken: string;
};

let cachedAdminTokens: AuthTokens | null = null;

export async function loginAsAdmin(api: ApiClient): Promise<AuthTokens> {
  if (cachedAdminTokens) return cachedAdminTokens;
  cachedAdminTokens = await api.login(e2eConfig.adminEmail, e2eConfig.adminPassword);
  return cachedAdminTokens;
}

function uniqueId(): string {
  return `${Date.now().toString(36)}${randomBytes(3).toString("hex")}`;
}

function uniqueCpf(seed: string): string {
  const hash = randomBytes(6).toString("hex") + seed.replace(/\D/g, "");
  const digits = hash.replace(/[a-f]/g, "1").slice(0, 11).padEnd(11, "8");
  return digits.replace(/^0/, "1");
}

function uniquePhone(suffix: string, prefix: string): string {
  const digits = suffix.replace(/\D/g, "").slice(-8).padStart(8, "0");
  return `${prefix}${digits}`.slice(0, 11);
}

export async function bootstrapShop(
  api: ApiClient,
  options: { withDriver?: boolean } = {},
): Promise<IsolatedShop & { driver?: Actor & { driverId: string } }> {
  const suffix = uniqueId();
  const password = e2eConfig.testPassword;

  const adminTokens = await loginAsAdmin(api);
  const adminToken = adminTokens.accessToken;

  const categories = await api.getCategories();
  const category =
    categories.find((item) => item.name === "Lanchonete") ?? categories[0];
  if (!category) {
    throw new Error("Nenhuma categoria de estabelecimento seedada.");
  }

  const city = await api.createCity(adminToken, `E2E ${suffix}`, "SP");

  const merchantEmail = `loja-${suffix}@e2e.pedelocal.dev`;
  const establishment = await api.createEstablishment(adminToken, {
    cityId: city.id,
    name: `Lanchonete E2E ${suffix}`,
    document: `E2E${suffix}`.slice(0, 32),
    phone: uniquePhone(suffix, "14991"),
    email: `contato-${suffix}@e2e.pedelocal.dev`,
    categoryId: category.id,
    street: "Rua da Loja",
    number: "100",
    district: "Centro",
    complement: null,
    zipCode: "17370000",
    latitude: -21.85,
    longitude: -49.9,
    adminName: `Admin E2E ${suffix}`,
    adminEmail: merchantEmail,
    adminPhone: uniquePhone(suffix, "14992"),
    adminPassword: password,
  });

  await api.setEstablishmentStatus(adminToken, establishment.id, {
    operationalStatus: "Open",
  });

  const merchantTokens = await api.login(merchantEmail, password);
  await api.putDeliveryPricing(merchantTokens.accessToken, establishment.id);
  const menu = await api.createMenuCategory(
    merchantTokens.accessToken,
    establishment.id,
    "Lanches",
  );
  const product = await api.createProduct(
    merchantTokens.accessToken,
    establishment.id,
    menu.id,
  );

  const customerEmail = `cliente-${suffix}@e2e.pedelocal.dev`;
  await api.registerCustomer({
    name: `Cliente E2E ${suffix}`,
    email: customerEmail,
    phone: uniquePhone(suffix, "14993"),
    password,
  });
  const customerTokens = await api.login(customerEmail, password);
  const address = await api.createAddress(
    customerTokens.accessToken,
    city.id,
    -21.851,
    -49.901,
  );

  const shop: IsolatedShop & { driver?: Actor & { driverId: string } } = {
    suffix,
    city,
    establishment,
    product,
    merchant: { email: merchantEmail, password, tokens: merchantTokens },
    customer: { email: customerEmail, password, tokens: customerTokens },
    address,
    adminToken,
  };

  if (options.withDriver) {
    shop.driver = await registerApprovedDriver(api, adminToken, city.id, suffix, "a");
  }

  return shop;
}

export async function registerCustomerActor(
  api: ApiClient,
  suffix = uniqueId(),
): Promise<Actor> {
  const password = e2eConfig.testPassword;
  const email = `cliente-${suffix}@e2e.pedelocal.dev`;
  await api.registerCustomer({
    name: `Cliente E2E ${suffix}`,
    email,
    phone: uniquePhone(suffix, "14995"),
    password,
  });
  const tokens = await api.login(email, password);
  return { email, password, tokens };
}

export async function bootstrapRivalMerchant(
  api: ApiClient,
  shop: IsolatedShop,
): Promise<Actor> {
  const suffix = uniqueId();
  const password = e2eConfig.testPassword;
  const merchantEmail = `loja-${suffix}@e2e.pedelocal.dev`;
  const categories = await api.getCategories();
  const category =
    categories.find((item) => item.name === "Lanchonete") ?? categories[0];
  if (!category) {
    throw new Error("Nenhuma categoria de estabelecimento seedada.");
  }

  const establishment = await api.createEstablishment(shop.adminToken, {
    cityId: shop.city.id,
    name: `Rival E2E ${suffix}`,
    document: `RV${suffix}`.slice(0, 32),
    phone: uniquePhone(suffix, "14996"),
    email: `rival-${suffix}@e2e.pedelocal.dev`,
    categoryId: category.id,
    street: "Rua Rival",
    number: "200",
    district: "Centro",
    complement: null,
    zipCode: "17370000",
    latitude: -21.852,
    longitude: -49.902,
    adminName: `Rival Admin ${suffix}`,
    adminEmail: merchantEmail,
    adminPhone: uniquePhone(suffix, "14997"),
    adminPassword: password,
  });

  await api.setEstablishmentStatus(shop.adminToken, establishment.id, {
    operationalStatus: "Open",
  });

  const tokens = await api.login(merchantEmail, password);
  return { email: merchantEmail, password, tokens };
}

export async function registerApprovedDriver(
  api: ApiClient,
  adminToken: string,
  cityId: string,
  suffix: string,
  lane: string,
): Promise<Actor & { driverId: string }> {
  const password = e2eConfig.testPassword;
  const email = `motorista-${lane}-${suffix}@e2e.pedelocal.dev`;
  const cpfBase = `${Date.now()}${lane.charCodeAt(0)}`.replace(/\D/g, "");
  const registered = await api.registerDriver({
    name: `Motorista ${lane} ${suffix}`,
    cpf: uniqueCpf(`${cpfBase}${lane}`),
    phone: uniquePhone(`${suffix}${lane}`, "14994"),
    email,
    password,
    cityId,
    vehicleType: "Motorcycle",
    pixKeyType: "Email",
    pixKey: email,
  });
  await api.approveDriver(adminToken, registered.driverId);
  const tokens = await api.login(email, password);
  await api.driverOnline(tokens.accessToken);
  return { email, password, tokens, driverId: registered.driverId };
}

export async function cleanupShop(api: ApiClient, shop: IsolatedShop): Promise<void> {
  try {
    await api.setEstablishmentStatus(shop.adminToken, shop.establishment.id, {
      commercialStatus: "Inactive",
    });
  } catch {
    // Isolamento: não falhar o teste por limpeza.
  }
  try {
    await api.setCityActive(shop.adminToken, shop.city.id, false);
  } catch {
    // Isolamento: não falhar o teste por limpeza.
  }
}

export function deliveryOrderBody(shop: IsolatedShop, overrides: Partial<{
  paymentMethod: string;
  quantity: number;
  orderType: "Delivery" | "Pickup";
  addressId: string | null;
  notes: string;
}> = {}) {
  const orderType = overrides.orderType ?? "Delivery";
  const addressId =
    orderType === "Pickup"
      ? null
      : Object.hasOwn(overrides, "addressId")
        ? (overrides.addressId ?? null)
        : shop.address.id;
  return {
    establishmentId: shop.establishment.id,
    addressId,
    paymentMethod: overrides.paymentMethod ?? "Pix",
    changeForAmount: null,
    items: [
      {
        productId: shop.product.id,
        quantity: overrides.quantity ?? 2,
        options: [],
      },
    ],
    idempotencyKey: crypto.randomUUID(),
    orderType,
    notes: overrides.notes ?? "pedido e2e",
  };
}
