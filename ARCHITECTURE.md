# WhatsOrder Oman — Solution Architecture

SaaS platform for small Omani businesses: a shareable mini-storefront (`whatsorder.om/{slug}`),
organized order intake, and WhatsApp order notifications via the Meta WhatsApp Cloud API.

---

## 1. Solution Architecture

```
┌──────────────────────────────┐          ┌─────────────────────────────────┐
│   Angular 21 SPA (frontend/) │          │  Meta WhatsApp Cloud API        │
│                              │          │  graph.facebook.com             │
│  • Public storefront /:slug  │          └───────────▲───────────┬─────────┘
│  • Owner dashboard /dashboard│                      │ send      │ webhooks
│  • Auth pages                │                      │ (HTTPS)   │ (signed)
└──────────────┬───────────────┘                      │           │
               │ REST + JWT (HTTPS)                   │           ▼
┌──────────────▼───────────────────────────────────────────────────────────┐
│                    ASP.NET Core 8 Web API (backend/)                      │
│                                                                           │
│  WhatsOrder.Api            controllers, middleware, auth, rate limiting   │
│  WhatsOrder.Application    domain services, DTOs, validators, interfaces  │
│  WhatsOrder.Domain         entities, enums, business rules                │
│  WhatsOrder.Infrastructure EF Core (PostgreSQL), Identity, JWT,           │
│                            WhatsApp client, file storage, dispatcher      │
└──────────────┬────────────────────────────────────────────────────────────┘
               │ EF Core (Npgsql)
┌──────────────▼───────────────┐
│         PostgreSQL 16        │
└──────────────────────────────┘
```

**Layering rules**

| Project | References | Contains |
|---|---|---|
| `WhatsOrder.Domain` | nothing | Entities, enums, invariants. No EF/ASP.NET deps. |
| `WhatsOrder.Application` | Domain | Service logic, DTOs, FluentValidation validators, interfaces (`IAppDbContext`, `IWhatsAppApiClient`, `IFileStorage`, `IAuthService`, …), plan limits, phone/slug/pricing helpers. |
| `WhatsOrder.Infrastructure` | Application | `AppDbContext` (+ Identity), EF configurations & migrations, JWT token service, refresh-token store, WhatsApp Cloud API client + background dispatcher, webhook signature validator, local file storage, email sender. |
| `WhatsOrder.Api` | Application, Infrastructure | Controllers (thin), global exception middleware, validation filter, CORS, rate limiter, Swagger, static file hosting for uploads, DB migration + seeding at startup. |

Pragmatic clean architecture: Application depends on an `IAppDbContext` abstraction
(EF Core *is* the persistence abstraction — no extra repository layer for the MVP).

**Multi-tenancy** — single database, `StoreId` on every tenant-owned row. The tenant is
resolved **server-side only**: JWT `sub` → user → `Store.OwnerId`. No owner endpoint ever
accepts a `storeId` from the client; every query in every service filters by the resolved
store id. Cross-tenant access returns 404. Covered by dedicated integration tests.

**Key decisions**

- **Money**: `numeric(10,3)` — OMR has 3 decimal places (baisa). Formatted `8.500 OMR`.
- **Phones**: normalized to E.164 (`+968XXXXXXXX`); 8-digit Omani numbers get `+968`
  prepended automatically; other countries accepted (`+…`) so expansion isn't blocked.
- **Order numbers**: per-store counter (`Store.OrderSequence`, starts at 1000) →
  `WO-1001`, protected by a unique index `(StoreId, OrderNumber)` + retry.
- **Prices are always recomputed server-side** from the database at order time —
  client-sent prices are never trusted.
- **Soft delete** (`IsDeleted` + global query filters) on Store, Product, Category.
- **Auth**: ASP.NET Core Identity + JWT access tokens (15 min) + rotating refresh
  tokens (14 days, hashed at rest, reuse detection revokes the whole family).
