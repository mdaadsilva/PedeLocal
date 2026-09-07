export type PagedResult<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};

export type Profile = {
  id: string;
  name: string;
  email: string;
  phoneNumber: string;
  roles: string[];
  createdAtUtc: string;
};

export type City = {
  id: string;
  name: string;
  state: string;
  isActive: boolean;
};

export type Establishment = {
  id: string;
  cityId: string;
  name: string;
  slug: string;
  document: string;
  phone: string;
  email: string;
  categoryId: string;
  addressId: string;
  operationalStatus: string;
  commercialStatus: string;
  createdAtUtc: string;
};

export type MenuCategory = {
  id: string;
  establishmentId: string;
  name: string;
  sortOrder: number;
};

export type ProductOption = {
  id: string;
  name: string;
  priceDelta: number;
  isAvailable: boolean;
};

export type ProductOptionGroup = {
  id: string;
  name: string;
  required: boolean;
  min: number;
  max: number;
  options: ProductOption[];
};

export type Product = {
  id: string;
  establishmentId: string;
  menuCategoryId: string;
  name: string;
  description: string | null;
  imageUrl: string | null;
  price: number;
  promotionalPrice: number | null;
  stockMode: "Uncontrolled" | "Controlled" | string;
  stockQuantity: number | null;
  isAvailable: boolean;
  optionGroups: ProductOptionGroup[];
};

export type OrderItem = {
  id: string;
  productId: string;
  productName: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
  options: { id: string; optionName: string; priceDelta: number }[];
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
  paymentStatus: string;
  changeForAmount: number | null;
  subtotal: number;
  deliveryFee: number;
  total: number;
  calculatedDistanceKm: number | null;
  notes: string | null;
  createdAtUtc: string;
  items: OrderItem[];
  statusHistory: { status: string; changedAtUtc: string; notes: string | null }[];
};

export type InventoryMovement = {
  id: string;
  productId: string;
  type: string;
  quantity: number;
  previousQuantity: number;
  newQuantity: number;
  orderId: string | null;
  userId: string | null;
  reason: string | null;
  createdAtUtc: string;
};

export type DeliveryPricing = {
  establishmentId: string;
  urbanMode: string;
  urbanFixedFee: number;
  urbanPricePerKm: number;
  urbanMinFee: number;
  ruralMode: string;
  ruralFixedFee: number;
  ruralPricePerKm: number;
  ruralMinFee: number;
  maxDistanceKm: number;
};

export type DriverReceivable = {
  id: string;
  deliveryId: string;
  driverId: string;
  establishmentId: string;
  amount: number;
  status: string;
  paymentBatchId: string | null;
  createdAtUtc: string;
};

export type DriverPaymentBatch = {
  id: string;
  driverId: string;
  establishmentId: string;
  totalAmount: number;
  status: string;
  createdAtUtc: string;
  paymentReportedAtUtc: string | null;
  receivedAtUtc: string | null;
  receivableIds: string[];
};
