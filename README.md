# ClickBand

ClickBand is a collaborative, server-authoritative metronome platform that links a Telegram bot, an ASP.NET Core backend, a Vue 3 client and Redis-backed synchronization services. This repository implements the integration plan in `ClickBand_Integration_Plan.md`.

## Project layout

```
ClickBand.sln
├── src/ClickBand.Api        # ASP.NET Core API, SignalR hub, Telegram webhook
└── web                      # Vue 3 + Vite single-page client
```

## Backend

* **Tech stack**: .NET 9, ASP.NET Core, SignalR, StackExchange.Redis, Telegram.Bot.
* **Endpoints**:
  * `POST /api/rooms` – create a room with optional tempo/time signature.
  * `GET /api/rooms/{roomId}` / `DELETE /api/rooms/{roomId}` – retrieve or close a room.
  * `POST /api/telegram/webhook` – Telegram webhook guarded with `X-Telegram-Bot-Api-Secret-Token`.
  * `GET /health` – liveness and Redis health.
* **Real-time**: `/hubs/rooms` exposes hub methods for joining rooms, requesting metronome start/stop/tempo changes and clock pings. Server broadcasts snapshots, participant changes and sync payloads.
* **Redis schema**: room state (`room:{id}:state`), participants (`room:{id}:participants`), clock offsets (`room:{id}:client:{clientId}:offset`).

### Configuration

Settings are sourced from `appsettings*.json` + environment variables (preferred in production).

| Environment variable | Description |
| --- | --- |
| `TELEGRAM__BOTTOKEN` | Telegram bot token used to send messages. |
| `TELEGRAM__WEBHOOKSECRET` | Shared secret expected in the webhook header. |
| `TELEGRAM__BASEPUBLICURL` | Public URL used for invite links. |
| `REDIS_CONNECTION_STRING` | Redis endpoint (`host:port`). |
| `ROOMS__*`, `SYNCHRONIZATION__*` | Override defaults for tempo, TTL, sync lead time and drift thresholds. |

## Frontend

* **Tech stack**: Vue 3 (SFC), Vite, TypeScript, @microsoft/signalr, date-fns.
* **Features**:
  * Create rooms from the web UI and surface shareable links.
  * Join an existing room, perform a multi-sample clock sync and display drift.
  * Control the metronome (start/stop/tempo) with optimistic UI locks.
  * Live participant list with relative timestamps.
  * Connection state badge (idle/connecting/connected/reconnecting/disconnected).
  * Local metronome engine aligns playback to server schedule within a few ms.
* **Environment variables** (`env.web.sample`): `VITE_API_BASE_URL`, `VITE_SIGNALR_HUB_URL`.

To run locally:

```
cd web
npm install
npm run dev
```

## Docker & Compose

* `src/ClickBand.Api/Dockerfile` – multi-stage .NET publish.
* `web/Dockerfile` – Node build → Nginx static host.
* `docker-compose.yml` – orchestrates `clickband-api`, `clickband-web`, and `clickband-redis`. Configure ports via `.env`. The web container reverse-proxies `/api/*` and `/hubs/*` to the API service, so browsers always talk to `http://localhost:${WEB_PORT}`.
* Sample env files: `.env.sample`, `env.api.sample`, `env.web.sample` (web values are injected at **build** time and are optional unless you need fully-qualified URLs).

Usage:

```
cp .env.sample .env
cp env.api.sample env.api
cp env.web.sample env.web
# fill secrets, webhook URL, etc.
docker compose up --build
```

After containers start:

1. Hit `http://localhost:${API_PORT}/health` to validate the API/Redis connection.
2. Serve the Telegram webhook via a public URL (ngrok/tunnel) and register it with Telegram.
3. Load `http://localhost:${WEB_PORT}` to access the client and verify SignalR events. If you change API URLs, rebuild `clickband-web` so the Vite variables are recompiled (`docker compose build clickband-web`).

> **Note:** Telegram only renders inline buttons for HTTPS URLs. Set `TELEGRAM__BASEPUBLICURL` to your public HTTPS domain when deploying (ngrok, reverse proxy, etc.).

## Testing & validation

* `dotnet build ClickBand.sln` validates the backend, including SignalR hub compilation.
* Frontend type-checks via `npm run build`.
* End-to-end: create a room in the UI, note the invite link, open multiple browser tabs and confirm the metronome stays in sync (drift badge ≤ `SYNCHRONIZATION__MAXDRIFTMS`).