- **Subscriptions**: `Subscription` entity + `PlanLimits` service (Free: 20 products,
  50 orders/month; Pro: unlimited + WhatsApp + analytics). Billing-provider-ready
  (external customer/subscription id columns); no payment processing in MVP.
- **WhatsApp sends are asynchronous**: order submission enqueues a notification on an
  in-process channel; a hosted background dispatcher sends and records delivery state,
  so the customer's checkout never waits on Meta.

---

## 2. Angular Folder Structure (`frontend/src/app`)

```
app/
├── app.config.ts / app.routes.ts / app.ts        # zone.js, lazy routes
├── core/
│   ├── models/            # TS interfaces mirroring API DTOs (api-types.ts)
│   ├── services/          # AuthService, StoreService, ProductService,
│   │                      # OrderService, DashboardService, PublicStoreService,
│   │                      # CartService (signals + localStorage), TranslationService
│   ├── interceptors/      # apiInterceptor (base URL + bearer + 401 refresh)
│   └── guards/            # authGuard, guestGuard, storeGuard
├── shared/                # OmrPricePipe, TranslatePipe, status chip,
│                          # confirm dialog, empty state, image w/ fallback
├── features/
│   ├── landing/           # marketing page + pricing
│   ├── auth/              # login, register, forgot/reset password
│   ├── onboarding/        # create-store wizard (slug, profile, WhatsApp)
│   ├── dashboard/         # shell (nav) + home (stats)
│   ├── products/          # list, product editor (variants), category manager
│   ├── orders/            # order board w/ filters + status actions + detail
│   ├── settings/          # store profile, ordering, opening hours, plan
│   └── storefront/        # public store page, product detail, cart, checkout,
│                          # order confirmation (/:slug/...)
└── assets/i18n/en.json, ar.json                  # runtime i18n, RTL-aware
```

- Standalone components, lazy `loadComponent`/`loadChildren` per feature.
- Signals for all template-bound state; Reactive Forms for input; no NgRx.
- Runtime i18n via a light `TranslationService` (JSON dictionaries, `dir` switching,
  persisted choice) — additional languages = add a JSON file.
- Dev proxy (`proxy.conf.json`) maps `/api` + `/uploads` → API, so the app uses
  relative URLs in dev and prod (nginx does the same in Docker).

## 3. ASP.NET Core Folder Structure (`backend/`)

```
backend/
├── WhatsOrder.sln
├── src/
│   ├── WhatsOrder.Domain/
│   │   ├── Entities/      # Store, StoreSettings, Category, Product, ProductImage,
│   │   │                  # ProductVariant, ProductVariantOption, Customer, Order,
│   │   │                  # OrderItem, WhatsAppMessage, RefreshToken, Subscription
│   │   └── Enums/         # OrderStatus, FulfillmentMethod, SubscriptionPlan, …
│   ├── WhatsOrder.Application/
│   │   ├── Common/        # interfaces, exceptions, PhoneNumber, Slugs, Money,
│   │   │                  # OpeningHours, PlanLimits
│   │   ├── Auth/ Stores/ Categories/ Products/ Orders/ Dashboard/ WhatsApp/
│   │   │                  # per-feature: DTOs + Service + Validators
│   │   └── DependencyInjection.cs
│   ├── WhatsOrder.Infrastructure/
│   │   ├── Persistence/   # AppDbContext, entity configurations, migrations,
│   │   │                  # audit/soft-delete SaveChanges logic, seeder
│   │   ├── Identity/      # ApplicationUser, AuthService, JwtTokenService
│   │   ├── WhatsApp/      # WhatsAppApiClient, dispatcher (Channel + HostedService),
│   │   │                  # WebhookSignatureValidator, options
│   │   ├── Files/ Email/  # LocalFileStorage, LoggingEmailSender
│   │   └── DependencyInjection.cs
│   └── WhatsOrder.Api/
│       ├── Controllers/   # Auth, Store, Categories, Products, Orders, Dashboard,
│       │                  # Subscription, PublicStores, WhatsAppWebhook
│       ├── Middleware/    # ExceptionHandlingMiddleware (ProblemDetails)
│       ├── Filters/       # FluentValidation action filter
│       └── Program.cs, appsettings*.json
└── tests/
    ├── WhatsOrder.UnitTests/          # phone, slug, pricing, opening hours,
    │                                  # plan limits, WhatsApp payloads, signatures
    └── WhatsOrder.IntegrationTests/   # WebApplicationFactory + SQLite in-memory:
                                       # auth flow, tenant isolation, public order
                                       # flow, status transitions, WhatsApp queue
```

