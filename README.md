# WhatsOrder Oman 🇴🇲

A SaaS platform for small businesses in Oman that sell through Instagram and WhatsApp.
Each business gets a beautiful storefront link (`whatsorder.om/alreem`), an order
dashboard, and automatic WhatsApp confirmations via the **Meta WhatsApp Cloud API** —
no website needed. Customers order without creating an account.

**Stack:** Angular 21 · ASP.NET Core 8 · EF Core · PostgreSQL · WhatsApp Cloud API ·
Docker. Full design details in [ARCHITECTURE.md](ARCHITECTURE.md).

---

## Quick start

### Option A — Docker (full stack with PostgreSQL)

```bash
cp .env.example .env        # then set POSTGRES_PASSWORD and Jwt__SigningKey
docker compose up --build
```

| URL | What |
|---|---|
| http://localhost:4200 | App (landing → register → dashboard) |
| http://localhost:4200/alreem | Seeded demo storefront |
| http://localhost:5000/swagger | API + Swagger UI |

### Option B — local development (no Docker needed)

The API can run on a local SQLite file instead of PostgreSQL:

```powershell
# API  (http://localhost:5000, seeds demo data, SQLite file DB)
cd backend/src/WhatsOrder.Api
$env:Database__Provider = 'Sqlite'
dotnet run

# Frontend  (http://localhost:4200, /api proxied to :5000)
cd frontend
npm install
npm start
```

To develop against real PostgreSQL instead, start only the database container
(`docker compose up db`) and run the API without `Database__Provider`
(connection string in `appsettings.Development.json`). EF migrations apply
automatically at startup.

### Demo account (Development / `App__SeedDemoData=true`)

```
Store:    http://localhost:4200/alreem   (Al Reem Cakes — Pro plan)
Email:    demo@whatsorder.om
Password: Demo@1234
```

---

## The core flow (works end-to-end)

Business registers → creates store (unique slug) → adds products (variants, images,
stock, discounts) → shares the public link → customer browses, fills a cart, checks
out with name + phone + pickup/delivery → order saved with a `WO-1001`-style number →
owner sees it on the dashboard and advances the status
(`New → Confirmed → Preparing → Ready → Out for delivery → Completed`, cancel any
time with automatic restock) → customer receives WhatsApp confirmations and status
updates → delivery receipts come back via webhook.

## WhatsApp Cloud API setup

The product is fully functional without Meta credentials — messages are recorded in
the `whatsapp_messages` table as `Skipped` instead of sent. To send for real:

1. Create a Meta app → add the **WhatsApp** product (developers.facebook.com).
2. From *WhatsApp → API Setup* copy into `.env`:
   - `WhatsApp__PhoneNumberId`, `WhatsApp__BusinessAccountId`, `WhatsApp__AccessToken`
   - `WhatsApp__AppSecret` (App settings → Basic)
   - set `WhatsApp__Enabled=true`
3. Configure the webhook (*WhatsApp → Configuration*):
   - Callback URL: `https://<your-domain>/api/webhooks/whatsapp` (use ngrok in dev)
   - Verify token: the value you chose for `WhatsApp__WebhookVerifyToken`
   - Subscribe to the `messages` field.
4. Test-mode numbers can receive free-text messages. In production, messages sent
   outside Meta's 24-hour customer-service window require **approved templates**:
   create templates whose body is `{{1}}`, then set `WhatsApp__UseTemplates=true`
   and the template names in configuration.

Webhook signatures (`X-Hub-Signature-256`) are validated with the app secret;
delivery statuses (sent/delivered/read/failed) update the message log. WhatsApp
notifications are a **Pro-plan** feature (the seeded demo store is Pro; new stores
start Free — switch instantly in *Settings → Plan*, billing is intentionally not
wired yet).

## Testing

```bash
cd backend  && dotnet test     # 132 tests: unit + full API integration suite
cd frontend && npm test        # 22 vitest specs
```

Integration tests run the real HTTP pipeline (WebApplicationFactory + SQLite) and
cover: auth + refresh-token rotation & reuse detection, tenant isolation, the whole
public order flow with server-side pricing, stock + restock, plan limits, WhatsApp
dispatch logging, webhook verification/signatures.

## Security highlights

- JWT auth (15-min tokens) + rotating refresh tokens, hashed at rest, family
  revocation on reuse; ASP.NET Core Identity password policy + lockout.
- Multi-tenant isolation: the store is always resolved server-side from the JWT —
  no endpoint accepts a store id from the client; cross-tenant access returns 404.
- Prices are always recomputed from the database at checkout; client prices are
  never trusted. FluentValidation on every write.
- Rate limiting per IP on auth and public endpoints (6 orders/min).
- WhatsApp webhook HMAC validation (constant-time), tokens only server-side.
- Uploads validated (type/size), stored outside the web app's code paths.

## Repository layout

```
backend/   ASP.NET Core 8 solution (Domain / Application / Infrastructure / Api + tests)
frontend/  Angular 21 app (storefront + dashboard, EN/AR with RTL)
docker-compose.yml, .env.example, ARCHITECTURE.md
```

## Oman specifics

Prices are OMR with 3 decimals (`8.500 OMR`); phone numbers normalize to
`+968XXXXXXXX` (other countries accepted, so international expansion stays open);
opening hours use Asia/Muscat (UTC+4); full Arabic UI with correct RTL layout —
switch languages from any page header.

## Not in this MVP (prepared for)

Billing (subscription entity + plan limits exist; Stripe/Lemon Squeezy slot into
`SubscriptionService`), email delivery (interface exists, dev logs the reset links),
S3/object storage for images (swap `IFileStorage`), customer accounts (intentionally
never required).
