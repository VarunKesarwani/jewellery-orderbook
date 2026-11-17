# Deployment

How to get the app off your laptop and onto your phone. Three shapes, in increasing order of
seriousness. Pick the one that matches what you are doing today.

| | Use when | Sign-in works | Survives a reboot |
|---|---|---|---|
| **A. Same Wi-Fi** | you are at your desk, testing | no | no |
| **B. Tailscale** | you are out, or the phone is on mobile data | yes | no |
| **C. Containers** | the shop is actually using it | yes | yes |

The reason A cannot sign in: Entra requires an HTTPS redirect URI and only exempts `localhost`. A
phone hitting `http://192.168.1.50:4200` is neither, so Microsoft rejects the redirect. B and C solve
that with a real certificate.

---

## A. Same Wi-Fi

Both devices on the same network.

**1. Find the laptop's address.** `ipconfig` on Windows, `ipconfig getifaddr en0` on macOS,
`hostname -I` on Linux. You want the IPv4 address, usually `192.168.x.x` or `10.x.x.x`.

**2. Start the API.** It already listens on all interfaces by default when run this way:

```bash
cd src/api
dotnet run --urls http://0.0.0.0:5080
```

**3. Start the web app on all interfaces.**

```bash
cd src/web
npm run start:lan
```

`start:lan` is `ng serve --host 0.0.0.0`. The API does not need to be reachable from the phone
directly — the dev server proxies `/api` to `localhost:5080` on the laptop, so the phone only ever
talks to port 4200.

**4. Open `http://<laptop-ip>:4200` on the phone.**

If the page does not load: the laptop firewall is blocking port 4200. On Windows, allow Node.js on
private networks; on macOS, accept the incoming-connections prompt.

### About `--disable-host-check`

Older guides tell you to add it. **It no longer exists** — Angular removed the flag, and passing it
now is an unknown-option error. Angular 20's dev server is Vite-based, and the equivalent is
`allowedHosts` in `angular.json`, which this repository already sets. IP addresses are accepted
without being listed; hostnames need to be.

---

## B. Tailscale

Reachable from anywhere, no port forwarding, no public exposure.

**1. Install Tailscale on the laptop and the phone, signed in to the same account.** The laptop gets
a `100.x.x.x` address and a MagicDNS name like `laptop.tailnet-name.ts.net`.

**2. Enable HTTPS and get a certificate.** In the Tailscale admin console, turn on HTTPS for the
tailnet, then on the laptop:

```bash
tailscale cert laptop.tailnet-name.ts.net
```

That writes `laptop.tailnet-name.ts.net.crt` and `.key` into the current directory. Tailscale
certificates are publicly trusted — which is exactly what Entra needs.

**3. Serve the app over HTTPS.**

```bash
cd src/web
npx ng serve --host 0.0.0.0 \
  --ssl --ssl-cert /path/to/laptop.tailnet-name.ts.net.crt \
  --ssl-key  /path/to/laptop.tailnet-name.ts.net.key
```

The `allowedHosts` entry for `.ts.net` is already in `angular.json`, so the hostname is accepted.

**4. Start the API** the same way as above. The dev server still proxies to `localhost:5080`.

**5. Add the redirect URI to Azure.** In the SPA app registration, add
`https://laptop.tailnet-name.ts.net:4200` — port included if you keep 4200. It must match the
address bar character for character.

**6. Open `https://laptop.tailnet-name.ts.net:4200` on the phone.**

### Turn the phone Wi-Fi off to prove it

With the phone on mobile data and Tailscale connected, the app should load exactly as it does at
home. If it does not, the phone's Tailscale is not running or is signed into a different account.

---

## C. Containers

The shape the design assumed: two containers behind a reverse proxy, everything bound to the tailnet.

```bash
docker compose up --build -d
```

That brings up `api` (port 5080) and `web` (nginx on 4200, serving the built app and proxying `/api`
to the API container). `docker-compose.yml` is in the repository root.

What is still missing for a shop to rely on this:

- **A reverse proxy with TLS.** Add Caddy in front, using the Tailscale certificate, and stop
  publishing ports directly.
- **`restart: unless-stopped`** on both services, so a reboot brings the app back.
- **A persistent volume for the database.** `src/api/data/orderbook.db` must survive a container
  rebuild — mount it, do not leave it inside the image.
- **The backup job.** Nothing takes a snapshot yet; see the design documents.

### Where the data lives

SQLite writes to `src/api/data/orderbook.db` relative to the working directory. In a container that
is `/app/data/orderbook.db`. Mount a host directory there, and back up that file — it is the entire
database, including WAL side files while the app is running.

---

## Things that break, in the order they usually do

**The phone cannot reach the laptop.** `ng serve` binds `localhost` unless told otherwise; that is
what `start:lan` is for. Then check the firewall.

**"Blocked request. This host is not allowed."** The hostname is not in `allowedHosts`. Add it, or
use the IP address instead.

**Sign-in fails only on the phone.** The redirect URI in Azure does not match the URL in the address
bar, or the URL is plain HTTP. Both have to be right, and the port counts.

**Sign-in succeeds, every API call returns 401.** The API's `Entra:Audience` does not match the
token's audience, or the SPA is registered as a *Web* platform instead of *Single-page application*.
The API logs the exact rejection reason under the `JewelleryOrderbook.Auth` category.

**Sign-in succeeds, every API call returns 403.** The user has no app role assigned. A valid token
with no `roles` claim passes signature validation and fails every policy.

**The app loads but every screen is empty.** The API is not running, or the dev server's proxy target
points somewhere else. `curl http://localhost:5080/health/live` should answer.

---

## Never do this

Do not port-forward `ng serve` or the API to the public internet, with or without
`--disable-host-check`. The dev server has no authentication of its own, serves source maps, and will
happily expose your database to anyone who finds the port. Tailscale exists precisely so you never
have to.
