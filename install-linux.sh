#!/usr/bin/env bash
set -euo pipefail

umask 077

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
prefix="${HOME}/.local/bin"
config_dir="${XDG_CONFIG_HOME:-${HOME}/.config}/gh-app"
binary="${script_dir}/bin/gh-app-linux-amd64"
app_id=""
installation_id=""
organization=""
domain="github.com"
username="x-access-token"
key_source=""
repo=""
global_helper=0
verify=0
force=0

usage() {
    cat <<'EOF'
Usage: ./install-linux.sh [options]

Install gh-app and configure a user-level GitHub App credential helper.

Options:
  --binary PATH          Linux binary to install
                         (default: ./bin/gh-app-linux-amd64)
  --prefix DIR           Binary install directory
                         (default: ~/.local/bin)
  --config-dir DIR       Config directory
                         (default: $XDG_CONFIG_HOME/gh-app or ~/.config/gh-app)
  --app-id ID            GitHub App ID
  --installation-id ID   GitHub App installation ID
  --organization ORG     Organization used to discover the installation
  --key PATH             Source private key .pem (copied with mode 600)
  --domain DOMAIN        GitHub domain (default: github.com)
  --username NAME        Credential username (default: x-access-token)
  --repo PATH            Run gh-app setup for this repository after install
  --global-helper        Set credential.helper=gh-app globally, resetting
                         inherited helpers. Affects all repositories.
  --verify               Run gh-app identity --verify after install
  --force                Overwrite an existing config/key
  -h, --help             Show this help

If App ID, installation/org, or key are omitted, the script prompts for them
when stdin is an interactive terminal.

Environment:
  XDG_CONFIG_HOME        Used for the default config directory
EOF
}

fail() {
    printf 'error: %s\n' "$*" >&2
    exit 1
}

need_value() {
    [ "$#" -ge 2 ] && [ -n "$2" ] || fail "missing value for $1"
}

prompt() {
    local label="$1"
    local default_value="${2:-}"
    local value=""

    [ -t 0 ] || fail "missing $label (run interactively or pass a flag)"
    if [ -n "$default_value" ]; then
        read -r -p "$label [$default_value]: " value
    else
        read -r -p "$label: " value
    fi
    printf '%s' "${value:-$default_value}"
}

json_safe() {
    local value="$1"
    local label="$2"
    case "$value" in
        *'"'*|*'\'*|*$'\n'*|*$'\r'*)
            fail "$label contains an unsupported quote, backslash, or newline"
            ;;
    esac
}

while [ "$#" -gt 0 ]; do
    case "$1" in
        --binary)
            need_value "$@"
            binary="$2"
            shift 2
            ;;
        --prefix)
            need_value "$@"
            prefix="$2"
            shift 2
            ;;
        --config-dir)
            need_value "$@"
            config_dir="$2"
            shift 2
            ;;
        --app-id)
            need_value "$@"
            app_id="$2"
            shift 2
            ;;
        --installation-id)
            need_value "$@"
            installation_id="$2"
            shift 2
            ;;
        --organization)
            need_value "$@"
            organization="$2"
            shift 2
            ;;
        --key)
            need_value "$@"
            key_source="$2"
            shift 2
            ;;
        --domain)
            need_value "$@"
            domain="$2"
            shift 2
            ;;
        --username)
            need_value "$@"
            username="$2"
            shift 2
            ;;
        --repo)
            need_value "$@"
            repo="$2"
            shift 2
            ;;
        --global-helper)
            global_helper=1
            shift
            ;;
        --verify)
            verify=1
            shift
            ;;
        --force)
            force=1
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            fail "unknown option: $1"
            ;;
    esac
done

[ -f "$binary" ] || fail "binary not found: $binary"
[ -r "$binary" ] || fail "binary is not readable: $binary"

mkdir -p "$prefix" "$config_dir"
prefix="$(cd "$prefix" && pwd)"
config_dir="$(cd "$config_dir" && pwd)"
binary="$(cd "$(dirname "$binary")" && pwd)/$(basename "$binary")"
config_path="$config_dir/config.json"
target_key="$config_dir/private-key.pem"

if [ -z "$app_id" ]; then
    app_id="$(prompt 'GitHub App ID')"
fi
case "$app_id" in
    ''|*[!0-9]*) fail "App ID must be numeric" ;;
