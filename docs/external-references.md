# Official Artifacts MMO references

Use these upstream resources when implementing or reviewing API behavior. They are external and may change; the repository's DTOs, routes and assumptions must be checked against the current OpenAPI document before compatibility-sensitive changes.

## Primary links

| Resource | URL | Purpose |
|---|---|---|
| Game site and encyclopedia | <https://artifactsmmo.com/> | Game data, world content and public entry point |
| Official documentation | <https://docs.artifactsmmo.com/> | Getting started, API guide, concepts, mechanics and changelog |
| Game concepts / wiki | <https://docs.artifactsmmo.com/concepts/> | Combat, skills, crafting, inventory, maps, trading, tasks and other mechanics |
| OpenAPI guide | <https://docs.artifactsmmo.com/api_guide/openapi_spec/> | Explanation of the machine-readable API specification |
| OpenAPI specification | <https://api.artifactsmmo.com/openapi.json> | Authoritative endpoint and schema description for compatibility work |
| Interactive API reference | <https://api.artifactsmmo.com/docs/#/> | Hosted Swagger UI for browsing endpoints and schemas |
| API changelog | <https://docs.artifactsmmo.com/changelog/> | Upstream behavior and schema changes |
| Response codes | <https://docs.artifactsmmo.com/api_guide/response_codes> | Game-specific HTTP/error codes |
| Rate limits | <https://docs.artifactsmmo.com/api_guide/rate_limits> | Current request buckets and throttling rules |
| Actions and cooldowns | <https://docs.artifactsmmo.com/concepts/actions> | Action lifecycle and cooldown contract |

## How to use these references

- Before changing `Artiact.Contracts/Models/Api`, compare the affected schema with the current OpenAPI specification.
- Before adding or changing `GameClient` routes, confirm method, path, request body, response model, authentication, cooldown and documented error codes.
- Before changing planning rules, confirm the corresponding game mechanic in the concepts documentation; do not infer domain policy solely from DTO fields.
- Check the changelog when cached JSON or previously working deserialization changes unexpectedly.
- Never paste Bearer tokens, Basic credentials or authenticated responses into tracked documentation, issue reports or AI prompts.
- Treat the interactive Swagger UI as an external side-effect surface: authenticated action requests can change a real character.

## Known local divergence

`Artiact.MockService` implements only a small subset of the official API. Its fixed token, empty cooldowns, local mutation rules and error behavior are test fixtures—not evidence of the upstream contract. See [Mock service](mock-service.md).
