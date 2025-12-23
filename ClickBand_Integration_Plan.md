# ClickBand Integration Plan

Based on the ClickBand Technical Specification for the Telegram bot, backend API, Vue.js web client, SignalR real-time communication, Redis cache and Dockerized deployment. :contentReference[oaicite:0]{index=0}  

---

## 1. Integration Goals & Constraints

1. **Functional coverage**
   - Full support for the described flow:
     - Add bot to Telegram chat → tag bot → bot creates room via API → bot posts room link → users open web UI and control a shared metronome.
   - Additional web-based room creation and invite links.
2. **Operational goals**
   - Simple setup for local dev and production: `docker-compose up` as the primary entry point.
   - Minimal manual steps for configuration (Telegram bot token, webhook URL, Redis and DB connection strings).
3. **Synchronization goals**
   - Server-authoritative metronome schedule.
   - Effective inter-client drift of **no more than a few milliseconds** in steady state.
   - Robust to temporary connectivity issues and client joins/leaves.

---

## 2. High-Level Architecture

### 2.1 Components

- **Telegram Bot (C#, ASP.NET Core)**
  - Exposes a webhook endpoint: `/api/telegram/webhook`.
  - Handles commands/mentions, calls backend room API, posts room link back to Telegram.
- **Backend API (C#, ASP.NET Core)**
  - REST endpoints for room management & configuration.
  - SignalR hub for real-time synchronization.
  - Redis for transient data:
    - Room state (tempo, time signature, status).
    - Participants list.
    - Synchronization timestamps & offsets.
- **Web Client (Vue 3)**
  - SPA served by backend or static file server.
  - Connects to SignalR hub for real-time events.
  - Implements local metronome scheduling based on server timing.
- **Redis**
  - Central ephemeral store for rooms, users, sync parameters.
- **Docker & Docker Compose**
  - Separate services:
    - `clickband-api` (backend + Telegram webhook + SignalR hub).
    - `clickband-web` (Vue app; optionally served by `clickband-api`).
    - `clickband-redis`.
  - Optional `reverse-proxy` (e.g., Nginx) for TLS and URL routing.

### 2.2 Integration Topology

- Telegram → HTTPS → `clickband-api` (webhook).
- Web client → HTTPS → `clickband-api` REST + SignalR (WebSockets).
- `clickband-api` ↔ Redis for room/user state.
- All containers on the same Docker network to minimize internal network latency.

---

## 3. Environment & Configuration Strategy

### 3.1 Environments

- **Local development**
  - `docker-compose.dev.yml` with:
    - `ASPNETCORE_ENVIRONMENT=Development`
    - `TELEGRAM_WEBHOOK_URL` pointing to ngrok / local tunnel.
- **Staging**
  - `docker-compose.staging.yml` for integration tests and bot sandbox.
- **Production**
  - `docker-compose.yml` + overrides for secrets & resource limits.

### 3.2 Configuration Mechanism

- **Backend API & Bot**
  - Base config: `appsettings.json`.
  - Environment-specific overrides: `appsettings.Development.json`, `appsettings.Production.json`.
  - Secrets and non-committable settings via environment variables:
    - `TELEGRAM_BOT_TOKEN`
    - `TELEGRAM_WEBHOOK_SECRET`
    - `REDIS_CONNECTION_STRING`
    - `BASE_PUBLIC_URL` (e.g. `https://clickband.example.com`)
    - `SYNC_MAX_DRIFT_MS` (e.g. `3`)
- **Web Client**
  - `.env`, `.env.development`, `.env.production`:
    - `VITE_API_BASE_URL`
    - `VITE_SIGNALR_HUB_URL`
- **Docker Compose**
  - `.env` file:
    - Common variables reused by all services (ports, image tags, environment names).
  - Service-level `environment:` sections mapping env vars into containers.

### 3.3 Config Templates

- Provide sample templates:
  - `env.sample` for root compose env.
  - `env.api.sample`, `env.web.sample` if needed.
- Documentation section “Configuration Quick Start” explaining each variable.

---

## 4. Data & State Integration (Redis)

### 4.1 Key Structures

- **Room state key**
  - `room:{roomId}:state`
  - Fields:
    - `tempoBpm`
    - `timeSignature`
    - `status` (`stopped` / `running`)
    - `lastServerBeatTimestamp` (server UTC ticks / ms)
    - `beatIntervalMs`
    - `createdAt`, `lastUpdatedAt`
- **Participant list**
  - `room:{roomId}:participants` (set or hash).
- **Sync parameters per client**
  - `room:{roomId}:client:{clientId}:offset`
  - Fields:
    - `clockOffsetMs` (client clock vs server clock)
    - `lastPingMs`
    - `lastSyncAt`

### 4.2 Lifecycle

- **Create room**
  - API generates `roomId`.
  - Stores initial state in Redis.
- **Join room**
  - SignalR connection registers `clientId` in participants set.
  - Performs synchronization handshake (see section 6).
- **Update room (tempo, Start/Stop)**
  - Centralized in backend:
    - Updates Redis state.
    - Broadcasts to all clients via SignalR.
- **Cleanup**
  - Inactivity timeout (e.g. 30–60 min) via background job:
    - Remove room keys and offset keys.
  - Periodic clean task integrated into API (e.g., hosted service).

---

## 5. Backend API Integration

### 5.1 REST Endpoints

1. **Room creation (via bot & web)**
   - `POST /api/rooms`
   - Request:
     - Optional `chatId` (Telegram).
   - Response:
     - `roomId`, `roomUrl`, default tempo/time signature.
2. **Room details**
   - `GET /api/rooms/{roomId}`
   - Returns current state for SEO / initial page load / diagnostics.
3. **Administrative endpoints (optional)**
   - `DELETE /api/rooms/{roomId}`
   - `GET /api/admin/rooms` (debug).
   - `GET /api/admin/rooms/{roomId}/participants`.

### 5.2 Telegram Webhook Endpoint

- `POST /api/telegram/webhook`
  - Validates secret header.
  - Parses update:
    - Detect mention / command.
  - Calls `POST /api/rooms` to create room.
  - Sends callback message with `roomUrl` using Telegram Bot API.

---

## 6. Real-Time Synchronization Design

This is the core of “maximum synchronization” and must be integrated across backend, web client and Redis.

### 6.1 SignalR Hub Contract

- Hub: `/hub/metronome`
- Methods (server → client):
  - `RoomStateUpdated(roomStateDto)`
  - `MetronomeStart(syncPayload)`
  - `MetronomeStop(syncPayload)`
  - `TempoChanged(syncPayload)`
  - `SyncPingResponse(pingId, serverTimestamp)`
- Methods (client → server):
  - `JoinRoom(roomId, clientCapabilities)`
  - `LeaveRoom(roomId)`
  - `RequestMetronomeStart(roomId)`
  - `RequestMetronomeStop(roomId)`
  - `RequestTempoChange(roomId, newTempo)`
  - `SyncPingRequest(pingId, clientTimestamp)`

### 6.2 Time Base & Clock Synchronization

1. **Authoritative time**
   - Server uses `DateTime.UtcNow` or a monotonic clock (e.g. `Stopwatch.GetTimestamp` mapped to UTC).
   - Server host is synced with NTP (OS-level responsibility, but documented as a requirement).
2. **Client clock offset estimation**
   - On `JoinRoom`:
     - Client runs a multi-sample ping:
       - Repeatedly sends `SyncPingRequest(pingId, clientSendTimestamp)`.
       - Server replies with `SyncPingResponse(pingId, serverTimestamp)`.
       - Client measures round-trip time (RTT).
     - For each sample:
       - `offset ≈ serverTimestamp - (clientSendTimestamp + RTT/2)`
     - Client picks the sample with **minimum RTT** (best path) and stores:
       - `bestOffsetMs`, `bestRttMs`.
   - Client stores `offset` in memory and optionally API records in Redis for diagnostics.

### 6.3 Scheduling Start/Stop with Millisecond-Level Sync

1. **Server-side on Start**
   - When a user presses Start in the web UI:
     - Client calls `RequestMetronomeStart`.
   - Server:
     - Validates room state and permissions.
     - Reads current `serverTimeUtc`.
     - Chooses `startAtServerTime = serverTimeUtc + syncLeadTime` (e.g., 2000 ms).
     - Updates Redis room state:
       - `status = running`
       - `lastServerBeatTimestamp = startAtServerTime`
       - `beatIntervalMs = 60000 / tempoBpm`
     - Broadcasts `MetronomeStart(syncPayload)` to all room clients via SignalR:
       - `syncPayload` includes:
         - `startAtServerTime`
         - `tempoBpm`
         - `beatIntervalMs`
         - `roomId`
2. **Client-side on Start event**
   - On `MetronomeStart`:
     - Client computes local scheduled start time:
       - `scheduledLocalStart = startAtServerTime - offsetMs`
     - Convert to high-resolution timer (e.g. `performance.now()` in browser + AudioContext time base).
     - Schedule metronome ticks via Web Audio API:
       - Use audio scheduler that schedules a few seconds of ticks in advance.
       - Use the same `beatIntervalMs`.
   - This yields inter-client sync accuracy:
     - Bound by:
       - Offset estimation error (few ms if RTT is low and multiple samples).
       - Audio scheduling resolution (1–2 ms typical).
3. **Stop and Tempo Change**
   - Similar pattern:
     - Server defines a `stopAtServerTime` or `applyTempoAtServerTime`.
     - Broadcasts event with server timestamp.
     - Clients convert to local time via `offset` and adjust schedule.

### 6.4 Drift Correction

1. **Periodic resync**
   - Clients periodically (e.g. every 10–20 seconds) perform new clock offset pings.
   - If offset drift > threshold (e.g. 2–3 ms):
     - Apply gradual correction:
       - Slightly adjust intervals between ticks for a few beats instead of abrupt jump.
2. **Late joiners**
   - On `JoinRoom` when room is already running:
     - Server sends:
       - `lastServerBeatTimestamp`
       - `beatIntervalMs`
       - `tempoBpm`
       - `serverTimeUtc`
     - Client computes:
       - `beatsElapsed = (serverTimeUtc - lastServerBeatTimestamp) / beatIntervalMs`
       - Aligns to current beat boundary and schedules from that point.

### 6.5 SignalR Transport & Performance

- Force WebSockets where possible:
  - Configure SignalR on server to prefer WebSockets.
  - In Vue client, configure SignalR connection options to disable or de-prioritize long polling.
- Keep payloads small:
  - Compact DTOs (no unnecessary fields).
  - Minimal JSON size to reduce latency.
- Host server & Redis close together (same datacenter/region) to avoid extra latency.

---

## 7. Telegram Bot Integration Plan

### 7.1 Bot Registration & Setup

1. Use BotFather to create a bot:
   - Retrieve `TELEGRAM_BOT_TOKEN`.
2. Configure Webhook:
   - `https://<public-host>/api/telegram/webhook?secret=<random>`
   - Set the secret as `TELEGRAM_WEBHOOK_SECRET`.
3. Document steps in `docs/telegram-setup.md`.

### 7.2 Bot Message Handling

1. **Update parsing**
   - Handle:
     - Mentions (`@ClickBandBot` in group chats).
     - Commands like `/clickband` or `/metronome`.
2. **Room creation**
   - On appropriate message:
     - Call `POST /api/rooms` with `chatId` and optional metadata.
     - Receive `roomUrl`.
   - Send back message:
     - “ClickBand room created: <roomUrl>”.
3. **Error handling**
   - If API fails:
     - Log error.
     - Respond with user-friendly message: “Temporary issue, please try again later.”

### 7.3 Bot Logging & Monitoring

- Log:
  - Incoming updates.
  - API call statuses.
  - Response times.
- Ensure logs are accessible via Docker logs or centralized logging.

---

## 8. Web Client Integration Plan (Vue + SignalR)

### 8.1 Application Structure

- Vue 3 application with:
  - `RoomView` component for `/room/:roomId`.
  - `MetronomeControls` component (tempo input, Start/Stop).
  - `StatusIndicator` for sync status, drift, connection quality.
- Use a UI library (PrimeVue) for:
  - Inputs, buttons, dialogs, notifications.

### 8.2 Room View Initialization

1. On route enter:
   - Fetch room details via `GET /api/rooms/{roomId}`.
   - Establish SignalR connection with `roomId` and `clientId` (generated GUID).
2. On successful connection:
   - Perform sync handshake:
     - Send multiple pings to estimate `offsetMs`.
   - Join room via hub method `JoinRoom(roomId, clientCapabilities)`.

### 8.3 Metronome Controls Behavior

- **Start**
  - User clicks Start.
  - Frontend:
    - Disable button temporarily.
    - Call hub method `RequestMetronomeStart(roomId)`.
  - Updates metronome view once `MetronomeStart` event is received with `syncPayload`.
- **Stop**
  - Same pattern with `RequestMetronomeStop`.
- **Tempo change**
  - User changes BPM.
  - Frontend either:
    - Immediately updates local display but waits for `TempoChanged` event to actually reschedule.
  - Hub method `RequestTempoChange(roomId, newTempo)`.

### 8.4 Visual Feedback for Synchronization

- Display:
  - Current BPM.
  - Connection status (Connected/Disconnected/Reconnecting).
  - Estimated drift (e.g., `< 3 ms`).
- Use non-blocking toasts for:
  - Connection loss.
  - Room closed.

---

## 9. Deployment & Docker Integration

### 9.1 Dockerfile Structure

- **Backend**
  - Multi-stage build:
    - `sdk` image for build.
    - `aspnet` runtime image for final.
  - Environment variables for:
    - `ASPNETCORE_ENVIRONMENT`
    - `TELEGRAM_BOT_TOKEN`
    - `REDIS_CONNECTION_STRING`
- **Web Client**
  - Build with Node (Vite).
  - Serve via:
    - Either static files from `clickband-api` (copy into `wwwroot`).
    - Or separate lightweight web server container (e.g. Nginx).

### 9.2 docker-compose.yml

- Services:
  - `api`:
    - Depends on `redis`.
    - Exposes port `5000`/`80`.
    - Environment: secrets and URLs.
  - `web` (optional if not served by `api`):
    - Depends on `api`.
  - `redis`:
    - Standard Redis image.
- Network:
  - Shared custom network.
- Volumes:
  - Optional volume for logs / diagnostics.

### 9.3 Deployment Steps

1. Build images:
   - `docker-compose build`.
2. Set environment variables (or `.env` file).
3. Run:
   - `docker-compose up -d`.
4. Configure Telegram webhook with the final public URL.
5. Verify:
   - API health endpoint (e.g. `/health`).
   - Web UI accessible.
   - Bot responds to mentions and posts room links.

---

## 10. Observability

### 10.1 Logging & Metrics

- Add structured logging in backend:
  - Room lifecycle, sync events, drift details.
- Expose minimal metrics (via /metrics or logs) for:
  - Average RTT.
  - Average estimated drift.
  - Number of active rooms / participants.

---

## 11. Rollout & Operational Checklist

1. **Before first deployment**
   - All Docker images build successfully.
   - `.env` files and secrets configured.
   - Redis reachable from API container.
   - Telegram bot token & webhook configured.
2. **After deployment**
   - Health checks OK.
   - Redis keys appear on room creation.
   - Web UI loads and connects to SignalR hub.
   - Bot generates functioning room links.
3. **Synchronization**
   - Multi-client tests show:
     - Start/Stop actions in one client reflect on others.
     - Beat timing difference within a few milliseconds.
4. **Ongoing ops**
   - Monitor logs for:
     - Drift corrections.
     - Error rates (API, Redis, SignalR).
   - Periodic cleanup job verified (old rooms removed).

---

## 12. Summary

This integration plan:

- Covers all functional requirements from Telegram-bot-based room creation to web metronome control.
- Provides a clear configuration and deployment story via environment variables and Docker Compose.
- Implements a precise, server-authoritative synchronization model with clock offset estimation, scheduled starts, and drift correction to maintain inter-client timing differences within a few milliseconds.
