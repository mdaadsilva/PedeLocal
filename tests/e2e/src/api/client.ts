import { expect, type APIRequestContext, type APIResponse } from "@playwright/test";
import { e2eConfig } from "../config";
import type {
  Address,
  ApiErrorBody,
  AuthTokens,
  City,
  CreateOrderBody,
  Delivery,
  DeliveryCode,
  DeliveryDetails,
  DriverProfile,
  Establishment,
  EstablishmentCategory,
  MenuCategory,
  Order,
  OrderPreview,
  PagedResult,
  Product,
  RegisterCustomerResponse,
  RegisterDriverResponse,
} from "./types";

export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly body: ApiErrorBody,
    readonly url: string,
  ) {
    super(
      `${status} ${body.code ?? "UNKNOWN"} ${url}: ${body.message ?? "(sem mensagem)"}`,
    );
  }
}

export class ApiClient {
  constructor(
    private readonly request: APIRequestContext,
    readonly baseUrl: string = e2eConfig.apiUrl,
  ) {}

  bearer(accessToken: string): { Authorization: string } {
    return { Authorization: `Bearer ${accessToken}` };
  }

  async login(email: string, password: string): Promise<AuthTokens> {
    const response = await this.postAuth("/api/auth/login", { email, password });
    await this.expectStatus(response, 200);
    return (await response.json()) as AuthTokens;
  }

  async registerCustomer(input: {
    name: string;
    email: string;
    phone: string;
    password: string;
  }): Promise<RegisterCustomerResponse> {
    const response = await this.postAuth("/api/auth/register/customer", input);
    await this.expectStatus(response, 201);
    return (await response.json()) as RegisterCustomerResponse;
  }

  async registerDriver(input: {
    name: string;
    cpf: string;
    phone: string;
    email: string;
    password: string;
    cityId: string;
    vehicleType: string;
    pixKeyType: string;
    pixKey: string;
  }): Promise<RegisterDriverResponse> {
    const response = await this.postAuth("/api/auth/register/driver", input);
    await this.expectStatus(response, 201);
    return (await response.json()) as RegisterDriverResponse;
  }

  async getCities(): Promise<City[]> {
    const response = await this.request.get(`${this.baseUrl}/api/cities`);
    await this.expectStatus(response, 200);
    return (await response.json()) as City[];
  }

  async getCategories(): Promise<EstablishmentCategory[]> {
    const response = await this.request.get(
      `${this.baseUrl}/api/establishment-categories`,
    );
    await this.expectStatus(response, 200);
    return (await response.json()) as EstablishmentCategory[];
  }

  async createCity(token: string, name: string, state = "SP"): Promise<City> {
    const response = await this.request.post(`${this.baseUrl}/api/admin/cities`, {
      headers: this.bearer(token),
      data: { name, state },
    });
    await this.expectStatus(response, 201);
    return (await response.json()) as City;
  }

  async setCityActive(token: string, cityId: string, isActive: boolean): Promise<void> {
    const response = await this.request.patch(
      `${this.baseUrl}/api/admin/cities/${cityId}/status`,
      { headers: this.bearer(token), data: { isActive } },
    );
    await this.expectStatus(response, 200);
  }

  async createEstablishment(
    token: string,
    body: Record<string, unknown>,
  ): Promise<Establishment> {
    const response = await this.request.post(
      `${this.baseUrl}/api/admin/establishments`,
      { headers: this.bearer(token), data: body },
    );
    await this.expectStatus(response, 201);
    return (await response.json()) as Establishment;
  }

  async setEstablishmentStatus(
    token: string,
    establishmentId: string,
    body: { operationalStatus?: string; commercialStatus?: string },
  ): Promise<Establishment> {
    const response = await this.request.patch(
      `${this.baseUrl}/api/admin/establishments/${establishmentId}/status`,
      { headers: this.bearer(token), data: body },
    );
    await this.expectStatus(response, 200);
    return (await response.json()) as Establishment;
  }

  async approveDriver(token: string, driverId: string): Promise<DriverProfile> {
    const response = await this.request.post(
      `${this.baseUrl}/api/admin/drivers/${driverId}/approve`,
      { headers: this.bearer(token) },
    );
    await this.expectStatus(response, 200);
    return (await response.json()) as DriverProfile;
  }

  async putDeliveryPricing(
    token: string,
    establishmentId: string,
  ): Promise<void> {
    const response = await this.request.put(
      `${this.baseUrl}/api/merchant/establishments/${establishmentId}/delivery-pricing`,
      {
        headers: this.bearer(token),
        data: {
          urbanMode: "Fixed",
          urbanFixedFee: 5,
          urbanPricePerKm: 0,
          urbanMinFee: 0,
          ruralMode: "Fixed",
          ruralFixedFee: 15,
          ruralPricePerKm: 0,
          ruralMinFee: 0,
          maxDistanceKm: 50,
        },
      },
    );
    await this.expectStatus(response, 200);
  }

