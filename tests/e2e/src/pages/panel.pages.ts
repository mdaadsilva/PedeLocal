import type { Page } from "@playwright/test";

export class LoginPage {
  constructor(
    private readonly page: Page,
    private readonly baseUrl: string,
  ) {}

  async open() {
    await this.page.goto(`${this.baseUrl}/login`);
    await this.page.getByRole("heading").waitFor();
  }

  async submit(email: string, password: string) {
    await this.page.getByLabel("E-mail").fill(email);
    await this.page.getByLabel("Senha").fill(password);
    await this.page.getByRole("button", { name: "Entrar" }).click();
  }
}

export class MerchantOrdersPage {
  constructor(private readonly page: Page) {}

  async openList(baseUrl: string) {
    await this.page.goto(`${baseUrl}/pedidos`);
    await this.page.getByRole("heading", { name: "Pedidos" }).waitFor();
  }

  async openOrder(orderId: string) {
    const shortId = orderId.slice(0, 8);
    await this.page.getByRole("link", { name: new RegExp(`#${shortId}`) }).click();
    await this.page.waitForURL(new RegExp(`/pedidos/${orderId}$`));
    await this.page.getByText(`Pedido #${shortId}`).waitFor();
  }

  async runAction(label: string) {
    await this.page.getByRole("button", { name: label }).click();
  }
}

export class AdminPanelPage {
  constructor(private readonly page: Page) {}

  async expectDashboard() {
    await this.page.getByRole("heading", { name: "Dashboard" }).waitFor();
  }

  async openNav(label: string) {
    await this.page.getByRole("navigation").getByRole("link", { name: label, exact: true }).click();
  }
}
