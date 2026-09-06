# real-api-readonly-verification Specification

## Purpose
Provide an explicit, read-only verification path for the real Artifacts API that consumes ignored local credentials without exposing them or mutating game state.

## Requirements

### Requirement: Real-server verification is explicit and excluded by default
The real-server project SHALL define separate offline and live test categories and documented commands. The offline command SHALL exclude the live category, require no credentials or opt-in variable, and make zero network requests. The live command SHALL select only the live category and require process environment variable `ARTIACT_REAL_API_READONLY=1`. The project SHALL NOT be included in the default `Artiact.sln` test execution. Merely having a `.env` file SHALL NOT contact the real server. If the live command is invoked without the opt-in variable, its live test SHALL fail explicitly and make zero network requests.

#### Scenario: Default unit test run
- **WHEN** a developer runs the documented default solution test command
- **THEN** no real-server request is made

#### Scenario: Explicit real-server run
- **WHEN** a developer invokes the documented live-category command with `ARTIACT_REAL_API_READONLY=1`
- **THEN** the verifier loads and validates its configuration before contacting the server

#### Scenario: Live-category run without opt-in
- **WHEN** a developer invokes the documented live-category command without `ARTIACT_REAL_API_READONLY=1`
- **THEN** the live test fails explicitly and makes zero network requests

#### Scenario: Dedicated offline run without opt-in
- **WHEN** a developer invokes the documented offline-category command without credentials or `ARTIACT_REAL_API_READONLY=1`
- **THEN** parser, validation, allowlist, redirect, and sanitization tests can pass using only in-memory fixtures and make zero network requests

### Requirement: Dotenv is parsed as data
The verifier SHALL parse the repository-root `.env` as key-value data and SHALL NOT source it, execute it, perform variable expansion, or interpret shell syntax. It SHALL support blank lines, comment lines, the first equals sign as the separator, and optional matching single or double quotes around a complete value.

#### Scenario: Valid dotenv input
- **WHEN** the file contains comments, blank lines, quoted values, or a value containing an equals sign
- **THEN** the parser returns the intended keys and literal values without executing content

#### Scenario: Malformed dotenv input
- **WHEN** a non-comment non-blank line has no key or separator, contains duplicate canonical keys, or has unmatched surrounding quotes
- **THEN** verification fails before any network request and identifies only the invalid key or line number, not secret values

### Requirement: Configuration aliases are deterministic
The verifier SHALL accept canonical .NET keys `ApiSettings__BaseUrl`, `ApiSettings__Username`, `ApiSettings__Password`, and `ApiSettings__Character`, with `API_BASE_URL`, `API_USERNAME`, `API_PASSWORD`, and `API_CHARACTER` as explicit aliases. If both forms for one setting are present with different values, verification SHALL fail before contacting the server.

#### Scenario: Canonical keys
- **WHEN** all four canonical keys contain non-empty values
- **THEN** the verifier uses them

#### Scenario: Consistent aliases
- **WHEN** a canonical key and its alias are both present with equal values
- **THEN** the verifier accepts the setting without disclosing its value

#### Scenario: Conflicting aliases
- **WHEN** a canonical key and its alias differ
- **THEN** verification fails before any network request and names only the conflicting setting

### Requirement: Destination is restricted to the official HTTPS API
The verifier SHALL send credentials only to an HTTPS base URL whose normalized host is `api.artifactsmmo.com`. User information, fragments, and non-default ports SHALL be rejected before any network request.

#### Scenario: Official API URL
- **WHEN** the configured base URL uses HTTPS and host `api.artifactsmmo.com` with no user information, fragment, or non-default port
- **THEN** destination validation succeeds

#### Scenario: Unapproved destination
- **WHEN** the configured URL uses HTTP, another host, embedded user information, a fragment, or a non-default port
- **THEN** verification fails before credentials are used

### Requirement: Verification is read-only
The verifier SHALL perform only authentication through `POST /token` and selected GET requests for the configured character and public reference data. It MUST NOT invoke any `/my/{name}/action/*` endpoint or any other state-changing game operation. Automatic redirects SHALL be disabled for every verifier request. Any 3xx response SHALL fail that operation without a follow-up request.

#### Scenario: Successful read-only smoke
- **WHEN** credentials are valid and the server returns compatible responses
- **THEN** the verifier authenticates, reads the configured character, reads one page each of maps, resources, items, and monsters, and reports a non-sensitive success summary

#### Scenario: Request construction audit
- **WHEN** the real-server suite is reviewed or tested with a recording handler
- **THEN** every request after authentication uses GET and no path contains `/action/`

#### Scenario: Redirect response
- **WHEN** token, character, or catalog verification receives a 3xx response
- **THEN** that operation fails and no redirected follow-up request is sent

### Requirement: Secrets remain confidential
The verifier SHALL NOT print or persist usernames, passwords, tokens, authorization headers, complete `.env` contents, or raw response bodies. Failures SHALL report the operation and non-sensitive status information only.

#### Scenario: Authentication failure
- **WHEN** the token request is rejected
- **THEN** output reports that authentication failed and the HTTP status without including credentials, token material, authorization headers, or the response body

#### Scenario: Contract failure
- **WHEN** a read response cannot be parsed into the selected local contract
- **THEN** output identifies the operation and contract type without dumping the raw payload

### Requirement: Credential files remain outside version control
The repository SHALL continue to ignore `.env`, and the real-server project SHALL contain no fallback credentials or copied secret values.

#### Scenario: Repository inspection
- **WHEN** the change is ready for review
- **THEN** `.env` is untracked and ignored, and a secret scan of the diff finds no credential values