esac

if [ -z "$installation_id" ] && [ -z "$organization" ]; then
    installation_id="$(prompt 'Installation ID (leave blank to use organization)')"
    if [ -z "$installation_id" ]; then
        organization="$(prompt 'Organization')"
    fi
fi

if [ -n "$installation_id" ]; then
    case "$installation_id" in
        *[!0-9]*) fail "installation ID must be numeric" ;;
    esac
fi
if [ -z "$installation_id" ] && [ -z "$organization" ]; then
    fail "provide installation ID or organization"
fi

if [ -z "$key_source" ] && [ ! -f "$target_key" ]; then
    key_source="$(prompt 'Private key .pem path')"
fi

if [ -n "$key_source" ]; then
    [ -f "$key_source" ] || fail "private key not found: $key_source"
    [ -r "$key_source" ] || fail "private key is not readable: $key_source"
    key_source="$(cd "$(dirname "$key_source")" && pwd)/$(basename "$key_source")"
fi

json_safe "$domain" "domain"
case "$domain" in
    *[[:space:]]*) fail "domain cannot contain whitespace" ;;
esac
json_safe "$username" "username"
json_safe "$organization" "organization"
json_safe "$config_path" "config path"

if [ -e "$config_path" ] && [ "$force" -eq 0 ]; then
    fail "config already exists: $config_path (use --force to replace it)"
fi
if [ -e "$config_path" ] && [ "$force" -eq 1 ]; then
    backup="${config_path}.bak.$(date +%Y%m%d%H%M%S)"
    cp -p "$config_path" "$backup"
    printf 'Backed up existing config to %s\n' "$backup"
fi

if [ -n "$key_source" ] && [ "$key_source" != "$target_key" ]; then
    if [ -e "$target_key" ] && [ "$force" -eq 0 ]; then
        fail "key already exists: $target_key (use --force to replace it)"
    fi
    install -m 600 "$key_source" "$target_key"
elif [ -f "$target_key" ]; then
    chmod 600 "$target_key"
else
    fail "private key not found: $target_key"
fi

if [ "$binary" = "$prefix/gh-app" ]; then
    chmod 755 "$binary"
else
    install -m 755 "$binary" "$prefix/gh-app"
fi
ln -sf "$prefix/gh-app" "$prefix/git-credential-gh-app"

if [ -n "$installation_id" ]; then
    cat > "$config_path" <<EOF
{
  "appId": $app_id,
  "installationId": $installation_id,
  "privateKeyPath": "$target_key",
  "username": "$username",
  "domain": "$domain"
}
EOF
else
    cat > "$config_path" <<EOF
{
  "appId": $app_id,
  "organization": "$organization",
  "privateKeyPath": "$target_key",
  "username": "$username",
  "domain": "$domain"
}
EOF
fi
chmod 600 "$config_path"

if [ "$global_helper" -eq 1 ]; then
    command -v git >/dev/null 2>&1 || fail "git not found in PATH"
    git config --global --unset-all credential.helper || true
    git config --global --add credential.helper ''
    git config --global --add credential.helper gh-app
    printf 'Configured global credential.helper=gh-app (GCM/global helpers reset).\n'
fi

if [ -n "$repo" ]; then
    "$prefix/gh-app" setup --config "$config_path" "$repo"
fi

if [ "$verify" -eq 1 ]; then
    "$prefix/gh-app" identity --config "$config_path" --verify
fi

printf '\nInstalled:\n'
printf '  binary: %s\n' "$prefix/gh-app"
printf '  alias:  %s\n' "$prefix/git-credential-gh-app"
printf '  config: %s\n' "$config_path"
printf '  key:    %s\n' "$target_key"

case ":${PATH}:" in
    *":${prefix}:"*) ;;
    *)
        printf '\nAdd the binary directory to PATH for this shell:\n'
        printf '  export PATH="%s:$PATH"\n' "$prefix"
        printf 'To persist it, add that line to ~/.bashrc or ~/.zshrc.\n'
        ;;
esac

printf '\nNext steps:\n'
printf '  %s identity --config "%s" --verify\n' "$prefix/gh-app" "$config_path"
printf '  %s setup --config "%s" /path/to/repo\n' "$prefix/gh-app" "$config_path"
printf '\nNo commit or push was performed.\n'
