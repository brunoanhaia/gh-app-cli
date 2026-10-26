# Features

Features of the C#/.NET 10 `gh-app` implementation.

## CLI

- `identity`
- `identity --verify`
- `token`
- `credential get|store|erase|generate`
- `generate`
- `setup`
- `version` / `version --verbose`
- `help`
- Flags before or after the operation/repository.
- Alternative form: `gh-app -config ... get`.
- `-v`, `-version`, `--version`.
- Config through `-config`, `GH_APP_CONFIG`, or automatic discovery.
- App/installation IDs as strings or numbers.

## Authentication and identity

- RS256 JWT signed with the GitHub App private key.
- `iat` with a 60-second allowance and a 9-minute `exp`.
- Installation token on demand through
  `POST /app/installations/<id>/access_tokens`.
- Installation by `installationId` or `organization`.
- Identity through `GET /app` + `GET /users/<slug>[bot]`, without `GET /user`.
- Login `<slug>[bot]`.
- Email `<id>+<slug>[bot]@users.noreply.<domain>`.
- `identity --verify` counts repositories through
  `/installation/repositories`.
- GitHub.com and GitHub Enterprise Server (`/api/v3`).

## Credential helper

- `get`, `store`, `erase`.
- Reads `protocol`, `host`, `path`, and `url` from stdin.
- Responds only to HTTPS on the configured domain.
- Output includes `username`, `password`, and `password_expiry_utc`.
- Error output uses `quit=1` on stdout and details on stderr.
- Different host: no output and no token.
- `store`/`erase` drain the pipe and exit 0.
- Port validation: 443 for a domain without a port, explicit port for GHE.

## Gitconfig generation

- One context per installation.
- `useHttpPath = true`.
- Helper includes app ID, installation ID, absolute key path, username, and GHE
  domain.
- 43200-second cache.
- `git@host:` and `ssh://git@host/` to HTTPS redirect.
- `-output` writes UTF-8 without a BOM.
- `-helper-name` allows using `gh-app` from `PATH`.

## Repository setup

- Validates that the path is a Git repository.
- Requires an HTTPS `origin` remote on the configured domain.
- Rejects SSH, HTTP, credentials in the URL, query, fragment, and extra path
  segments.
- Confirms the exact repository in the installation, with pagination.
- Writes local `user.name` and `user.email`.
- Resets inherited general and URL-specific `credential.helper` values.
- Adds this app's local helper.
- Does not commit or push.

## Configuration and storage

- JSON config with `appId`, `installationId`/`organization`, `privateKeyPath`,
  `username`, and `domain`.
- `privateKeyFile` accepted as an alias.
- `privateKeyPath` relative to the config or absolute; `~` is expanded.
- Config discovery:
  1. `-config`
  2. `GH_APP_CONFIG`
  3. next to the executable
  4. one level above the executable
  5. user config (`%AppData%\gh-app` on Windows, `~/.config/gh-app` on Linux)
- The key is never written to the config.
- `config.json` and the key are ignored by Git.
- The helper stores the absolute binary path; moving the binary/key requires
  running setup again.

## Build and quality

- .NET 10, `net10.0`, nullable enabled, and warnings as errors in the app.
- xUnit v3 with MTP (`xunit.v3.mtp-v2` 4.0.2) and 86 tests.
- `global.json` enables Microsoft.Testing.Platform for `dotnet test`.
- CSharpier 1.3.0 as a local tool.
- `build.ps1`/`build.sh` run tool restore, CSharpier check, tests, and publish
  self-contained single-file builds.
- RIDs: `win-x64`, `linux-x64`, `linux-arm64`.
- `InvariantGlobalization` to reduce the ICU dependency on Linux.
- Coverage is not included by default; if needed, use `coverlet.MTP` 10.1.0.

## Out of scope

- Webhook, HTTP server, or callback.
- Tokens persisted to disk.
- Commit signing (GPG/SSH).
- SSH authentication.
- Automatic app creation/installation.
- Proactive background token renewal.
- Multiple installations in a single helper invocation; `generate` covers
  multiple accounts.
- GUI/MAUI.
- macOS build/test on this machine.
- NativeAOT; the default is self-contained single-file.

## Validation

- `dotnet test --solution GhApp.slnx -c Release`: 86 tests passed.
- `dotnet csharpier check .`: passed.
- `identity`, `identity --verify`, `token`, and `credential get` against the real
  GitHub API.
- `generate -output` and use of the generated file.
- `setup` in a temporary repository on Windows and Linux (WSL).
- `git credential fill`, `git var GIT_AUTHOR_IDENT`, and `git ls-remote`.
- `win-x64` and `linux-x64` binaries tested; `linux-arm64` compiled but not
  executed.
- No real commit or push was executed.

## Limitations

- Self-contained binaries are large (~74-81 MB).
- The .NET 10 SDK is required to build.
- The .NET 10 runtime is not required to run the self-contained binary.
- GHE in private mode may block the public lookup of the bot user; in that case
  the identity may need manual adjustment.
- The GHE noreply email format may vary.
- `setup` fails if the remote is not HTTPS; this is intentional.
- The Git cache keeps tokens in memory; clear it with
  `git credential-cache exit`.
