import { expect, test } from "../src/fixtures/test";
import { deliveryOrderBody } from "../src/fixtures/world";

test.describe("Fluxo 1 — customer delivery", () => {
  test("autentica, escolhe cidade/loja/produto, preview, cria e acompanha o pedido", async ({
    api,
    shop,
  }) => {
    const customerToken = shop.customer.tokens.accessToken;

    const cities = await api.getCities();
    expect(cities.some((city) => city.id === shop.city.id)).toBeTruthy();

    const establishmentsResponse = await api.rawGet(
      `/api/establishments?cityId=${shop.city.id}`,
      customerToken,
    );
    await api.expectStatus(establishmentsResponse, 200);
    const catalog = (await establishmentsResponse.json()) as {
      items: Array<{ id: string; name: string }>;
    };
    expect(catalog.items.some((item) => item.id === shop.establishment.id)).toBeTruthy();

    const productsResponse = await api.rawGet(
      `/api/establishments/${shop.establishment.id}/products`,
      customerToken,
    );
    await api.expectStatus(productsResponse, 200);
    const products = (await productsResponse.json()) as Array<{ id: string }>;
    expect(products.some((item) => item.id === shop.product.id)).toBeTruthy();

    const body = deliveryOrderBody(shop, { paymentMethod: "Pix", quantity: 2 });
    const preview = await api.previewOrder(customerToken, body);

    expect(preview.type).toBe("Delivery");
    expect(preview.addressId).toBe(shop.address.id);
    expect(preview.subtotal).toBe(40);
    expect(preview.deliveryFee).toBe(5);
    expect(preview.total).toBe(45);

    const order = await api.createOrder(customerToken, body);
    expect(order.status).toBe("Pending");
    expect(order.type).toBe("Delivery");
    expect(order.paymentMethod).toBe("Pix");
    expect(order.subtotal).toBe(preview.subtotal);
    expect(order.deliveryFee).toBe(preview.deliveryFee);
    expect(order.total).toBe(preview.total);
    expect(order.items).toHaveLength(1);
    expect(order.items[0]?.productName).toContain("X-Burger");

    const listed = await api.rawGet("/api/orders?page=1&pageSize=20", customerToken);
    await api.expectStatus(listed, 200);

    expect(order.establishmentId).toBe(shop.establishment.id);

    const tracked = await api.getOrder(customerToken, order.id);
    await api.expectStatus(tracked, 200);
  });
});