## 4. Database Schema (PostgreSQL)

Identity tables (`AspNetUsers` with `Guid` keys, roles, …) plus:

```
stores            id PK, owner_id FK→users (UNIQUE), slug VARCHAR(40) UNIQUE,
                  name, name_ar, description, description_ar, logo_url,
                  whatsapp_number, instagram_handle, location_text, governorate,
                  wilayat, is_accepting_orders BOOL, order_sequence INT,
                  is_deleted, created_at, updated_at
store_settings    store_id PK/FK, delivery_fee NUMERIC(10,3), minimum_order_amount
                  NUMERIC(10,3), delivery_enabled, pickup_enabled,
                  opening_hours_json TEXT, currency CHAR(3)='OMR',
                  default_language, created_at, updated_at
categories        id PK, store_id FK, name, name_ar, sort_order, is_active,
                  is_deleted, created_at, updated_at            IX(store_id)
products          id PK, store_id FK, category_id FK NULL, name, name_ar,
                  description, description_ar, price NUMERIC(10,3),
                  discounted_price NUMERIC(10,3) NULL, stock_quantity INT NULL
                  (NULL = untracked), is_available, is_featured, is_deleted,
                  created_at, updated_at        IX(store_id, is_deleted)
product_images    id PK, product_id FK, path, sort_order, is_primary
product_variants  id PK, product_id FK, name, name_ar, is_required, sort_order
product_variant_options id PK, product_variant_id FK, name, name_ar,
                  price_adjustment NUMERIC(10,3), is_available, sort_order
customers         id PK, store_id FK, name, phone, orders_count, total_spent,
                  first_order_at, last_order_at   UNIQUE(store_id, phone)
orders            id PK, store_id FK, order_number VARCHAR(20), customer_id FK NULL,
                  customer_name, customer_phone, status INT, fulfillment_method INT,
                  delivery_address, google_maps_url, preferred_time, notes,
                  subtotal, delivery_fee, discount, total NUMERIC(10,3/12,3),
                  created_at, updated_at
                  UNIQUE(store_id, order_number), IX(store_id, status),
                  IX(store_id, created_at DESC)
order_items       id PK, order_id FK (cascade), product_id FK NULL (SET NULL),
                  product_name, variants_text, unit_price NUMERIC(10,3),
                  quantity INT, line_total NUMERIC(12,3)
whatsapp_messages id PK, store_id FK NULL, order_id FK NULL, direction INT,
                  to_phone, type INT, body TEXT, wa_message_id, status INT,
                  error, created_at, updated_at   IX(wa_message_id)
refresh_tokens    id PK, user_id FK, token_hash UNIQUE, expires_at, created_at,
                  revoked_at NULL, replaced_by_token_hash NULL
subscriptions     id PK, store_id FK UNIQUE, plan INT, status INT, starts_at,
                  ends_at NULL, external_customer_id, external_subscription_id,
                  created_at, updated_at
```

`OrderStatus`: New(0) Confirmed(1) Preparing(2) Ready(3) OutForDelivery(4)
Completed(5) Cancelled(6). Cancelling restocks tracked inventory.

## 5. API Endpoint Design

**Auth** (`/api/auth`, anonymous, rate-limited)
`POST register · POST login · POST refresh · POST logout · POST forgot-password ·
POST reset-password · GET me` (authorized)