  async createMenuCategory(
    token: string,
    establishmentId: string,
    name: string,
  ): Promise<MenuCategory> {
    const response = await this.request.post(
      `${this.baseUrl}/api/merchant/establishments/${establishmentId}/menu-categories`,
      { headers: this.bearer(token), data: { name, sortOrder: 1 } },
    );
    await this.expectStatus(response, 201);
    return (await response.json()) as MenuCategory;
  }

  async createProduct(
    token: string,
    establishmentId: string,
    menuCategoryId: string,
    name = "X-Burger E2E",
    price = 20,
  ): Promise<Product> {
    const response = await this.request.post(
      `${this.baseUrl}/api/merchant/establishments/${establishmentId}/products`,
      {
        headers: this.bearer(token),
        data: {
          menuCategoryId,
          name,
          description: "Produto gerado pelo E2E",
          price,
          stockMode: "Uncontrolled",
          optionGroups: [],
        },
      },
    );
    await this.expectStatus(response, 201);
    return (await response.json()) as Product;
  }

  async createAddress(
    token: string,
    cityId: string,
    latitude: number,
    longitude: number,
  ): Promise<Address> {
    const response = await this.request.post(`${this.baseUrl}/api/me/addresses`, {
      headers: this.bearer(token),
      data: {
        cityId,
        type: "Urban",
        label: "Casa E2E",
        street: "Rua do Cliente",
        number: "50",
        district: "Centro",
        zipCode: "17370000",
        latitude,
        longitude,
      },
    });
    await this.expectStatus(response, 201);
    return (await response.json()) as Address;
  }

  async previewOrder(token: string, body: CreateOrderBody): Promise<OrderPreview> {
    const response = await this.request.post(`${this.baseUrl}/api/orders/preview`, {
      headers: this.bearer(token),
      data: body,
    });
    await this.expectStatus(response, 200);
    return (await response.json()) as OrderPreview;
  }

  async createOrder(token: string, body: CreateOrderBody): Promise<Order> {
    const response = await this.postOrder(token, body);
    await this.expectStatus(response, 201);
    return (await response.json()) as Order;
  }

  async postOrder(token: string, body: CreateOrderBody): Promise<APIResponse> {
    return this.request.post(`${this.baseUrl}/api/orders`, {
      headers: this.bearer(token),
      data: body,
    });
  }

  async getOrder(token: string, orderId: string): Promise<APIResponse> {
    return this.request.get(`${this.baseUrl}/api/orders/${orderId}`, {
      headers: this.bearer(token),
    });
  }

  async getDeliveryCode(token: string, orderId: string): Promise<APIResponse> {
    return this.request.get(`${this.baseUrl}/api/orders/${orderId}/delivery-code`, {
      headers: this.bearer(token),
    });
  }

  async getMerchantOrder(
    token: string,
    establishmentId: string,
    orderId: string,
  ): Promise<APIResponse> {
    return this.request.get(
      `${this.baseUrl}/api/merchant/establishments/${establishmentId}/orders/${orderId}`,
      { headers: this.bearer(token) },
    );
  }

  async merchantAction(
    token: string,
    establishmentId: string,
    orderId: string,
    action: "accept" | "preparing" | "ready" | "complete-pickup" | "cancel",
    body?: Record<string, unknown>,
  ): Promise<APIResponse> {
    return this.request.post(
      `${this.baseUrl}/api/merchant/establishments/${establishmentId}/orders/${orderId}/${action}`,
      body
        ? { headers: this.bearer(token), data: body }
        : { headers: this.bearer(token) },
    );
  }

  async listMerchantOrders(
    token: string,
    establishmentId: string,
  ): Promise<PagedResult<Order>> {
    const response = await this.request.get(
      `${this.baseUrl}/api/merchant/establishments/${establishmentId}/orders?page=1&pageSize=50`,
      { headers: this.bearer(token) },
    );
    await this.expectStatus(response, 200);
    return (await response.json()) as PagedResult<Order>;
  }

  async driverOnline(token: string): Promise<DriverProfile> {
    const response = await this.request.post(
      `${this.baseUrl}/api/driver/me/status/online`,
      { headers: this.bearer(token) },
    );
    await this.expectStatus(response, 200);
    return (await response.json()) as DriverProfile;
  }

  async listAvailableDeliveries(token: string): Promise<Delivery[]> {
    const response = await this.request.get(
      `${this.baseUrl}/api/driver/deliveries/available`,
      { headers: this.bearer(token) },
    );
    await this.expectStatus(response, 200);
    return (await response.json()) as Delivery[];
  }

