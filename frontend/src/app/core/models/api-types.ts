/** TypeScript mirrors of the backend DTOs (camelCase over the wire). */

// ── Auth ────────────────────────────────────────────────────────────────
export interface UserDto {
  id: string;
  email: string;
  displayName: string;
  hasStore: boolean;
  storeSlug: string | null;
  /** "Owner" (seller) and/or "Buyer". */
  roles: string[];
  avatarUrl: string | null;
}

export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  user: UserDto;
}

// ── Store ───────────────────────────────────────────────────────────────
export interface OpeningHourItem {
  day: number; // 0 = Sunday … 6 = Saturday
  closed: boolean;
  open: string; // "09:00"
  close: string; // "21:00"
}

export interface StoreSettingsDto {
  deliveryFee: number;
  minimumOrderAmount: number;
  deliveryEnabled: boolean;
  pickupEnabled: boolean;
  openingHours: OpeningHourItem[];
  defaultLanguage: string;
  currency: string;
}

export interface StoreDto {
  id: string;
  slug: string;
  name: string;
  nameAr: string | null;
  description: string | null;
  descriptionAr: string | null;
  logoUrl: string | null;
  bannerUrl: string | null;
  whatsAppNumber: string;
  instagramHandle: string | null;
  locationText: string | null;
  governorate: string | null;
  wilayat: string | null;
  isAcceptingOrders: boolean;
  settings: StoreSettingsDto;
  plan: string;
  createdAt: string;
}

export interface CreateStoreRequest {
  name: string;
  nameAr: string | null;
  slug: string;
  whatsAppNumber: string;
  instagramHandle: string | null;
  description: string | null;
  descriptionAr: string | null;
  locationText: string | null;
  governorate: string | null;
  wilayat: string | null;
}

export interface UpdateStoreRequest extends CreateStoreRequest {
  isAcceptingOrders: boolean;
}

export interface UpdateStoreSettingsRequest {
  deliveryFee: number;
  minimumOrderAmount: number;
  deliveryEnabled: boolean;
  pickupEnabled: boolean;
  openingHours: OpeningHourItem[];
  defaultLanguage: string;
}

export interface SlugAvailabilityDto {
  slug: string;
  isValid: boolean;
  isAvailable: boolean;
}

// ── Catalog ─────────────────────────────────────────────────────────────
export interface VariantOptionDto {
  id: string | null;
  name: string;
  nameAr: string | null;
  priceAdjustment: number;
  isAvailable: boolean;
  sortOrder: number;
}

export interface VariantDto {
  id: string | null;
  name: string;
  nameAr: string | null;
  isRequired: boolean;
  sortOrder: number;
  options: VariantOptionDto[];
}

export interface ProductImageDto {
  id: string;
  url: string;
  sortOrder: number;
  isPrimary: boolean;
}

export interface ProductDto {
  id: string;
  categoryId: string | null;
  categoryName: string | null;
  name: string;
  nameAr: string | null;
  description: string | null;
  descriptionAr: string | null;
  price: number;
  discountedPrice: number | null;
  stockQuantity: number | null;
  isAvailable: boolean;
  isFeatured: boolean;
  images: ProductImageDto[];
  variants: VariantDto[];
  createdAt: string;
}

export interface SaveProductRequest {
  categoryId: string | null;
  name: string;
  nameAr: string | null;
  description: string | null;
  descriptionAr: string | null;
  price: number;
  discountedPrice: number | null;
  stockQuantity: number | null;
  isAvailable: boolean;
  isFeatured: boolean;
  variants: VariantDto[];
}

export interface CategoryDto {
  id: string;
  name: string;
  nameAr: string | null;
  sortOrder: number;
  isActive: boolean;
  productsCount: number;
}

