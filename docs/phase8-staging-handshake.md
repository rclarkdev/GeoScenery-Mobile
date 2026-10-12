# Phase 8 staging handshake runbook

**Status:** Prepared for owner review; not executed. This runbook is for staging only. It does not authorize DNS, database, production, or infrastructure changes.

## 1. Preconditions and owner approvals

1. **Staging platform site:** Through the platform's approved staging administration process, ensure a site has key exactly `geoscenery`, status Active, a settings row, and a domain whose normalized host is exactly the GeoScenery staging hostname and whose `IsVerified` flag is true. Do not set verification flags manually or use a client-supplied site ID to select the site.
2. **Host preservation:** The platform resolves from `Request.Host.Host`; it does not use `X-Forwarded-Host` to choose a site. Confirm the staging ingress forwards the request Host for the registered hostname and routes an unknown test hostname to the same staging API if wrong-host testing is planned.
3. **TLS and origin:** Use public staging hostnames with valid HTTPS certificates. Do not use reserved test suffixes or IP literals for the production-configured platform URL; the GeoScenery production build validator rejects them.
4. **CORS owner approval (current blocker):** The platform's current `AllowPolicy` is a hard-coded origin list and does not include a GeoScenery staging origin. Obtain approval to allow the exact GeoScenery staging origin before browser verification. Do not broaden to `AllowAnyOrigin`. The policy currently allows credentials, but this consumer request sets `withCredentials: false`; do not change credential policy as part of this run.
5. **GeoScenery runtime config:** Build the staging web artifact using the normal production wrapper with `GEOSCENERY_API_URL` set to the staging GeoScenery API origin and `GEOSCENERY_PLATFORM_CONFIG_URL` set to the platform HTTPS origin only (no path, query, fragment, username, or password). Confirm the generated runtime asset contains those public origins and is loaded before the application bundle. Never place tokens or secrets in runtime config.
6. **Read-side effects accepted:** The anonymous platform request currently participates in visitor tracking and request/action logging (details below). Obtain the staging data-owner's acceptance of these expected effects and retention before issuing browser requests.
7. **Browser identity observation:** `SiteIdentityService` is not rendered into the UI. Use an owner-approved, staging-only Playwright/QA harness that instantiates the real `SiteIdentityService` and real `HttpClient` against the configured platform origin, or another approved runtime diagnostic that reads its `currentState`. Do not add a production debug endpoint or claim acceptance from the HTTP response alone.

## 2. Direct platform API checks

Set these placeholders from the staging owner: `PLATFORM_HOST` (verified GeoScenery host), `UNKNOWN_HOST` (unverified host routed to the same staging API), and `STAGING_INGRESS_IP`. Do not substitute production values. Use `curl.exe --resolve` to connect to the staging ingress without changing DNS; certificate validation remains enabled.

```powershell
curl.exe --resolve "${env:PLATFORM_HOST}:443:${env:STAGING_INGRESS_IP}" `
  -i "https://$env:PLATFORM_HOST/api/site/config"
```

Expected: `200`, `Cache-Control: no-store`, JSON whose `key` is exactly `geoscenery`, and the configured optional `name` (plus the established public configuration fields). This direct request sends no Authorization header and uses no cookie jar.

Test an unknown host only if the staging ingress routes it to this same API and its TLS certificate is valid for that host:

```powershell
curl.exe --resolve "${env:UNKNOWN_HOST}:443:${env:STAGING_INGRESS_IP}" `
  -i "https://$env:UNKNOWN_HOST/api/site/config" `
  -H "X-Forwarded-Host: $env:PLATFORM_HOST"
```

Expected: `404`; the forwarded header must not select the GeoScenery site. If TLS/routing cannot safely reach the same API for the unknown hostname, record this check as blocked rather than disabling certificate checks or altering host-verification rules.

Confirm a client-supplied ID cannot change the selected site:

```powershell
curl.exe --resolve "${env:PLATFORM_HOST}:443:${env:STAGING_INGRESS_IP}" `
  -i "https://$env:PLATFORM_HOST/api/site/config?siteId=<another-staging-site-id>"
```

Expected: the same host-resolved `geoscenery` key, not the other site's identity. Do not include user data or credentials in the query.

## 3. Browser-level anonymous credential and identity check

