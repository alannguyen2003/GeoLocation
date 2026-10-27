# CLAUDE.md: Location Tracker

Guidance for any agent working in this repository. Read it fully before writing code.

## What we are building

An app that records the location of the current device and stores it in a database.

- **Backend:** ASP.NET Core Web API on .NET 10 (LTS), three-layer architecture, PostgreSQL through EF Core. Deployed to **Render** as a Docker web service.
- **Frontend:** React Native with **Expo** (Expo Router, TypeScript). One codebase, two targets:
  - **Mobile (Android, iOS):** foreground and background tracking with `expo-location` + `expo-task-manager`. Built with **EAS Build**, not deployed to Vercel.
  - **Web:** foreground tracking only while the tab is open. Exported as a static site and deployed to **Vercel**.
- **No user login.** Each install is identified by an anonymous device ID plus a secret device token (see "Device identity").
- **Place names:** every point also gets a city, region (province/state) and country, resolved on the server from an embedded GeoNames dataset, plus a best-effort ward/district from the phone's own geocoder while the app is open (see "Place names").

Out of scope unless asked: user accounts, maps UI beyond a simple list, analytics, push notifications.

## Repository layout

```
/
├── CLAUDE.md
├── render.yaml                    # Render Blueprint (API + Postgres)
├── docker-compose.yml             # Local only: Postgres + API on http://localhost:8080
├── docs/
│   └── MOBILE_BUILD.md            # Step-by-step guide to build and install the app on phones
├── backend/
│   ├── Dockerfile
│   ├── .dockerignore
│   ├── LocationTracker.sln
│   ├── tools/
│   │   └── build-places.cs               # Regenerates the embedded GeoNames place data
│   ├── src/
│   │   ├── LocationTracker.Api/          # Presentation layer
│   │   ├── LocationTracker.Business/     # Business logic layer
│   │   └── LocationTracker.Data/         # Data access layer (Places/ holds the GeoNames data and lookup)
│   └── tests/
│       ├── LocationTracker.Business.Tests/
│       └── LocationTracker.Api.Tests/
└── mobile/
    ├── app.json
    ├── eas.json
    ├── vercel.json
    ├── app/                       # Expo Router routes
    └── src/
        ├── api/
        ├── identity/
        └── location/
```

## Backend

### Three layers and the dependency rule

`Api → Business → Data`. Never the other way, and the Api project must not reference `Data` types directly except in `Program.cs` for DI registration.

| Layer | Project | Contains | Must not contain |
|---|---|---|---|
| Presentation | `LocationTracker.Api` | Controllers, request/response DTOs, validation attributes, middleware (device auth, error handling), CORS, rate limiting, `Program.cs` | Business rules, EF Core queries |
| Business logic | `LocationTracker.Business` | Service interfaces and implementations (`IDeviceService`, `ILocationService`), domain rules (validation of coordinates and timestamps, batch limits, dedupe, when a place match counts), token hashing, mapping between DTOs and entities | `DbContext`, HTTP types (`HttpContext`, `IActionResult`) |
| Data access | `LocationTracker.Data` | `AppDbContext`, entities, EF Core configurations, migrations, repositories (`IDeviceRepository`, `ILocationRepository`), the place lookup (`IPlaceLookup`, `GeoNamesPlaceLookup`) and its embedded dataset | Business rules, HTTP types |

Keep controllers thin: parse input, call one service method, map the result to an HTTP response.

### Data model (PostgreSQL)

`devices`
- `id` uuid PK (server-generated)
- `token_hash` text, not null (SHA-256 of the device token; never store the raw token)
- `platform` text (`android`, `ios`, `web`)
- `created_at` timestamptz, `last_seen_at` timestamptz