**Owner** (JWT required, tenant resolved from token — never from the client)
```
GET  /api/store                     PUT /api/store
PUT  /api/store/settings            POST /api/store/logo (multipart)
POST /api/store                     GET /api/store/slug-available?slug=
GET|POST /api/categories            PUT|DELETE /api/categories/{id}
GET  /api/products?search=&categoryId=&page=      GET /api/products/{id}
POST /api/products                  PUT /api/products/{id}
DELETE /api/products/{id}           PATCH /api/products/{id}/availability
POST /api/products/{id}/images      DELETE /api/products/{id}/images/{imageId}
GET  /api/orders?filter=today|new|preparing|completed|cancelled&page=
GET  /api/orders/{id}               PATCH /api/orders/{id}/status
GET  /api/dashboard/summary
GET  /api/subscription              POST /api/subscription/plan
```

**Public** (anonymous, rate-limited per IP)
```
GET  /api/public/stores/{slug}                      # profile + settings + categories
GET  /api/public/stores/{slug}/products?search=&categoryId=&featured=
GET  /api/public/stores/{slug}/products/{id}
POST /api/public/stores/{slug}/orders               # checkout
GET  /api/public/stores/{slug}/orders/{orderNumber}?phone=   # status lookup
```

**WhatsApp webhooks**
`GET /api/webhooks/whatsapp` (Meta verification handshake) ·
`POST /api/webhooks/whatsapp` (HMAC-SHA256 `X-Hub-Signature-256` validated)

Conventions: DTOs only (no EF entities), FluentValidation on every write,
RFC 7807 ProblemDetails errors, pagination `{ items, total, page, pageSize }`.

## 6. WhatsApp Integration Architecture

```
Checkout ──► OrderService.CreatePublicOrder ──► PostgreSQL (order saved first)
                    │ enqueue (post-commit)
                    ▼
        WhatsAppNotificationQueue (bounded Channel<T>)
                    ▼
        WhatsAppDispatcher : BackgroundService
          • builds bilingual message text (Application layer)
          • plan check (Free plan ⇒ recorded as Skipped)
          • IWhatsAppApiClient → POST graph.facebook.com/{ver}/{phoneNumberId}/messages
          • persists WhatsAppMessage row: Pending → Sent/Failed (+ wa_message_id)
                    ▲
Webhook POST ───────┘ statuses (sent/delivered/read/failed) update the same row;
                      inbound customer messages are persisted for later features.
```

- All credentials (`WhatsApp__PhoneNumberId`, `__BusinessAccountId`, `__AccessToken`,
  `__WebhookVerifyToken`, `__AppSecret`) come from environment variables; nothing is
  ever exposed to Angular — every call goes through the backend.
- `WhatsApp__Enabled=false` (default in dev) keeps the whole product functional
  without Meta credentials: messages are recorded as `Skipped` instead of sent.
- Business-initiated messages outside Meta's 24-hour customer-service window require
  approved **templates** — the client supports both free-text (test numbers /
  sandbox) and template sends (`WhatsApp__UseTemplates=true` + template names).
- Webhook GET answers Meta's `hub.challenge` when `hub.verify_token` matches;
  webhook POST is rejected unless the HMAC-SHA256 signature over the raw body
  matches (constant-time compare).
- Order flow triggers: order created → customer confirmation + owner alert;
  status changed → customer status update.

## 7. Implementation Phases

| Phase | Scope | Verified by |
|---|---|---|
| 1 | Solution scaffold, DB schema + migrations, Identity + JWT + refresh rotation, store creation & settings, slug rules | build + auth/store integration tests |
| 2 | Categories & products CRUD, variants, images, plan limits | product/tenant-isolation tests |
| 3 | Public storefront API + Angular storefront, cart (signals + localStorage), checkout | public order flow tests |
| 4 | Order management API + dashboard orders board, status transitions, restocking | order status tests |
| 5 | WhatsApp Cloud API client, dispatcher, webhooks + signature validation | unit + queue integration tests |
| 6 | Dashboard analytics, Arabic/RTL i18n, UX polish, landing page | build + component tests |

Each phase: backend → frontend → wire up → validation → tests → verify flow.