export interface SaveCategoryRequest {
  name: string;
  nameAr: string | null;
  sortOrder: number;
  isActive: boolean;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

// ── Orders ──────────────────────────────────────────────────────────────
export type OrderStatus =
  | 'New' | 'Confirmed' | 'Preparing' | 'Ready' | 'OutForDelivery' | 'Completed' | 'Cancelled' | 'Rejected';

export type FulfillmentMethod = 'Pickup' | 'Delivery';

export interface OrderItemDto {
  id: string;
  productId: string | null;
  productName: string;
  variantsText: string | null;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
}

export interface OrderDto {
  id: string;
  orderNumber: string;
  customerName: string;
  customerPhone: string;
  status: OrderStatus;
  fulfillmentMethod: FulfillmentMethod;
  deliveryAddress: string | null;
  googleMapsUrl: string | null;
  preferredTime: string | null;
  notes: string | null;
  subtotal: number;
  deliveryFee: number;
  discount: number;
  total: number;
  items: OrderItemDto[];
  estimatedReadyAt: string | null;
  statusHistory: OrderStatusHistoryDto[];
  unreadMessages: number;
  createdAt: string;
  updatedAt: string;
}

export interface OrderListItemDto {
  id: string;
  orderNumber: string;
  customerName: string;
  customerPhone: string;
  status: OrderStatus;
  fulfillmentMethod: FulfillmentMethod;
  total: number;
  itemsCount: number;
  itemsSummary: string;
  unreadMessages: number;
  createdAt: string;
}

export interface OrdersPage {
  items: OrderListItemDto[];
  total: number;
  page: number;
  pageSize: number;
  newCount: number;
}

// ── Public storefront ───────────────────────────────────────────────────
export interface PublicCategoryDto {
  id: string;
  name: string;
  nameAr: string | null;
  sortOrder: number;
}

export interface PublicStoreDto {
  id: string;
  slug: string;
  name: string;
  nameAr: string | null;
  description: string | null;
  descriptionAr: string | null;
  logoUrl: string | null;
  bannerUrl: string | null;
  whatsAppNumber: string;
  instagramHandle: string | null;
  locationText: string | null;
  governorate: string | null;
  wilayat: string | null;
  acceptingOrders: boolean;
  isOpenNow: boolean;
  openingHours: OpeningHourItem[];
  deliveryFee: number;
  minimumOrderAmount: number;
  deliveryEnabled: boolean;
  pickupEnabled: boolean;
  currency: string;
  defaultLanguage: string;
  categories: PublicCategoryDto[];
  rating: number | null;
  reviewsCount: number;
  offers: PublicOfferDto[];
}

export interface PublicVariantOptionDto {
  id: string;
  name: string;
  nameAr: string | null;
  priceAdjustment: number;
}

export interface PublicVariantDto {
  id: string;
  name: string;
  nameAr: string | null;
  isRequired: boolean;
  options: PublicVariantOptionDto[];
}

export interface PublicProductDto {
  id: string;
  categoryId: string | null;
  name: string;
  nameAr: string | null;
  description: string | null;
  descriptionAr: string | null;
  price: number;
  discountedPrice: number | null;
  inStock: boolean;
  isFeatured: boolean;
  images: string[];
  variants: PublicVariantDto[];
}

export interface PublicOrderItemRequest {
  productId: string;
  quantity: number;
  optionIds: string[] | null;
}

export interface CreatePublicOrderRequest {
  customerName: string;
  customerPhone: string;
  fulfillmentMethod: FulfillmentMethod;
  deliveryAddress: string | null;
  googleMapsUrl: string | null;
  preferredTime: string | null;
  notes: string | null;
  items: PublicOrderItemRequest[];
}

export interface PublicOrderCreatedDto {
  orderNumber: string;
  subtotal: number;
  deliveryFee: number;
  discount: number;
  total: number;
  status: OrderStatus;
  storeWhatsAppNumber: string;
  appliedOffer: string | null;
  appliedOfferAr: string | null;
}

export interface PublicOrderStatusDto {
  orderId: string;
  orderNumber: string;
  status: OrderStatus;
  fulfillmentMethod: FulfillmentMethod;
  subtotal: number;
  deliveryFee: number;
  total: number;
  items: OrderItemDto[];
  estimatedReadyAt: string | null;
  statusHistory: OrderStatusHistoryDto[];
  createdAt: string;
}

// ── Chat / realtime / notifications ─────────────────────────────────────
export type ChatSender = 'Customer' | 'Store' | 'System';

export interface ChatMessageDto {
  id: string;
  orderId: string;
  sender: ChatSender;
  /** Free text, or a machine token like "status:Confirmed" for System messages. */
  body: string;
  sentAt: string;
  readAt: string | null;
}

export interface OrderStatusHistoryDto {
  previousStatus: OrderStatus | null;
  newStatus: OrderStatus;
  changedBy: string;
  changedAt: string;
}

export interface OrderStatusChangedEvent {
  orderId: string;
  orderNumber: string;
  status: OrderStatus;
  estimatedReadyAt: string | null;
  entry: OrderStatusHistoryDto;
}

export interface MessagesReadEvent {
  orderId: string;
  reader: ChatSender;
  readAt: string;
}

export interface TypingEvent {
  orderId: string;
  sender: ChatSender;
  isTyping: boolean;
}

export type NotificationType = 'NewOrder' | 'OrderStatusChanged' | 'NewMessage';

export interface NotificationDto {
  id: string;
  type: NotificationType;
  title: string;
  body: string;
  orderId: string | null;
  isRead: boolean;
  createdAt: string;
}

export interface NotificationsPage {
  items: NotificationDto[];
  unreadCount: number;
}

export interface SendMessageRequest {
  body: string;
}

// ── Dashboard / subscription ────────────────────────────────────────────
export interface TopProductDto {
  name: string;
  quantity: number;
  revenue: number;
}

export interface DashboardSummaryDto {
  ordersToday: number;
  revenueToday: number;
  ordersThisMonth: number;
  revenueThisMonth: number;
  averageOrderValue: number;
  newOrdersCount: number;
  productsCount: number;
  topProducts: TopProductDto[];
  recentOrders: OrderListItemDto[];
}

export interface SubscriptionDto {
  plan: string;
  status: string;
  maxProducts: number | null;
  maxOrdersPerMonth: number | null;
  whatsAppNotifications: boolean;
  analytics: boolean;
  productsUsed: number;
  ordersThisMonth: number;
  startsAt: string;
}

/** RFC 7807 problem details shape produced by the API. */
export interface ApiProblem {
  title?: string;
  status?: number;
  code?: string;
  errors?: Record<string, string[]>;
}

// ── Marketplace discovery ───────────────────────────────────────────────
export type OfferType = 'Percentage' | 'FixedAmount' | 'FreeDelivery';

export interface StoreCardDto {
  id: string;
  slug: string;
  name: string;
  nameAr: string | null;
  description: string | null;
  descriptionAr: string | null;
  logoUrl: string | null;
  bannerUrl: string | null;
  governorate: string | null;
  wilayat: string | null;
  acceptingOrders: boolean;
  isOpenNow: boolean;
  deliveryFee: number;
  minimumOrderAmount: number;
  deliveryEnabled: boolean;
  pickupEnabled: boolean;
  rating: number | null;
  reviewsCount: number;
  hasActiveOffers: boolean;
  isNew: boolean;
}

export interface ProductCardDto {
  id: string;
  name: string;
  nameAr: string | null;
  price: number;
  discountedPrice: number | null;
  imageUrl: string | null;
  inStock: boolean;
  isFeatured: boolean;
  storeSlug: string;
  storeName: string;
  storeNameAr: string | null;
}

export interface StoresPage {
  items: StoreCardDto[];
  total: number;
  page: number;
  pageSize: number;
}

export interface PublicOfferDto {
  id: string;
  title: string;
  titleAr: string | null;
  description: string | null;
  descriptionAr: string | null;
  type: OfferType;
  discountValue: number;
  minimumOrderAmount: number;
  endsAt: string | null;
  storeSlug: string;
  storeName: string;
  storeNameAr: string | null;
  storeLogoUrl: string | null;
}

export interface CategorySuggestionDto {
  name: string;
  nameAr: string | null;
  storeCount: number;
}

export interface SearchResultsDto {
  stores: StoreCardDto[];
  products: ProductCardDto[];
  categories: CategorySuggestionDto[];
  storesTotal: number;
  productsTotal: number;
}

export interface SearchSuggestionsDto {
  suggestions: string[];
  recent: string[];
  popular: string[];
}

export interface HomeSectionDto {
  key: string;
  stores: StoreCardDto[] | null;
  products: ProductCardDto[] | null;
  offers: PublicOfferDto[] | null;
}

export interface MarketplaceHomeDto {
  sections: HomeSectionDto[];
}

export interface TrackActivityRequest {
  eventType: 'AddedToCart' | 'CategoryViewed';
  storeId: string | null;
  productId: string | null;
  categoryId: string | null;
}

// ── Buyer account ───────────────────────────────────────────────────────
export interface BuyerProfileDto {
  email: string;
  displayName: string;
  phone: string | null;
  avatarUrl: string | null;
  preferredLanguage: string | null;
  notifyOrderUpdates: boolean;
  notifyMessages: boolean;
  notifyOffers: boolean;
}

export interface UpdateBuyerProfileRequest {
  displayName: string;
  phone: string | null;
  preferredLanguage: string | null;
  notifyOrderUpdates: boolean;
  notifyMessages: boolean;
  notifyOffers: boolean;
}

export interface BuyerAddressDto {
  id: string;
  label: string;
  recipientName: string;
  phone: string;
  governorate: string | null;
  wilayat: string | null;
  city: string | null;
  area: string | null;
  street: string | null;
  building: string | null;
  apartment: string | null;
  notes: string | null;
  latitude: number | null;
  longitude: number | null;
  isDefault: boolean;
  createdAt: string;
}

export interface SaveAddressRequest {
  label: string;
  recipientName: string;
  phone: string;
  governorate: string | null;
  wilayat: string | null;
  city: string | null;
  area: string | null;
  street: string | null;
  building: string | null;
  apartment: string | null;
  notes: string | null;
  latitude: number | null;
  longitude: number | null;
  isDefault: boolean;
}

export interface FavoriteIdsDto {
  storeIds: string[];
  productIds: string[];
}

export interface FavoriteStoreDto {
  storeId: string;
  slug: string;
  name: string;
  nameAr: string | null;
  logoUrl: string | null;
  bannerUrl: string | null;
  governorate: string | null;
  wilayat: string | null;
  acceptingOrders: boolean;
  rating: number | null;
  reviewsCount: number;
  savedAt: string;
}

export interface FavoriteProductDto {
  productId: string;
  name: string;
  nameAr: string | null;
  price: number;
  discountedPrice: number | null;
  imageUrl: string | null;
  inStock: boolean;
  storeSlug: string;
  storeName: string;
  storeNameAr: string | null;
  savedAt: string;
}

export interface FavoritesDto {
  stores: FavoriteStoreDto[];
  products: FavoriteProductDto[];
}

export interface ReviewDto {
  id: string;
  orderId: string;
  rating: number;
  comment: string | null;
  reviewerName: string;
  createdAt: string;
}

export interface CreateReviewRequest {
  rating: number;
  comment: string | null;
}

export interface StoreReviewsPage {
  items: ReviewDto[];
  total: number;
  page: number;
  pageSize: number;
  averageRating: number | null;
  reviewsCount: number;
}

export interface BuyerOrderListItemDto {
  id: string;
  orderNumber: string;
  status: OrderStatus;
  fulfillmentMethod: FulfillmentMethod;
  total: number;
  itemsCount: number;
  itemsSummary: string;
  unreadMessages: number;
  storeSlug: string;
  storeName: string;
  storeNameAr: string | null;
  storeLogoUrl: string | null;
  canReview: boolean;
  createdAt: string;
}

export interface BuyerOrdersPage {
  items: BuyerOrderListItemDto[];
  total: number;
  page: number;
  pageSize: number;
  activeCount: number;
}

export interface BuyerOrderDto {
  id: string;
  orderNumber: string;
  status: OrderStatus;
  fulfillmentMethod: FulfillmentMethod;
  deliveryAddress: string | null;
  preferredTime: string | null;
  notes: string | null;
  subtotal: number;
  deliveryFee: number;
  discount: number;
  total: number;
  items: OrderItemDto[];
  estimatedReadyAt: string | null;
  statusHistory: OrderStatusHistoryDto[];
  unreadMessages: number;
  storeSlug: string;
  storeName: string;
  storeNameAr: string | null;
  storeLogoUrl: string | null;
  storeWhatsAppNumber: string;
  canReview: boolean;
  review: ReviewDto | null;
  createdAt: string;
}

export interface BuyerConversationDto {
  orderId: string;
  orderNumber: string;
  storeSlug: string;
  storeName: string;
  storeNameAr: string | null;
  storeLogoUrl: string | null;
  lastMessage: string;
  lastSender: ChatSender;
  lastMessageAt: string;
  unreadCount: number;
}

export interface AccountOverviewDto {
  profile: BuyerProfileDto;
  activeOrdersCount: number;
  unreadMessages: number;
  unreadNotifications: number;
  favoriteStoresCount: number;
  favoriteProductsCount: number;
  recentOrders: BuyerOrderListItemDto[];
  defaultAddress: BuyerAddressDto | null;
}

// ── Offers (seller) ─────────────────────────────────────────────────────
export interface OfferDto {
  id: string;
  title: string;
  titleAr: string | null;
  description: string | null;
  descriptionAr: string | null;
  type: OfferType;
  discountValue: number;
  minimumOrderAmount: number;
  startsAt: string;
  endsAt: string | null;
  isActive: boolean;
  isRunning: boolean;
  createdAt: string;
}

export interface SaveOfferRequest {
  title: string;
  titleAr: string | null;
  description: string | null;
  descriptionAr: string | null;
  type: OfferType;
  discountValue: number;
  minimumOrderAmount: number;
  startsAt: string;
  endsAt: string | null;
  isActive: boolean;
}
