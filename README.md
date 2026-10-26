# gh-app (.NET / C#)

A C#/.NET 10 application that authenticates commits and pushes as a GitHub App
bot, with no webhook, no server, and no `GH_TOKEN` in the environment.
Documentation, CLI messages, and source comments are in en-US.

## What it does

- Generates an RS256 JWT from the GitHub App private key.
- Creates installation tokens on demand.
- Discovers the bot identity (`slug`, `login`, `id`, `email`) through `GET /app`
  and `GET /users/<slug>[bot]`, without using `GET /user`.
- Provides a Git credential helper (`get`, `store`, `erase`).
- Configures `user.name`, `user.email`, and a local credential helper per
  repository.
- Generates a `gitconfig` with one helper per installation, a cache, and an
  SSH-to-HTTPS redirect.
- Supports GitHub.com and GitHub Enterprise Server through `-domain`.
- Supports Windows and Linux.
- Validates the exact repository in the installation before `setup`.

## What it does not do

- It does not run a webhook or HTTP server.
- It does not read `GH_TOKEN`, `GITHUB_TOKEN`, or similar variables.
- It does not write installation tokens to disk.
- It does not commit, push, or modify the remote by itself.
- It does not sign commits (GitHub's "Verified" badge requires GPG/SSH signing).
- It does not authenticate over SSH; installation tokens work only over HTTPS.

## Requirements

- Git.
- .NET SDK 10 to build. Tested with SDK `10.0.403` and runtime `10.0.14`.
- A GitHub App installed on an account or organization, with access to the
  repositories.
- App permissions:
  - **Contents**: Read and write.
  - **Metadata**: Read-only (required).
  - **Workflows**: Read and write, if pushing `.github/workflows`.
- The app private key (`.pem`).

The published binary is **self-contained single-file**: the machine that only
runs it does not need the .NET runtime. The build machine needs the SDK.

## Step-by-step guide

### 1. Where files should live

Recommended layout:

| Item | Windows | Linux |
|---|---|---|
| Binary | `%LOCALAPPDATA%\Programs\gh-app\gh-app.exe` | `~/.local/bin/gh-app` |
| Config | `%APPDATA%\gh-app\config.json` | `~/.config/gh-app/config.json` |
| Private key | `%APPDATA%\gh-app\private-key.pem` | `~/.config/gh-app/private-key.pem` |

Rules:

- Keep the binary at a stable path. `setup` writes its absolute path into the
  Git credential helper.
- If you move the binary or the key, run `setup` again for each repository.
- Keep the config and key outside repositories.
- Never commit the private key or `config.json`.
- Portable alternative: keep `gh-app.exe`, `config.json`, and
  `private-key.pem` in the same folder. The app finds `config.json` next to the
  executable, and `privateKeyPath` can be relative, for example
  `private-key.pem`.

### 2. Install the binary

Windows:

```powershell
New-Item -ItemType Directory -Force "$env:LOCALAPPDATA\Programs\gh-app" | Out-Null
Copy-Item .\bin\gh-app.exe "$env:LOCALAPPDATA\Programs\gh-app\gh-app.exe" -Force
```

Linux:

```bash
mkdir -p ~/.local/bin
install -m 755 bin/gh-app-linux-amd64 ~/.local/bin/gh-app
```

Optionally, add the folder to `PATH`.

The project `bin/` folder is only a build output. For daily use, copy the binary
to the stable path above. If you are using a downloaded binary, copy the
downloaded file to the same destination.

Windows (reopen the terminal afterward):

```powershell
$current = [Environment]::GetEnvironmentVariable("Path", "User")
[Environment]::SetEnvironmentVariable(
  "Path",
  "$current;$env:LOCALAPPDATA\Programs\gh-app",
  "User"
)
```

Linux:

```bash
echo 'export PATH="$HOME/.local/bin:$PATH"' >> ~/.bashrc
source ~/.bashrc
```

Without `PATH`, use the full executable path in every command.

### 3. Create the GitHub App and store the key

On GitHub:

1. Create or open the GitHub App.
2. Minimum permissions:
   - **Contents**: Read and write.
   - **Metadata**: Read-only.
   - **Workflows**: Read and write, if changing `.github/workflows`.
3. Install the app on the account or organization and select the repositories.
4. Note the **App ID** and **Installation ID**.
5. Generate and download the private key (`.pem`).

Windows:

```powershell
New-Item -ItemType Directory -Force "$env:APPDATA\gh-app" | Out-Null
Move-Item "$env:USERPROFILE\Downloads\private-key.pem" `
  "$env:APPDATA\gh-app\private-key.pem" -Force
icacls "$env:APPDATA\gh-app\private-key.pem" /inheritance:r `
  /grant:r "$($env:USERNAME):(R)"
```

Linux:

```bash
mkdir -p ~/.config/gh-app
install -m 600 ~/Downloads/private-key.pem ~/.config/gh-app/private-key.pem
```

### 4. Create `config.json`

Windows: save it as `%APPDATA%\gh-app\config.json`.

```json
{
  "appId": 123456,
  "installationId": 12345678,
  "privateKeyPath": "~/AppData/Roaming/gh-app/private-key.pem",
  "username": "x-access-token",
  "domain": "github.com"
}
```

Linux: save it as `~/.config/gh-app/config.json`.

```json
{
  "appId": 123456,
  "installationId": 12345678,
  "privateKeyPath": "~/.config/gh-app/private-key.pem",
  "username": "x-access-token",
  "domain": "github.com"
}
```

Replace `123456` and `12345678` with the real IDs. If you use an organization
instead of an installation ID, replace `installationId` with
`"organization": "my-org"`.

### 5. Validate authentication

With the binary on `PATH`:

```powershell
gh-app identity --verify
```

Without `PATH`, on Windows:

```powershell
& "$env:LOCALAPPDATA\Programs\gh-app\gh-app.exe" identity --verify
```

Linux:

```bash
~/.local/bin/gh-app identity --verify
```

The expected result is JSON containing `slug`, `login`, `id`, `email`, and
`repositories`.

### 6. Configure a repository

The `origin` remote must use HTTPS.

```powershell
gh-app setup C:\workspace\repos\my-repo
```

Or on Linux:

```bash
gh-app setup ~/repos/my-repo
```

`setup` writes the following to the repository:

- the bot `user.name` and `user.email`;
- a local credential helper;
- a reset of local helpers so the GCM does not interfere.

It **does not** commit or push.

### 7. Confirm commit identity

```powershell
git -C C:\workspace\repos\my-repo var GIT_AUTHOR_IDENT
git -C C:\workspace\repos\my-repo var GIT_COMMITTER_IDENT
```

The name should be `<slug>[bot]`, and the email should be
`<id>+<slug>[bot]@users.noreply.github.com`.

### 8. Commit and push manually

```powershell
git -C C:\workspace\repos\my-repo commit --allow-empty -m "bot test"
git -C C:\workspace\repos\my-repo push
```

### 9. Optional global configuration

To use the app across multiple repositories:

```powershell
gh-app generate -output "$env:APPDATA\gh-app\gh-app.gitconfig"
git config --global include.path "$env:APPDATA\gh-app\gh-app.gitconfig"
```

If the GCM opens a dialog, prefer the per-repository `setup`.

### 10. GitHub Enterprise Server

```powershell
gh-app setup -domain ghe.example.com -config C:\path\config.json C:\repo
```

The remote must be `https://ghe.example.com/owner/repo`.

## Architecture

```text
.gh-app-dotnet/
  GhApp.slnx
  global.json
  dotnet-tools.json
  src/GhApp/                 console app
    Cli/                     parser, dispatcher, help, version
    Config/                  config.json, flexible IDs, path expansion
    GitHub/                  domain/GHE, JWT, API, identity, tokens
    Credential/              credential helper protocol
    Git/                     git execution and remote validation
    Generate/                gitconfig generation
    Setup/                   local repository configuration
    Helper/                  helper command quoting and executable resolution
    Services/                application services
  tests/GhApp.Tests/         xUnit v3 tests
  build.ps1
  build.sh
  README.md
  FEATURES.md
  config.example.json
  .gitignore
```

The app uses `System.Security.Cryptography` to sign the JWT, `HttpClient` for
the API, and `System.Text.Json` for config/responses. Dependencies:

- `Microsoft.Extensions.DependencyInjection` for service composition.
- `xUnit v3` with Microsoft Testing Platform (`xunit.v3.mtp-v2` 4.0.2).
- `CSharpier` during development.

Main interfaces:

- `IGhAppService` / `GhAppService` - identity, token, setup, and generate.
- `IGitRunner` / `GitRunner`
- `IRepositorySetup` / `RepositorySetup`
- `IGitHubAppClientFactory` / `GitHubAppClientFactory`
- `IExecutableResolver` / `ExecutableResolver`

This allows the CLI, setup, and services to be tested with fakes, without
network access and without a real Git repository when necessary.

## CSharpier

The project uses CSharpier as a local tool, configured in `dotnet-tools.json`
(version 1.3.0).

```powershell
dotnet tool restore
dotnet csharpier format .
dotnet csharpier check .
```

The build scripts run `dotnet tool restore` and `dotnet csharpier check .`
before the tests. If the check fails:

```powershell
dotnet csharpier format .
```

## Code coverage

The project does not include coverlet by default. `coverlet.collector` is the
VSTest integration and does not work with xUnit v3/MTP on .NET 10. Its latest
version is 10.1.0, but it remains incompatible with that model.

If coverage is required, use `coverlet.MTP` 10.1.0:

```powershell
dotnet add tests/GhApp.Tests package coverlet.MTP --version 10.1.0
dotnet test --solution GhApp.slnx -- --coverlet
```

Coverage is optional; the build and tests do not depend on it.

## Build

Windows:

```powershell
.\build.ps1
```

If the execution policy blocks scripts:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

To use a specific `dotnet`:

```powershell
.\build.ps1 -Dotnet "C:\Program Files\dotnet\dotnet.exe"
```

Linux:

```bash
bash build.sh
```

Or with a specific runtime:

```bash
DOTNET=/path/to/dotnet bash build.sh
```

The script:

1. Runs `dotnet tool restore`.
2. Runs `dotnet csharpier check .`.
3. Runs the tests.
4. Publishes self-contained single-file builds for:
   - `win-x64`
   - `linux-x64`
   - `linux-arm64`
5. Copies the binaries to:
   - `bin/gh-app.exe`
   - `bin/gh-app-linux-amd64`
   - `bin/gh-app-linux-arm64`

Approximate sizes: 74 MB (Windows), 74 MB (Linux x64), and 81 MB (Linux arm64),
because the .NET runtime is embedded. `artifacts/` contains intermediate outputs
and is ignored by Git.

Manual publish for one RID:

```powershell
dotnet publish src/GhApp/GhApp.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:Version=1.0.0 -o artifacts/win-x64
```

There is no macOS cross-build in this script; you can add the RIDs
(`osx-x64`, `osx-arm64`) if needed, but they have not been tested.

## Configuration

Example in `config.example.json`:

```json
{
  "appId": 123456,
  "installationId": 12345678,
  "privateKeyPath": "~/.config/gh-app/private-key.pem",
  "username": "x-access-token",
  "domain": "github.com"
}
```

On Windows, the recommended key path is
`~/AppData/Roaming/gh-app/private-key.pem`; on Linux, it is
`~/.config/gh-app/private-key.pem`.

Fields:

| Field | Required | Description |
|---|---|---|
| `appId` | yes | GitHub App ID (string or number). |
| `installationId` | one of two | Installation ID. |
| `organization` | one of two | Organization used to discover the installation. |
| `privateKeyPath` | yes | Key path. Relative to the config or absolute; `~` is expanded. |
| `privateKeyFile` | alias | Alternative name accepted for `privateKeyPath`. |
| `username` | no | Credential username. Default: `x-access-token`. |
| `domain` | no | GHE domain. Default: `github.com`. |

`installationId` takes precedence over `organization`. To use `organization`,
do not set an `installationId`, or pass `-installationId 0` with
`-organization`.

### Search order

1. `-config PATH`
2. `GH_APP_CONFIG`
3. `config.json` next to the executable
4. `config.json` one level above the executable
5. user config (`%AppData%\gh-app\config.json` on Windows,
   `~/.config/gh-app/config.json` on Linux)

Flags override the config. Any combination of `-appId`, `-installationId`,
`-organization`, `-privateKeyFile`, `-username`, and `-domain` can be used
without a config file.

The installed binary automatically finds:

- Windows: `%APPDATA%\gh-app\config.json`;
- Linux: `~/.config/gh-app/config.json`.

With `dotnet run`, the process is the `dotnet` host, so pass
`-config`/`GH_APP_CONFIG` explicitly.

The `config.json` in this project root is only a local development
configuration and is in `.gitignore`. For a normal installation, use the paths
in the guide table.

## Commands

### `identity`

```powershell
gh-app identity
```

```json
{"slug":"orih-agent","login":"orih-agent[bot]","id":347995100,"email":"347995100+orih-agent[bot]@users.noreply.github.com"}
```

With `--verify`, it creates a token and queries `/installation/repositories` to
count the repositories. The token is not printed:

```powershell
gh-app identity --verify
```

### `token`

Prints only the installation token, without a newline:

```powershell
gh-app token
```

Use with care: any redirection may write the token to disk.

### `credential`

```powershell
gh-app credential get
gh-app credential store
gh-app credential erase
gh-app credential generate
```

- `get`: reads the request from stdin, checks the host/protocol, and returns
  `username`, `password`, and `password_expiry_utc`.
- `store` / `erase`: no-op, exit 0; they drain stdin when it comes from a pipe.
- Different host or protocol: exits without a token and without output.
- Error: `quit=1` on stdout and a message on stderr.
- Flags before or after the operation:

```powershell
gh-app credential get --config C:\path\config.json
gh-app credential --config C:\path\config.json get
```

An alternative flag form also works:

```powershell
gh-app -config C:\path\config.json get
```

### `generate`

Generates a gitconfig with:

- one helper per installation;
- `useHttpPath = true`;
- a 43200-second cache;
- a redirect from `git@host:` and `ssh://git@host/` to HTTPS;
- the absolute key path;
- `-domain` in the helper for GHE.

```powershell
gh-app generate -output gh-app.gitconfig
```

`-output` writes UTF-8 without a BOM. Use `-output` on Windows to avoid the BOM
from PowerShell 5.1 `>` redirection.

Options:

- `-output PATH`
- `-helper-name NAME`: binary name/path used in the helper. Default: current
  executable (or `dotnet <dll>` when running through `dotnet run`).

Example output:

```gitconfig
[credential "https://github.com/my-org"]
	useHttpPath = true
	helper = "!C:/path/gh-app.exe credential -appId 123456 -installationId 12345678 -privateKeyFile C:/path/pk.pem -username x-access-token"
[credential "https://github.com"]
	helper = "cache --timeout=43200"
[url "https://github.com"]
	insteadOf = git@github.com:
	insteadOf = ssh://git@github.com/
```

For global use:

```powershell
git config --global include.path "C:/path/gh-app.gitconfig"
```

If the GCM or another global helper opens a dialog first, prefer the
per-repository `setup` (it resets only the local config), or deliberately reset
the global helpers:

```powershell
git config --global credential.helper ""
```

### `setup`

```powershell
gh-app setup --config config.json C:\workspace\repos\my-repo
gh-app setup C:\workspace\repos\my-repo --config config.json
```

The command:

1. Validates that the path is a Git repository.
2. Requires an HTTPS `origin` remote on the configured domain.
3. Confirms that the exact repository is in the installation.
4. Writes local `user.name` and `user.email`.
5. Resets inherited `credential.helper` and URL-specific helpers and adds this
   app's helper.
6. Prints test commands, but **does not execute** commit or push.

The written helper contains the app ID, installation ID, key path, and username;
it does not contain a token. If you move the binary or key, run `setup` again.

### `version` and `help`

```powershell
gh-app version
gh-app version --verbose
gh-app help
```

## Using with OpenCode

After `setup`, no OpenCode configuration is required: OpenCode calls `git`, and
the repository's local config resolves identity and the credential helper.

Recommended `AGENTS.md` entry:

```md
## Git

- This repository commits as the GitHub App `your-app[bot]`.
- Do not change `user.name`/`user.email` or the credential helper.
- Never put a token in the remote URL.
- If you need the GitHub API, ask the local app for a token; do not write it to
  a file.
```

## GitHub Enterprise Server (GHE)

```powershell
gh-app setup -domain ghe.example.com -config config.json C:\repo
gh-app generate -domain ghe.example.com -output gh-app.gitconfig
```

- API: `https://ghe.example.com/api/v3`
- Web: `https://ghe.example.com`
- Email: `<id>+<slug>[bot]@users.noreply.<domain>`

The remote must be `https://ghe.example.com/owner/repo`. For GHE on a custom
port, use `-domain ghe.example.com:8443` and the same port in the remote.

The noreply email format may vary by GHE version/configuration. If commits are
not attributed to the bot, adjust `user.email` manually.

With a private CA, install the certificate in the system trust store. The app
uses standard .NET TLS validation and offers no way to disable verification.

## Security

- The private key is the main secret: use `chmod 600` on Linux and a restricted
  ACL on Windows.
- The app does not persist installation tokens on disk.
- `generate` may use `git-credential-cache`, which keeps tokens in memory; clear
  it with `git credential-cache exit`.
- The helper only responds to HTTPS on the configured host (port 443 or the
  explicit GHE domain port).
- Errors do not print tokens. The `token` command prints by design.
- `config.json` and the helper contain the app ID, installation ID, and key
  path; treat them as sensitive configuration, but not as tokens.

## Troubleshooting

### `dotnet` not found

The SDK may be at `C:\Program Files\dotnet\dotnet.exe` without being on `PATH`.
`build.ps1` detects that path; alternatively:

```powershell
.\build.ps1 -Dotnet "C:\Program Files\dotnet\dotnet.exe"
```

### `csharpier check` failed

```powershell
dotnet tool restore
dotnet csharpier format .
```

### `dotnet run` cannot find the config or generates an unexpected helper

Use the published binary in `bin/` for `setup`/`generate`, or pass
`-config`/`GH_APP_CONFIG`. `dotnet run` uses `dotnet <dll>` in the helper.

### `403 Resource not accessible by integration` on `GET /user`

Expected. Installation tokens cannot call `GET /user`. Identity is discovered
through `GET /app` + `GET /users/<slug>[bot]`.

### `404` or repository outside the installation

- Confirm the installation on the account or organization.
- Confirm that the repository was selected.
- Confirm `Contents` and `Metadata`.
- Confirm `installationId` or `organization`.

### SSH remote

Use HTTPS:

```powershell
git remote set-url origin https://github.com/owner/repo.git
```

`generate` adds SSH redirects, but `setup` requires HTTPS.

### GCM opening a dialog

`setup` resets the helpers in the local config. With global `generate` usage,
you may need `git config --global credential.helper ""`.

### `quit=1` from the credential helper

Check stderr: missing config, unreadable key, missing installation, denied
token, or a different host.

### Commit shows as "Unverified"

The commit is attributed to the bot through `user.name`/`user.email`, but it is
not signed.

### Push rejected by branch protection

Add the app to the ruleset/branch bypass list or work through a branch and pull
request.

### Invalid generated file in PowerShell

Use `-output`; PowerShell 5.1 writes `>` as UTF-16LE with a BOM.

### Linux binary is not executable

```bash
chmod +x gh-app-linux-amd64
```

### Large binary

It is self-contained. To reduce size, publish framework-dependent (requires the
.NET 10 runtime on the target machine) or use NativeAOT (build on each OS). The
default prioritizes not requiring an installed runtime.

## Tests and validation

```powershell
dotnet tool restore
dotnet csharpier check .
dotnet test --solution GhApp.slnx -c Release
```

`global.json` selects the **Microsoft.Testing.Platform (MTP)** runner for .NET
10 `dotnet test`.

- 86 xUnit v3 tests, covering config, JWT, fake API, credential, gitconfig,
  remote URL, parser, and setup with a temporary repository.
- Manual validation against the real app: `identity`, `identity --verify`,
  `token`, `credential get`, `generate`, `setup`, `git credential fill`, and
  `git ls-remote`.
- Published Windows and Linux x64 on WSL.
- **No real commit or push was executed.**

## Detailed features

See `FEATURES.md`.
