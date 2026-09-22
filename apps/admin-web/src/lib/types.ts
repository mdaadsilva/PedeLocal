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

export type EstablishmentCategory = {
  id: string;
  name: string;
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

export type CreateEstablishmentBody = {
  cityId: string;
  name: string;
  document: string;
  phone: string;
  email: string;
  categoryId: string;
  street: string;
  number: string;
  district: string;
  complement?: string | null;
  zipCode?: string | null;
  latitude: number;
  longitude: number;
  adminName: string;
  adminEmail: string;
  adminPhone: string;
  adminPassword: string;
};

export type UpdateEstablishmentBody = {
  name: string;
  phone: string;
  email: string;
  categoryId: string;
  street: string;
  number: string;
  district: string;
  complement?: string | null;
  zipCode?: string | null;
  latitude: number;
  longitude: number;
};

export type Subscription = {
  id: string;
  establishmentId: string;
  monthlyAmount: number;
  billingDay: number;
  dueDate: string;
  graceDays: number;
  paymentStatus: string;
};

export type SubscriptionPayment = {
  id: string;
  establishmentId: string;
  subscriptionId: string;
  amount: number;
  paymentMethod: string;
  paidAtUtc: string;
  registeredByUserId: string;
  notes: string | null;
  receiptUrl: string | null;
};

export type EstablishmentFeature = {
  id: string;
  establishmentId: string;
  featureKey: string;
  enabled: boolean;
  settingsJson: string | null;
};

export type Customer = {
  id: string;
  name: string;
  email: string;
  phoneNumber: string;
  isActive: boolean;
  createdAtUtc: string;
};

export type Driver = {
  id: string;
  userId: string;
  name: string;
  cityId: string;
  cpf: string;
  vehicleType: string;
  pixKeyType: string;
  pixKey: string;
  /** `PendingDocuments | UnderReview | Approved | Rejected` */
  onboardingStatus: string;
  /** `Offline | Available | Busy | Blocked` */
  status: string;
  /**
   * `Eligible | PendingDocuments | UnderReview | Rejected | Blocked | DocumentExpired`
   * — computado pelo backend (`DriverEligibilityService`), ver
   * `docs/DRIVER_ONBOARDING.md`. É o que explica por que o entregador não
   * consegue ficar online, mesmo já aprovado (ex.: CNH vencida).
   */
  eligibilityStatus: string;
  reviewedAtUtc: string | null;
  rejectionReason: string | null;
};

export type DriverLicense = {
  registrationNumber: string;
  category: string;
  expiresAt: string;
  hasEar: boolean | null;
  documentImageUrl: string | null;
  daysUntilExpiration: number;
};

export type DriverVehicle = {
  type: string;
  plate: string | null;
  model: string;
  color: string;
  documentImageUrl: string | null;
};

export type DriverDetail = {
  driver: Driver;
  license: DriverLicense | null;
  vehicle: DriverVehicle | null;
};

export type DeliveryOccurrence = {
  id: string;
  deliveryId: string;
  driverId: string;
  /** `CustomerNotFound | CustomerNotResponding | WrongAddress | PaymentProblem | VehicleProblem | Other` */
  type: string;
  note: string | null;
  createdAtUtc: string;
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

export type Delivery = {
  id: string;
  orderId: string;
  establishmentId: string;
  cityId: string;
  driverId: string | null;
  status: string;
  assignedAtUtc: string | null;
  pickedUpAtUtc: string | null;
  outForDeliveryAtUtc: string | null;
  completedAtUtc: string | null;
};

export type AuditLog = {
  id: string;
  actorUserId: string | null;
  action: string;
  entityType: string;
  entityId: string | null;
  ip: string | null;
  userAgent: string | null;
  metadataJson: string | null;
  createdAtUtc: string;
};