Use an isolated Playwright browser context against the deployed **staging** GeoScenery origin and platform origin. They must be cross-origin for the cookie assertion to exercise the consumer's `withCredentials: false` behavior.

1. Seed only a fake, non-valid bearer marker into the GeoScenery tab's `sessionStorage` under `geoscenery.auth.token`. Do not use a real user token.
2. Add a harmless sentinel cookie for the platform staging origin (Secure and SameSite=None) in the isolated context. Use a throwaway test value, never a real session cookie.
3. Before navigating, register a Playwright request listener for exactly `https://<PLATFORM_HOST>/api/site/config`. Inspect `request.allHeaders()` in memory and record only booleans for whether `authorization` or `cookie` exists. **Never print or save header values.**
4. Navigate to GeoScenery and wait for the real request and the approved QA harness's `SiteIdentityService.currentState` observation. Do not mock the platform response or `HttpClient` in this run.
5. Expected request: `GET /api/site/config`, an `Origin` matching the GeoScenery staging origin, no `Authorization` header, and no `Cookie` header. Expected response: `200`, exact key `geoscenery`, valid JSON, and no CORS error. Expected consumer state: `status: available`, `identity.key: geoscenery`, and the optional name present only if supplied and valid.
6. Repeat with an intentionally wrong host only through the approved staging ingress route: expect 404 and consumer `unavailable`/`http-error`, not an accepted identity. The app must continue normal startup.
7. Capture browser console errors, request URL/method/origin, the two credential-presence booleans, status, response key/name only, consumer state/failure category, and correlated staging request IDs. Redact IPs, user-agent strings, cookies, bearer values, and unrelated site settings from shared evidence.

`SKIP_AUTH` causes the GeoScenery auth interceptor to forward the request unchanged, and `withCredentials: false` prevents cross-origin browser credentials. The sentinel browser check proves behavior for that tested browser/origin combination; it does not establish behavior for same-origin requests, native Capacitor networking, other browsers, or unrelated platform endpoints.

## 4. Expected platform-side effects to account for

The controller's configuration lookup uses `AsNoTracking()` and does not update the site, domain, or settings. However, the full anonymous request path is not globally side-effect-free:

- `VisitorTrackingMiddleware` creates a `VisitorProfile` when no valid `com_visitor_id` cookie is received, or updates the existing visitor's last-seen time, IP address, user-agent, language, referrer, and last path. When it creates a visitor, it returns an HttpOnly `com_visitor_id` cookie (Secure for HTTPS, SameSite=Lax, one-year expiry).
- `RequestActivityLoggingMiddleware` writes one `ApplicationLog` per non-health request with request category/message, path/method/status, timing and sanitized query metadata, user/visitor IDs when available, trace ID, and timestamps.
- `GlobalActivityLoggingFilter` writes a second `ApplicationLog` for the controller action with category `BackendAction`, action/result/status metadata, path/method and user/visitor/trace IDs. Neither log stores an Authorization header or cookie value in its documented fields.
- These GET paths do not write an `AuditLog`; audit logging is limited to mutation HTTP methods in those middleware/filter branches.

Consequently, a cross-origin browser request that omits cookies may create a new visitor row on each request if the browser does not store/return the platform's cross-origin visitor cookie. This is an existing platform behavior, not a GeoScenery contract change. Do not claim strict no-state-change/read-only verification unless the owner explicitly defines whether visitor analytics and operational logs are excluded from that term.

## 5. Evidence and completion record

Record for each run:

- staging release/build IDs and date, without credentials;
- the active site's key/status and verified host, confirmed through approved staging administration (do not export private settings);
- ingress host-forwarding and CORS approval/config evidence;
- direct request/response status, `Cache-Control`, response key/name, and request ID;
- wrong-host and client-ID result, or the precise reason either could not be routed safely;
- browser request origin/method and credential-presence booleans;
- browser response status and consumer identity state/failure category;
- console/server errors and correlated request IDs;
- expected visitor-profile and application-log counts/records, with personal data redacted;
- explicit statement that this was staging only. Do not describe unit tests, mocked responses, or build success as a live handshake.

A handshake is verified only when the direct API request proves host-based selection and the unmocked browser consumer accepts that response while the browser evidence confirms no Authorization or Cookie header. Staging/production sign-off and any decision to make the endpoint strictly side-effect-free remain owner responsibilities.
