# Changelog

All notable changes to Swaggregate will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **"Try it out"** — execute HTTP requests directly from the portal via a server-side proxy
  - Path, query, and header parameter input fields
  - JSON request body editor for POST/PUT/PATCH (pre-populated from schema)
  - Response panel shows status code, headers, and formatted response body
  - Proxy validates target URL against configured service origins (SSRF protection)
- `BaseUrl` and `PathPrefix` fields on `ServiceGroup` to support request construction
- `POST /api-docs/proxy` middleware endpoint for server-side request forwarding
- `BaseUrl` parsing: OpenAPI 3.x `servers[0].url` (falls back to spec URL origin); Swagger 2.0 `host`+`basePath`+`schemes`

## [0.1.0] - 2025-04-03

### Added
- Initial release of Swaggregate
- ASP.NET Core middleware that aggregates multiple Swagger/OpenAPI specs into a single searchable portal
- Support for **OpenAPI 3.x** (JSON and YAML) and **Swagger 2.0** (JSON and YAML) spec formats
- Automatic YAML detection by URL extension (`.yml`/`.yaml`), `Content-Type` header, and content sniffing
- Embedded portal UI (HTML/CSS/JS) served directly from the NuGet package — no static file hosting required
- Full-text search across all endpoints (path, method, summary, description, tags)
- Dark / light theme with browser-local persistence
- In-memory caching of fetched specs with configurable TTL
- Graceful error handling per service — one failing spec doesn't block the rest
- Optional path prefix rewriting per endpoint (for API gateway scenarios)
- `AddSwaggerAggregator()` / `UseSwaggerAggregator()` fluent configuration API
- Targets `net8.0` and `net9.0`
