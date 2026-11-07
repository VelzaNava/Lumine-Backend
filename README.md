# Lumine API

ASP.NET Core REST API for **Lumine**, an Android AR jewelry try-on app (thesis project: MediaPipe hand/face tracking + Unity 3D rendering). It serves the catalog, accounts, favorites and try-on ratings, backed by [Supabase](https://supabase.com) (PostgreSQL, Auth, Storage).

## Tech stack

| | |
|---|---|
| Runtime | .NET 10 (LTS), ASP.NET Core controllers |
| Data, auth, files | Supabase - PostgREST, Auth (GoTrue) and Storage, called over REST with typed `HttpClient`s |
| API docs | OpenAPI + [Scalar](https://scalar.com) at `/scalar` (development only by default) |
| Tests | xUnit + `WebApplicationFactory` integration tests |

## How it works

```
Android app --HTTPS--> Caddy --> Lumine API +--> Supabase Auth      (sign-in, codes, tokens)
                                            +--> Supabase database  (catalog, profiles, favorites, ratings)
                                            +--> Supabase Storage   (product photos, avatars)
```

- **Controllers** are thin: validate input, check access, call a service.
- **Services** hold the business rules (`Services/`).
- **Supabase clients** (`Infrastructure/Supabase/`) are stateless - every call carries its own credentials, so one user's session can never leak into another request.
- **Errors** always come back as `{ "error": "message" }` with a meaningful status code. Unexpected exceptions are logged in full and returned as a generic 500, so internals never reach the client.

## Security model

- The app signs in through `POST /api/auth/login` and receives a Supabase **access token** (1 hour) and **refresh token**. Every other call sends `Authorization: Bearer <accessToken>`; when it expires the app calls `POST /api/auth/refresh`.
- Tokens are verified with Supabase on each request (cached for up to 2 minutes, never past the token's expiry).
- **Secure by default:** every endpoint requires a signed-in user unless marked public.
- **Admins** are the accounts listed in `AdminEmails`. Only they can change the catalog, manage users or read ratings.
- **Ownership:** a user can only read or change their own profile and favorites (admins can access any).
- **Rate limits** per client IP: 30 auth requests/minute, and 5 emailed codes per 5 minutes.
- **Wrong password:** that email must wait 10 seconds before signing in again. The 401 and any attempt during the wait (429) carry a `Retry-After` header, which the app counts down on its Log In button.
- The Supabase **service_role key stays on the server**. The app never sees it.

## Endpoints

| Method | Route | Access |
|---|---|---|
| `POST` | `/api/auth/login` | Public |
| `POST` | `/api/auth/refresh` | Public |
| `POST` | `/api/auth/send-otp`, `/api/auth/verify-and-register` | Public (sign-up by emailed code) |
| `POST` | `/api/auth/forgot-password`, `/api/auth/reset-password` | Public (not for admin accounts) |
| `GET` | `/api/jewelry`, `/api/jewelry/{id}` | Public |
| `POST` `PUT` `DELETE` | `/api/jewelry[/{id}]`, `/api/jewelry/upload-image` | Admin |
| `GET` `PUT` | `/api/profile/{userId}` | Owner |
| `POST` | `/api/profile/{userId}/avatar` | Owner |
| `GET` `POST` `DELETE` | `/api/profile/{userId}/favorites[/{jewelryId}]` | Owner |
| `POST` | `/api/account/delete` | Signed in, not admins (asks for the password again) |
| `GET` `DELETE` | `/api/admin/users[/{userId}]`, `/api/admin/users/{userId}/favorites` | Admin |
| `POST` | `/api/evaluation` | Signed in |
| `GET` | `/api/evaluation/summary`, `/api/evaluation/all` | Admin |
| `GET` | `/health` | Public |
| `GET` | `/privacy`, `/delete-account` | Public web pages for the Google Play listing |

Deleting an account (by its owner or an admin) removes the account, profile, profile photo and favorites. Ratings are kept for the try-on results, with their link to the account removed.

## Configuration

Settings come from `appsettings.json` (git-ignored - copy `appsettings.example.json`) or environment variables (`Supabase__Url`, `AdminEmails__0`, ...).

| Key | Required | Description |
|---|---|---|
| `Supabase:Url` | Yes | Project URL, e.g. `https://abcd.supabase.co` |
| `Supabase:Key` | Yes | **service_role** key (Supabase > Project Settings > API). Server only. |
| `Supabase:AnonKey` | Yes | **anon** public key |
| `AdminEmails` | | Emails of admin accounts |
| `ApiDocs:Enabled` | | Serve `/scalar` and `/openapi/v1.json`. Default: on in Development, off in Production |
| `Cors:AllowedOrigins` | | Only needed for a browser client; the Android app doesn't use CORS |
| `Urls` / `ASPNETCORE_URLS` | | Listen address. Default `http://0.0.0.0:5111` |

The API refuses to start if a required Supabase setting is missing.

## Run locally

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
cp appsettings.example.json appsettings.json   # then fill in the Supabase keys
dotnet run
```

- API: `http://localhost:5111`, docs: `http://localhost:5111/scalar`, health: `http://localhost:5111/health`
- To reach it from a phone, tunnel it (e.g. `ngrok http 5111`) and point the app's API URL at the tunnel.

## Tests

```bash
dotnet test Lumine.Backend.Tests
```

The integration tests host the real API with fake Supabase services and check the access rules: public vs. signed-in vs. admin, ownership, validation errors and the error format.

## Deploy to a Linux VPS (e.g. Linode, Ubuntu 24.04)

The API runs as a systemd service on `127.0.0.1:5111`; [Caddy](https://caddyserver.com) serves it over HTTPS with an automatic certificate.

**Before you start:** a server with a public IP, and a domain (or a free [DuckDNS](https://www.duckdns.org) name) whose DNS `A` record points at that IP.

**1. Build** (on your PC, from this folder). This produces a self-contained Linux build in `bin/publish/linux-x64/`, with the .NET runtime included:

```bash
dotnet publish -p:PublishProfile=linux-x64
```

**2. Upload** the build and the deploy files:

```bash
scp -r bin/publish/linux-x64 root@SERVER_IP:/opt/lumine-api
scp deploy/lumine-api.service deploy/Caddyfile root@SERVER_IP:/root/
```

The build includes your `appsettings.json` with the Supabase keys. Keep that folder private and never commit it.

**3. Set up the server** (`ssh root@SERVER_IP`):

```bash
apt update && apt install -y caddy
useradd --system --no-create-home --shell /usr/sbin/nologin lumine
chmod +x /opt/lumine-api/Lumine.Backend
chown -R lumine:lumine /opt/lumine-api && chmod 600 /opt/lumine-api/appsettings.json

cp /root/lumine-api.service /etc/systemd/system/
systemctl daemon-reload && systemctl enable --now lumine-api
curl http://127.0.0.1:5111/health          # prints Healthy

nano /root/Caddyfile                        # replace api.example.com with your domain
cp /root/Caddyfile /etc/caddy/Caddyfile && systemctl reload caddy

ufw allow OpenSSH && ufw allow 80 && ufw allow 443 && ufw enable
```

**4. Check** `https://your-domain/health` in a browser, then set the app's API URL to `https://your-domain` and build the app.

**Updating:** build again (step 1), then replace the old build. `scp` into an existing folder would nest the copy inside it, so remove the old folder first:

```bash
ssh root@SERVER_IP "systemctl stop lumine-api && rm -rf /opt/lumine-api"
scp -r bin/publish/linux-x64 root@SERVER_IP:/opt/lumine-api
ssh root@SERVER_IP "chmod +x /opt/lumine-api/Lumine.Backend && chown -R lumine:lumine /opt/lumine-api && chmod 600 /opt/lumine-api/appsettings.json && systemctl start lumine-api"
```

**Logs:** `journalctl -u lumine-api -f`

**Using nginx instead of Caddy:** if the server already runs nginx, use [`deploy/lumine-api.nginx.conf`](deploy/lumine-api.nginx.conf) in place of steps 3-4's Caddy setup:

```bash
apt update && apt install -y unzip nginx certbot python3-certbot-nginx
# ...install the API as in step 3, then:
cp deploy/lumine-api.nginx.conf /etc/nginx/sites-available/lumine-api   # after replacing api.example.com with your domain
ln -s /etc/nginx/sites-available/lumine-api /etc/nginx/sites-enabled/
rm -f /etc/nginx/sites-enabled/default
nginx -t && systemctl reload nginx
certbot --nginx -d your-domain   # answer the prompts; it edits the config to add HTTPS
```

`ufw allow` and the health check are the same either way. **Logs:** `journalctl -u nginx -f`, and `/var/log/letsencrypt/letsencrypt.log` if `certbot` itself fails.

## Database updates

Paste [`database/update.sql`](database/update.sql) into the Supabase **SQL Editor** and run it. It's safe to run more than once. It:

- adds the columns this version needs (`stock_quantity`, `image_urls` for up to 3 photos per item);
- makes favorites unique per user + item;
- **locks the database down:** row-level security on every table with no public policies. Only this API (service_role key) can read or write data, so the anon key or a user's own token can't be used against Supabase directly;
- makes storage buckets view-only for the public (only the API can upload or delete, and nobody can list files).

On startup the API checks for the columns and logs a clear message if the script hasn't been run.

## Response times

Every response carries a `Server-Timing: app;dur=<milliseconds>` header, and every request is logged with its duration:

```
2026-09-28 14:02:11 info: ...RequestTimingMiddleware[0] GET /api/jewelry responded 200 in 143.2 ms
```

- **Server log:** `journalctl -u lumine-api -f` (on the VPS), or the `dotnet run` console locally.
- **From any computer** (network + server time), e.g. 10 requests:
  `for i in $(seq 10); do curl -s -o /dev/null -w "%{time_total}s\n" https://your-domain/api/jewelry; done`
- **From the app:** debug builds log every call in Android Studio's Logcat (filter `okhttp`), ending with the total time, e.g. `<-- 200 https://.../api/jewelry (212ms)`.

The server time includes its calls to Supabase; what a phone experiences adds the network round trip on top.

## Supabase setup (new projects only)

Only needed if you point the API at a brand-new Supabase project. You need:

- public Storage buckets `jewelry` and `avatars`
- the tables below

```sql
create table public.jewelry (
  id uuid primary key default gen_random_uuid(),
  created_at timestamptz default now(),
  name text, type text, material text, price numeric,
  description text, image_url text, image_urls text[] not null default '{}', model_url text,
  is_available boolean default true,
  is_ar_enabled boolean default false,
  stock_quantity integer
);

create table public.user_profiles (
  id uuid primary key references auth.users(id) on delete cascade,
  username text, first_name text, last_name text,
  mobile_number text, avatar_url text
);

create table public.favorites (
  id uuid primary key default gen_random_uuid(),
  user_id uuid references auth.users(id) on delete cascade,
  jewelry_id text not null
);
-- Recommended: one row per user + item.
create unique index if not exists favorites_user_jewelry on public.favorites (user_id, jewelry_id);

create table public.evaluations (
  id uuid primary key default gen_random_uuid(),
  user_id uuid, jewelry_id text, jewelry_name text,
  rating int check (rating between 1 and 5),
  comment text,
  created_at timestamptz default now()
);
```

In **Authentication > Emails > SMTP settings**, configure a real email provider. Supabase's built-in sender is only meant for testing and is heavily rate-limited, so sign-up and reset codes won't reach real users without it.

## Project structure

```
Controllers/         HTTP endpoints (thin)
Services/            Business rules: auth, catalog, profiles, admin, ratings
Infrastructure/      Typed Supabase REST clients (auth, database, storage)
Security/            Bearer-token authentication, roles, ownership checks
Contracts/           Request/response DTOs - the app's JSON contract
Data/                Database rows (snake_case) and table/bucket names
Errors/              ApiException, error body, global exception handler
Hosting/             Service registration and OpenAPI setup
deploy/              systemd unit and Caddyfile
Lumine.Backend.Tests/  Integration tests
```

The Android client lives in a separate repository: [VelzaNava/LumineApp](https://github.com/VelzaNava/LumineApp).

## License

MIT - see [LICENSE](LICENSE).