`location_points`
- `id` bigint identity PK
- `device_id` uuid FK → devices (cascade delete)
- `client_point_id` uuid, not null (generated on the device; used to drop duplicates on retry)
- `latitude` double precision, `longitude` double precision
- `accuracy_m` real null, `altitude_m` real null, `speed_mps` real null, `heading_deg` real null
- `recorded_at` timestamptz (device time, UTC), `received_at` timestamptz (server time)
- `source` text (`foreground`, `background`, `web`)
- `district` text null (ward/district from the phone's geocoder; best effort)
- `city` text null, `region` text null, `country_code` varchar(2) null (ISO 3166-1 alpha-2), `country` text null (resolved on the server from GeoNames; null when no place is within 100 km)
- Unique index on `(device_id, client_point_id)`
- Index on `(device_id, recorded_at DESC)`
- Index on `(recorded_at)` for the retention delete

Use plain latitude/longitude columns. Do not add PostGIS unless a task needs spatial queries.

Retention: add a configurable `Retention:Days` setting (default 90) and a hosted background service in the Api project that deletes older points once a day through `ILocationService`.

### API

All routes under `/api`. JSON, camelCase.

| Method | Route | Auth | Purpose |
|---|---|---|---|
| GET | `/health` | none | Health check for Render; also checks DB connectivity |
| POST | `/api/devices` | none | Register a device. Body `{ platform }`. Returns `{ deviceId, deviceToken }` once |
| POST | `/api/locations/batch` | device | Upload 1 to 100 points. Ignore duplicates by `clientPointId`. Returns `{ accepted, duplicates }` |
| GET | `/api/locations?from&to&limit` | device | The device's own points, newest first, `limit` max 500 |
| GET | `/api/locations/latest` | device | The device's latest point or 204 |
| DELETE | `/api/devices/me` | device | Delete the device and all its points |

Batch body: `{ points: [{ clientPointId, latitude, longitude, accuracy?, altitude?, speed?, heading?, recordedAt, source, district? }] }`. Point responses carry the same fields plus `id`, `receivedAt`, `city`, `region`, `countryCode`, `country`. Clients never send `city`, `region` or `country`; the server sets them.

Validation rules (enforce in the Business layer, return 400 with a problem details body):
- latitude in [-90, 90], longitude in [-180, 180]
- `recordedAt` not more than 5 minutes in the future and not older than 7 days
- `accuracy` ≥ 0 when present
- batch size 1 to 100
- `district` is never a reason to reject: trim it, store empty as null, cut it to 100 characters

### Device identity (no login)

1. On first launch the app calls `POST /api/devices` and stores `deviceId` and `deviceToken` securely.
2. Every later request sends `X-Device-Id` and `X-Device-Token` headers.
3. A middleware in the Api layer hashes the token, asks `IDeviceService` to verify it, and puts the device ID in `HttpContext.Items`. Reject with 401 otherwise.
4. Compare hashes with `CryptographicOperations.FixedTimeEquals`.

Never log tokens or full coordinates at Information level.

### Place names (reverse geocoding)

Two sources, by design ("A + C"). A walk-through with diagrams is in `docs/PLACE_NAMES_FLOW.md` (Vietnamese); keep it in step when this flow changes.

| Field | Source | Where it runs | When |
|---|---|---|---|
| `city`, `region`, `country_code`, `country` | GeoNames `cities1000` (populated places with ≥ 1,000 people), embedded in `LocationTracker.Data` | Server, offline, in memory | Every uploaded point, on every platform including web and background |
| `district` | The phone's geocoder via `expo-location` `reverseGeocodeAsync` (Google on Android, Apple on iOS) | Phone | Only while the app is in the foreground and online; not on the web |

Server rules:
- `IPlaceLookup` lives in the Data layer next to the repositories, so the dependency rule holds. `GeoNamesPlaceLookup` loads `Places/places.tsv.gz` and `Places/countries.tsv` (embedded resources) once into a 1° grid and is registered as a singleton; it is warmed at startup after migrations. Memory cost is small (the whole API runs in about 125 MB), well inside the free Render instance.
- `LocationService` resolves the nearest place for each point during `UploadBatchAsync`. A match farther than `LocationRules.MaxPlaceDistanceKm` (100 km) is ignored and the fields stay null.
- `PlaceBackfillBackgroundService` (Api) calls `ILocationService.FillMissingPlacesAsync` once, shortly after each start, for points stored without a country.
- Do not call any online geocoding service from the server. Do not add PostGIS for this.
- Names are kept as GeoNames spells them (for example `Hanoi`, `Ho Chi Minh City (HCMC)`, `Da Nang City`); the project owner chose not to translate them.
- `city` is the **nearest populated place**, not an administrative boundary. In big Vietnamese cities GeoNames lists wards and districts as places, so `city` is often a ward or district (`Hoàn Kiếm`, `Quận Một`) and `region` carries the city/province. Near borders the country can be wrong.
- Snapshot in the repo: GeoNames dump of 2026-10-27. Its Vietnamese `region` values already follow the July 2025 merger (34 provinces/cities), but below that level GeoNames still lists former districts (`Quận Một`, `Quận Bình Thạnh`) as places, so `city` can carry a pre-2025 district name. Region spelling is mixed (`Phu Tho`, `Hà Tĩnh Province`). About 285 places worldwide (3 in Vietnam) have no region.
- Data licence: GeoNames, CC BY 4.0. Keep the attribution line in the app's Settings → Thông tin.
- To refresh the data: `cd backend && dotnet run tools/build-places.cs`, then rebuild and commit the two files. It downloads from `download.geonames.org`; the output is about 2.2 MB.
- The data is a fixed snapshot: nothing updates it at build time or at runtime, and there is no scheduled refresh. Refresh only when there is a reason (an administrative reform, names reported wrong, or roughly once a year). A refresh changes names only for points uploaded afterwards: `FillMissingPlacesAsync` fills points that have no country, it does not rewrite existing names.

Phone rules (`src/location/district.native.ts`):
- At most one geocoder request every 30 s, only when `AppState` is `active`, with a 5 s timeout; results cached in memory per ~1 km cell (coordinates rounded to 2 decimals), 500 entries max.
- The uploader adds districts just before sending a batch. A missing district never blocks or delays an upload beyond that one short lookup, and the background task never makes a geocoder request (it only uses cached results).
- Pick `district` from the geocoder result, falling back to `subregion`. Names follow the phone's language.
- `district.web.ts` returns points unchanged.

### Cross-cutting

- **CORS:** allow origins from the `Cors__AllowedOrigins` setting (comma-separated): the Vercel production domain, Vercel preview domains if needed, and `http://localhost:8081` for local Expo web. Allow headers `Content-Type`, `X-Device-Id`, `X-Device-Token`.
- **Rate limiting:** use the built-in ASP.NET Core rate limiter, partitioned by device ID (fallback: client IP). Start with 60 requests per minute per device; registration 10 per hour per IP.
- **Forwarded headers:** enable `ForwardedHeaders` so the real client IP is read behind Render's proxy.
- **Errors:** global exception handler returning RFC 7807 problem details; no stack traces in production.
- **Migrations:** apply with `db.Database.MigrateAsync()` at startup (acceptable while there is a single instance). Note in comments that this should move to a pre-deploy step if the service scales out.

### Configuration

| Setting | Source on Render | Notes |
|---|---|---|
| `DATABASE_URL` | `fromDatabase` in `render.yaml` | Render gives a `postgresql://user:pass@host:port/db` URI. Npgsql expects key-value form, so convert it in `Program.cs` (parse with `Uri`, build with `NpgsqlConnectionStringBuilder`, set `SslMode` appropriately). Locally, use `ConnectionStrings:Default` from user secrets. |
| `PORT` | injected by Render (10000) | Listen on `http://0.0.0.0:{PORT}`; fall back to 8080 locally. |
| `Cors__AllowedOrigins` | dashboard env var | |
| `Retention__Days` | dashboard env var | default 90 |
| `ASPNETCORE_ENVIRONMENT` | `Production` | |

Never commit secrets. `appsettings.Development.json` may hold a local connection string only if it points to a local Docker Postgres with throwaway credentials.

### Dockerfile (backend/Dockerfile)

Multi-stage: build with `mcr.microsoft.com/dotnet/sdk:10.0`, run with `mcr.microsoft.com/dotnet/aspnet:10.0`. Restore before copying the rest of the source so layers cache. Run as the non-root `app` user. Do not hardcode the port in the image; the app reads `PORT`.

Only `src/` is copied into the image. The GeoNames data ships inside it as embedded resources of `LocationTracker.Data` (under `src/LocationTracker.Data/Places/`), so the image needs no network access to GeoNames and nothing extra at runtime. `tests/` and `tools/` are excluded in `.dockerignore`: `tools/build-places.cs` is a developer tool that is run by hand and its output committed; Render never runs it.

### render.yaml (repo root)

This is a PoC, so both resources use the **free** plan. If `plan` is omitted, Render creates paid instances, so always keep `plan: free` here.

Do **not** set `rootDir`. Render's docs disagree on whether `dockerfilePath` and `dockerContext` are relative to the repo root (Blueprint spec) or to `rootDir` (monorepo guide); without `rootDir` both readings agree. Scope auto-deploys with `buildFilter` instead. Its paths are always relative to the repo root; ignored paths win over included ones. Place-data refreshes still deploy because the generated files are under `backend/src/`.

```yaml
services:
  - type: web
    name: location-tracker-api
    runtime: docker
    plan: free
    region: singapore
    dockerfilePath: ./backend/Dockerfile
    dockerContext: ./backend
    buildFilter:
      paths:
        - backend/**
      ignoredPaths:
        - backend/tests/**
        - backend/tools/**
        - backend/**/*.md
    healthCheckPath: /health
    envVars:
      - key: DATABASE_URL
        fromDatabase:
          name: location-tracker-db
          property: connectionString
      - key: ASPNETCORE_ENVIRONMENT
        value: Production
      - key: Retention__Days
        value: "30"
      - key: Cors__AllowedOrigins
        sync: false
databases:
  - name: location-tracker-db
    plan: free
    region: singapore
    postgresMajorVersion: "17"
```

Both resources must be in the same region (Singapore, closest to Vietnam) so the API uses the private connection URL. `region` and `postgresMajorVersion` cannot be changed after creation.

PoC limits to keep in mind: the free API sleeps after 15 minutes idle and takes about a minute to wake; the free database is 1 GB, has no backups, and **expires 30 days after creation** (Render deletes it after a further 14-day grace period). Treat all stored data as disposable.

### Backend tests

- `LocationTracker.Business.Tests`: unit tests for validation, dedupe, token verification and place assignment with fake repositories and a fake `IPlaceLookup`, plus `GeoNamesPlaceLookupTests` against the real embedded dataset (known cities, open sea, the antimeridian).
- `LocationTracker.Api.Tests`: integration tests with `WebApplicationFactory` and a Postgres Testcontainer (or skip if Docker is unavailable, and say so).

## Mobile / web app (Expo)

### Setup rules

- Create with the current Expo SDK template (TypeScript, Expo Router). Install native packages with `npx expo install` so versions match the SDK:
  `expo-location expo-task-manager expo-secure-store @react-native-async-storage/async-storage expo-crypto expo-dev-client`
- Set `"web": { "output": "single" }` in `app.json`. New SDK templates default to `server` output, which Vercel static hosting cannot serve.
- API base URL comes from `EXPO_PUBLIC_API_URL`. Never hardcode it.

### `app.json` location config

Use the `expo-location` config plugin with:
- `locationAlwaysAndWhenInUsePermission` and `locationWhenInUsePermission`: plain-language strings that say what is tracked and why
- `isAndroidBackgroundLocationEnabled: true`
- `isAndroidForegroundServiceEnabled: true`
- `isIosBackgroundLocationEnabled: true`

### Code structure

```
src/identity/deviceStore.native.ts   # expo-secure-store
src/identity/deviceStore.web.ts      # localStorage
src/identity/ensureDevice.ts          # registers once, returns { deviceId, deviceToken }
src/api/client.ts                     # fetch wrapper adding X-Device-* headers, timeout, retry with backoff
src/location/queue.ts                 # persisted queue of pending points (AsyncStorage), max 5,000, oldest dropped first
src/location/backgroundTask.ts        # TaskManager.defineTask at module top level
src/location/tracking.native.ts       # start/stop foreground watch and background updates
src/location/tracking.web.ts          # browser Geolocation API directly, while the page is open
src/location/district.native.ts       # ward/district from the phone's geocoder (foreground only, throttled, cached)
src/location/district.web.ts          # no-op: browsers have no geocoder
src/location/uploader.ts              # flushes the queue in batches of 100, adding districts first
app/_layout.tsx                       # imports backgroundTask.ts so the task is defined at startup
app/index.tsx                         # status, start/stop, last point, pending count
app/history.tsx                       # list of recent points from the API, with place names
app/settings.tsx                      # interval/accuracy, delete my data, GeoNames attribution
```

Platform-specific modules (`x.native.ts` / `x.web.ts`) have a matching `x.d.ts` that declares the shared API: Metro picks the platform file, TypeScript type-checks imports against the `.d.ts`.

### Tracking behaviour

1. Before asking for permission, show an in-app screen that explains what location data is collected, when (including when the app is closed or not in use), and why. Google Play requires this prominent disclosure before the runtime prompt.
2. Ask for foreground permission first. Ask for background permission only when the user turns on background tracking.
3. **Foreground:** `watchPositionAsync` with `Accuracy.Balanced`, `timeInterval` 30 s, `distanceInterval` 25 m (configurable).
4. **Background (mobile only):** `startLocationUpdatesAsync` with a foreground service notification on Android, `deferredUpdatesInterval` 60 s and `deferredUpdatesDistance` 50 m, `pausesUpdatesAutomatically: true` on iOS.
5. Every point gets a `clientPointId` from `expo-crypto` `randomUUID()` and goes into the queue first. The uploader flushes the queue when the app is in the foreground, after each background task run, and on reconnect.
6. The background task must not use React state, hooks or context. It reads the device credentials from storage itself.
7. If the API returns 5xx, times out or the network is down, keep the points and retry with backoff. The free Render instance sleeps after 15 minutes idle and takes about a minute to wake, so the first request after idle often times out; this is expected.
8. **Web:** no background tracking. Show a clear note that tracking stops when the tab is closed. Browser geolocation needs HTTPS (Vercel provides it; `localhost` also works).
   - Use `navigator.geolocation` directly in `tracking.web.ts`, **not** `expo-location` on the web. Its web shim drops watch errors, passes no timeout, and matches watch callbacks by an ID the browser does not share, so updates can silently never arrive (seen with SDK 58).
   - Get one position first (20 s timeout) so the permission prompt appears and a point is recorded at once, then `watchPosition` with an error callback. Show every geolocation error in the UI.
   - Browsers ignore interval/distance options, so apply them in code: after the first point, record only when both the time interval has passed and the device moved at least the distance interval.
9. **Disclosure** must also say that city, region and country are derived from the coordinates on the project's server, and (mobile) that while the app is open the coordinates go to Google/Apple to look up the ward/district.

Background tracking does not run in Expo Go. Test it in a development build.

### Builds and deployment

- **Mobile:** EAS Build. The phone app is not deployed to a server; it is built into an installable file. Use this `mobile/eas.json` as the starting point (check the `cli.version` against the installed `eas-cli`):

```json
{
  "cli": { "version": ">= 16.0.0", "appVersionSource": "remote" },
  "build": {
    "development": {
      "developmentClient": true,
      "distribution": "internal",
      "android": { "buildType": "apk" },
      "env": { "EXPO_PUBLIC_API_URL": "https://<service>.onrender.com" }
    },
    "preview": {
      "distribution": "internal",
      "android": { "buildType": "apk" },
      "env": { "EXPO_PUBLIC_API_URL": "https://<service>.onrender.com" }
    },
    "production": {
      "autoIncrement": true,
      "env": { "EXPO_PUBLIC_API_URL": "https://<service>.onrender.com" }
    }
  }
}
```

  `development` installs a dev client that loads JavaScript from `npx expo start` on the developer's machine. `preview` is a standalone APK for testers. `production` is only for store submission and is out of scope for the PoC. `EXPO_PUBLIC_API_URL` is baked in at build time, so changing the API URL means rebuilding.
- **Web on Vercel:** project root directory `mobile`. `vercel.json`:

```json
{
  "buildCommand": "npx expo export -p web",
  "outputDirectory": "dist",
  "devCommand": "npx expo",
  "cleanUrls": true,
  "framework": null,
  "rewrites": [{ "source": "/:path*", "destination": "/" }]
}
```

Set `EXPO_PUBLIC_API_URL` in the Vercel project settings for Production and Preview. After the first deploy, add the Vercel domain to `Cors__AllowedOrigins` on Render.

### Deployment checklist (PoC)

Do these in order; each step needs the URL from the one before.

1. **Push** the repo to GitHub with `render.yaml` at the root and `backend/Dockerfile` building locally (`docker build` passes).
2. **Render:** New → Blueprint → select the repo. Render reads `render.yaml` and creates the free Postgres and the free Docker web service in Singapore. When prompted for `Cors__AllowedOrigins`, enter `http://localhost:8081` for now.
3. Wait for the deploy, then open `https://<service>.onrender.com/health`. It must return 200 and report the database as reachable (migrations run at startup).
4. Test the API: `POST /api/devices`, then `POST /api/locations/batch` with the returned headers, then `GET /api/locations/latest`.
5. **Vercel:** New Project → same repo → Root Directory `mobile`. Add `EXPO_PUBLIC_API_URL=https://<service>.onrender.com` for Production and Preview. Deploy.
6. **Back on Render:** set `Cors__AllowedOrigins` to `https://<project>.vercel.app,http://localhost:8081` and redeploy.
7. Open the Vercel URL, allow location, start tracking, and confirm points appear in History.
8. **Mobile:** set `EXPO_PUBLIC_API_URL` in `eas.json` for the `development` and `preview` profiles, run `eas build --profile preview --platform android`, install the APK, and test foreground and background tracking (lock the screen, walk, reopen History).
9. Note the database creation date; the free database expires 30 days later.

If step 3 fails with "No open ports detected", the app is not listening on `0.0.0.0:$PORT`.

## Required document: `docs/MOBILE_BUILD.md`

Create this file once `mobile/eas.json` exists, and update it whenever build configuration, package names or the API URL handling change. It is written for a developer who has never used EAS: every step is a command they can copy, with what they should see when it works. Write it in plain language; Vietnamese is preferred by the project owner, with commands and file names left in English.

It must contain these sections, in this order:

1. **Overview:** web goes to Vercel, phones get a build file from EAS. Which build to use when (table: Expo Go, development build, preview APK, iOS). State plainly that background tracking does not work in Expo Go or on the web.
2. **Prerequisites:** Node.js LTS, an Expo account, `npm install -g eas-cli`, `eas login`, `eas whoami`. For iOS: a paid Apple Developer Program membership. For local Android builds: Android Studio with the Android SDK and JDK.
3. **One-time project setup:** `cd mobile`, `eas init` (links the project to the Expo account), `eas build:configure`, set `android.package` and `ios.bundleIdentifier` in `app.json`, fill in `EXPO_PUBLIC_API_URL` in `eas.json` from the Render URL.
4. **Android preview APK (main path for the PoC):** `eas build --profile preview --platform android`; where to watch progress on expo.dev; that free-plan builds are low priority and may queue; how to install from the link or QR code on a phone (allow "install unknown apps"); how to share the link with testers; how to restrict the link to signed-in Expo accounts if needed.
5. **Android development build:** `eas build --profile development --platform android`, install once, then `npx expo start --dev-client` and open the project from the dev client. Explain that JavaScript changes then reload without rebuilding, but native changes (new native package, `app.json` plugin or permission changes) need a new build.
6. **iOS:** with a paid Apple account: `eas device:create` to register each iPhone (up to 100 per year; Apple can take 24 to 72 hours for new devices), then `eas build --profile preview --platform ios` and install from the link. Without a paid account: note that sharing builds is not possible, and the only option is building from Xcode on a Mac onto the developer's own iPhone with `npx expo run:ios --device`.
7. **Local builds (optional, no EAS queue):** `npx expo run:android` for a debug build on a connected device or emulator; `eas build --profile preview --platform android --local` for an APK on the developer's machine.
8. **Test checklist on a real phone:** disclosure screen shows before the permission prompt; foreground tracking records points; enabling background asks for "Allow all the time" (Android) / "Always" (iOS); with the screen locked and walking for a few minutes, points keep arriving; the Android foreground-service notification is visible while tracking; History shows the points; airplane mode queues points and they upload after reconnecting; "Delete my data" removes everything.
9. **Android battery settings:** some Android brands stop background apps aggressively. Explain how to set the app's battery usage to "Unrestricted" / turn off battery optimization, and to allow autostart where the phone offers it, before judging background tracking.
10. **When to rebuild:** a table of changes that need a new build (API URL, native packages, permissions, app icon/name, `app.json` plugins) versus changes that do not (JavaScript and UI code when using the development build).
11. **Troubleshooting:** build fails on credentials (let EAS generate them); app installs but cannot reach the API (check `EXPO_PUBLIC_API_URL`, HTTPS, and that the Render service is awake: the first request after idle can take about a minute); location permission denied permanently (open system settings); background tracking stops (battery settings, user force-quit the app); iOS device not in the provisioning profile (register it, rebuild).

Verify every command in the guide against the current EAS CLI help (`eas build --help`) before writing it, and do not invent flags.

## Commands

```bash
# backend
cd backend
dotnet build
dotnet test
dotnet ef migrations add <Name> -p src/LocationTracker.Data -s src/LocationTracker.Api
dotnet run tools/build-places.cs   # refresh the embedded GeoNames data
docker build -t location-tracker-api .

# local Postgres + API in Docker (repo root); the app then uses EXPO_PUBLIC_API_URL=http://localhost:8080 in mobile/.env.local
docker compose up -d --build
docker compose down        # add -v to delete the local database

# app
cd mobile
npx expo start            # Expo Go / web; background tracking needs a dev build
npx expo start --web
npx tsc --noEmit
npx expo lint
npx expo export -p web    # same as Vercel's build
eas build --profile development --platform android
```

## Working rules for agents

- Before finishing any change: backend `dotnet build` and `dotnet test` pass; app `npx tsc --noEmit` and `npx expo lint` pass.
- Keep the layer rule. If a change needs a database query in a controller, add a repository method and a service method instead.
- Add an EF Core migration for every model change; never edit an applied migration.
- Keep `docs/MOBILE_BUILD.md` in step with `eas.json` and `app.json`; a change to either is not finished until the guide matches.
- When unsure of a package or API version, check the official docs for the current Expo SDK and .NET 10 rather than relying on memory.
- Do not add a login system, analytics SDKs or third-party location services unless the task asks for them. The two approved exceptions are the embedded GeoNames dataset (offline, on the server) and the phone's built-in geocoder for `district` under the rules in "Place names"; do not add online geocoding APIs (Google, Mapbox, Nominatim...) without being asked.
- Do not commit `.env` files, tokens, keystores or connection strings.
- Keep the privacy behaviour intact: disclosure screen before permission (including the place-name sentences), "delete my data" works end to end, no tokens or precise coordinates in logs.