  async acceptDelivery(token: string, deliveryId: string): Promise<APIResponse> {
    return this.request.post(
      `${this.baseUrl}/api/driver/deliveries/${deliveryId}/accept`,
      { headers: this.bearer(token) },
    );
  }

  async getDeliveryDetails(token: string, deliveryId: string): Promise<APIResponse> {
    return this.request.get(
      `${this.baseUrl}/api/driver/deliveries/${deliveryId}/details`,
      { headers: this.bearer(token) },
    );
  }

  async getCurrentDelivery(token: string): Promise<APIResponse> {
    return this.request.get(`${this.baseUrl}/api/driver/deliveries/current`, {
      headers: this.bearer(token),
    });
  }

  async pickupDelivery(token: string, deliveryId: string): Promise<APIResponse> {
    return this.request.post(
      `${this.baseUrl}/api/driver/deliveries/${deliveryId}/pickup`,
      { headers: this.bearer(token) },
    );
  }

  async outForDelivery(token: string, deliveryId: string): Promise<APIResponse> {
    return this.request.post(
      `${this.baseUrl}/api/driver/deliveries/${deliveryId}/out-for-delivery`,
      { headers: this.bearer(token) },
    );
  }

  async confirmDelivery(
    token: string,
    deliveryId: string,
    code: string,
  ): Promise<APIResponse> {
    return this.postMaybeLimited(`/api/driver/deliveries/${deliveryId}/confirm`, {
      headers: this.bearer(token),
      data: { code },
    });
  }

  async rawGet(path: string, token?: string): Promise<APIResponse> {
    return this.request.get(`${this.baseUrl}${path}`, {
      headers: token ? this.bearer(token) : undefined,
    });
  }

  /**
   * Auth endpoints share the API rate-limit bucket (default 5/60s/IP).
   * Retrying 429 is client-side QA behavior; it does not raise the server limit.
   * Confirm de entrega usa a mesma policy `auth`.
   */
  private async postAuth(path: string, data: unknown): Promise<APIResponse> {
    return this.postMaybeLimited(path, { data });
  }

  private async postMaybeLimited(
    path: string,
    options: { data?: unknown; headers?: Record<string, string> },
  ): Promise<APIResponse> {
    const url = `${this.baseUrl}${path}`;
    let last: APIResponse | undefined;
    for (let attempt = 0; attempt < 4; attempt++) {
      last = await this.request.post(url, options);
      if (last.status() !== 429) return last;
      const retryAfter = Number(last.headers()["retry-after"]);
      const waitMs =
        Number.isFinite(retryAfter) && retryAfter > 0
          ? Math.ceil(retryAfter * 1000)
          : 20_000 * (attempt + 1);
      await new Promise((resolve) =>
        setTimeout(resolve, Math.min(waitMs, 65_000)),
      );
    }
    return last!;
  }

  async expectStatus(response: APIResponse, expected: number | number[]): Promise<void> {
    const allowed = Array.isArray(expected) ? expected : [expected];
    if (allowed.includes(response.status())) return;
    let details = "";
    try {
      details = await response.text();
    } catch {
      details = "(sem corpo)";
    }
    expect(
      response.status(),
      `${response.url()} → ${details}`,
    ).toBe(allowed.length === 1 ? allowed[0] : allowed[0]);
    throw new Error(
      `Expected HTTP ${allowed.join("|")} from ${response.url()}, got ${response.status()}: ${details}`,
    );
  }

  async readJson<T>(response: APIResponse): Promise<T> {
    return (await response.json()) as T;
  }

  async readError(response: APIResponse): Promise<ApiErrorBody> {
    try {
      return (await response.json()) as ApiErrorBody;
    } catch {
      return {};
    }
  }
}

export async function waitForOrderStatus(
  api: ApiClient,
  token: string,
  orderId: string,
  status: string,
): Promise<Order> {
  let last: Order | undefined;
  await expect
    .poll(
      async () => {
        const response = await api.getOrder(token, orderId);
        if (!response.ok()) return null;
        last = await api.readJson<Order>(response);
        return last.status;
      },
      { timeout: 15_000 },
    )
    .toBe(status);
  return last!;
}

export async function waitForAvailableDelivery(
  api: ApiClient,
  token: string,
  orderId: string,
): Promise<Delivery> {
  let found: Delivery | undefined;
  await expect
    .poll(
      async () => {
        const list = await api.listAvailableDeliveries(token);
        found = list.find((item) => item.orderId === orderId);
        return found?.id ?? null;
      },
      { timeout: 15_000 },
    )
    .toBeTruthy();
  return found!;
}
