export type AuthTokens = {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAtUtc: string;
};

export type ApiErrorBody = {
  code?: string;
  message?: string;
  traceId?: string;
};

export type City = {
  id: string;
  name: string;
  state: string;
  isActive: boolean;
};

export type EstablishmentCategory = {
  id: string;
  name: string;
};

export type Establishment = {
  id: string;
  cityId: string;
  name: string;
  slug: string;
  operationalStatus: string;
  commercialStatus: string;
};

export type MenuCategory = {
  id: string;
  establishmentId: string;
  name: string;
  sortOrder: number;
};

export type Product = {
  id: string;
  establishmentId: string;
  menuCategoryId: string;
  name: string;
  price: number;
  isAvailable: boolean;
};

export type Address = {
  id: string;
  cityId: string;
  type: string;
  street: string | null;
  number: string | null;
  district: string | null;
  latitude: number;
  longitude: number;
};

export type OrderItem = {
  id: string;
  productId: string;
  productName: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
};

export type Order = {
  id: string;
  customerUserId: string;
  establishmentId: string;
  cityId: string;
  addressId: string | null;
  status: string;
  type: string;
  paymentMethod: string;
  subtotal: number;
  deliveryFee: number;
  total: number;
  calculatedDistanceKm: number | null;
  notes: string | null;
  items: OrderItem[];
};

export type OrderPreview = {
  establishmentId: string;
  type: string;
  addressId: string | null;
  subtotal: number;
  deliveryFee: number;
  total: number;
  distanceKm: number | null;
  items: OrderItem[];
};

export type PagedResult<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};

export type Delivery = {
  id: string;
  orderId: string;
  establishmentId: string;
  cityId: string;
  driverId: string | null;
  status: string;
};

export type DeliveryDetails = {
  id: string;
  status: string;
  order: {
    id: string;
    paymentMethod: string;
    subtotal: number;
    deliveryFee: number;
    total: number;
    items: OrderItem[];
  };
  establishment: { id: string; name: string; phone: string };
  deliveryAddress: Address;
};

export type DeliveryCode = {
  code: string;
  expiresAtUtc: string;
};

export type DriverProfile = {
  id: string;
  userId: string;
  cityId: string;
  approvalStatus: string;
  status: string;
};

export type RegisterDriverResponse = {
  userId: string;
  driverId: string;
  approvalStatus: string;
};

export type RegisterCustomerResponse = {
  userId: string;
  name: string;
  email: string;
};

export type CreateOrderBody = {
  establishmentId: string;
  addressId?: string | null;
  paymentMethod: string;
  changeForAmount?: number | null;
  items: Array<{
    productId: string;
    quantity: number;
    options: Array<{ productOptionId: string }>;
  }>;
  idempotencyKey?: string;
  orderType?: "Delivery" | "Pickup";
  notes?: string | null;
};
