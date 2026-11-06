#!/usr/bin/env bash
#
# Deploy the MCP server to its host from a workstation that has the SSH key.
#
# Building here and shipping the artifacts keeps the server free of any source checkout or
# build toolchain: the server is framework-dependent (--self-contained false) and the host
# already runs dotnet, so the payload is small. Nothing on the host updates itself; every
# change reaches production through this script.
#
# What it does, in order: build and publish locally, sanity-check the payload, back up the
# live install, unpack the payload into a staging directory beside it, swap it in, restart,
# verify /health — and restore the backup if anything fails from the swap onwards. Nothing
# that is running is touched until the payload has arrived intact.
#
# Usage:
#   bash deploy/deploy-from-workstation.sh --host HOST --key PATH [--dry-run]
#   (or MCP_HOST / MCP_SSH_KEY in the environment; both are required)
#
# Exit codes:
#   0  deployed and healthy
#   1  refused or failed before the swap — the live install was never touched
#   2  the swap or the health check failed; the backup was restored and the service is up
#   3  the same, but the rollback could not be confirmed — the box needs a look, now
#
# Errors and Ctrl-C are trapped throughout, so those four codes cover the ordinary cases.
# Any other code means the script was killed in a way it could not handle (SIGKILL, say);
# if that happened during the swap the state of the live install is unknown — treat it
# like 3.

set -euo pipefail

# Everything before the swap must exit 1, the code that means "the live install was never
# touched". Without this a dropped ssh during the backup or the transfer propagates 255 —
# and the caller renders any unexpected code as "state UNKNOWN, check the droplet", turning
# the one failure that is provably harmless into a false emergency. The swap replaces this
# trap with the one that restores the backup.
#
# INT and TERM too: publish and the transfer are minutes of pre-swap work, and a Ctrl-C
# there would otherwise exit 130 — which the caller paints red as "state unconfirmed" for
# a run that provably shipped nothing.
trap 'exit 1' ERR INT TERM

HOST="${MCP_HOST:-}"
KEY="${MCP_SSH_KEY:-}"
DRY_RUN=0
APP_DIR="/opt/visioforge-mcp"
SERVICE="visioforge-mcp"
PUBLIC_URL="https://mcp.visioforge.com"
KEEP_BACKUPS=5

while [ $# -gt 0 ]; do
    case "$1" in
        --key)      KEY="$2"; shift 2 ;;
        --host)     HOST="$2"; shift 2 ;;
        --dry-run)  DRY_RUN=1; shift ;;
        -h|--help)  sed -n '2,/^[^#]/p' "$0" | sed '$d'; exit 0 ;;
        *)          echo "Unknown argument: $1" >&2; exit 1 ;;
    esac
done

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MCP_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
SERVER_DIR="$MCP_DIR/VisioForge.MCP.Server"
PUBLISH_DIR="$MCP_DIR/publish"
INDEX="$SERVER_DIR/Data/api-index.json"

say()  { printf '\n=== %s\n' "$1"; }
die()  { printf 'ERROR: %s\n' "$1" >&2; exit 1; }

# Put the backup back. Used both by the health check and by the ERR trap armed around the
# swap: anything that fails once the live install is being replaced must restore it rather
# than leave an empty directory and a dead unit. Before 2026-09-01 only the health path
# rolled back at all, and it exited 1 while the header promised 2.
restore() {
    say "$1 — rolling back"
    # The failure this exists to survive is ssh dropping mid-swap — and that is exactly the
    # case where restore's own ssh cannot reach the box either. Swallowing that and exiting 2
    # would report a clean rollback for a droplet left empty with a dead unit, so the two
    # outcomes get different exit codes and the caller is told which one it got.
    if "${SSH[@]}" "set -e
        test -d '$APP_DIR.bak-$STAMP'
        systemctl stop $SERVICE 2>/dev/null || true
        rm -rf '$APP_DIR' '$APP_DIR'.new-*
        mv '$APP_DIR.bak-$STAMP' '$APP_DIR'
        systemctl start $SERVICE
        # Poll rather than one sleep: a .NET service that needs four seconds is a slow
        # start, not an unconfirmed rollback, and this is the only thing separating
        # exit 2 from the exit 3 the caller renders as an emergency.
        for _ in \$(seq 1 15); do
            systemctl is-active --quiet $SERVICE && exit 0
            sleep 1
        done
        exit 1"; then
        printf 'ERROR: %s Rolled back to the backup; the service is running.\n' "$1" >&2
        exit 2
    fi
    printf 'ERROR: %s ROLLBACK COULD NOT BE CONFIRMED.\n' "$1" >&2
    printf '  The backup was at %s.bak-%s on %s — it may already have been moved back,\n' "$APP_DIR" "$STAMP" "$HOST" >&2
    printf '  so the listing below is what tells you which. Check the box now:\n' >&2
    printf '    ssh -i <key> root@%s "ls -d %s* ; systemctl status %s"\n' "$HOST" "$APP_DIR" "$SERVICE" >&2
    exit 3
}

[ -n "$HOST" ] || die "No host. Pass --host HOST or set MCP_HOST."
[ -n "$KEY" ] && [ -f "$KEY" ] || die "SSH key not found. Pass --key PATH or set MCP_SSH_KEY."

# On Windows, prefer the Windows OpenSSH client. The key is passphrase-protected and lives in
# the Windows ssh-agent; Git for Windows' ssh cannot see that agent and, under BatchMode,
# cannot ask for the passphrase, so the droplet answers "Permission denied (publickey)".
SSH_BIN=ssh
[ -x /c/Windows/System32/OpenSSH/ssh.exe ] && SSH_BIN=/c/Windows/System32/OpenSSH/ssh.exe

SSH=("$SSH_BIN" -i "$KEY" -o BatchMode=yes -o ConnectTimeout=15 "root@$HOST")

say "Preflight"
command -v dotnet >/dev/null || die "dotnet is not on PATH."
command -v tar    >/dev/null || die "tar is not on PATH."
[ -f "$INDEX" ] || die "$INDEX is missing — this is not an mcp-server checkout."
# What ships is the working tree, so it must be a pushed commit: production should never run
# code, or an index, that is not in the repository.
DIRTY="$(git -C "$MCP_DIR" status --porcelain)" || die "$MCP_DIR is not a git checkout."
[ -z "$DIRTY" ] || die "uncommitted changes in $MCP_DIR — commit and push them first:
$DIRTY"
AHEAD="$(git -C "$MCP_DIR" log --oneline '@{u}..HEAD' 2>/dev/null)" || die "no upstream for the current branch in $MCP_DIR — push it first."
[ -z "$AHEAD" ] || die "unpushed commits in $MCP_DIR — push them first:
$AHEAD"
"${SSH[@]}" true 2>/dev/null || die "cannot reach root@$HOST with $KEY."
echo "  host $HOST reachable, key $KEY"

# The index ships from the repo: the droplet cannot rebuild it (VisioForge.Core.xml is
# gitignored and Core is not built there). Shipping one without guides, or one still
# stamped with a long-dead SDK version, is the failure this whole exercise was about.
say "Checking the api-index.json about to ship"
python3 - "$INDEX" <<'PY' || die "the index in this checkout is not fit to deploy."
import json, sys, datetime
d = json.load(open(sys.argv[1]))
guides = len(d.get("deploymentGuides", []))
version = d.get("sdkVersion", "?")
generated = d.get("generatedAt", "?")
core = sum(1 for c in d.get("classes", []) if c.get("isCoreSDK"))
print(f"  sdkVersion {version}, generated {generated[:10]}, {guides} guides, {core} core classes")
ok = True
if guides == 0:
    print("  !! no deployment guides — list_deployment_guides would answer 'not found' for all of them"); ok = False
if version.startswith("2026.2.") or version == "?":
    print(f"  !! sdkVersion {version} looks like the frozen February index"); ok = False
try:
    age = (datetime.datetime.now(datetime.timezone.utc)
           - datetime.datetime.fromisoformat(generated.replace("Z", "+00:00"))).days
    print(f"  index is {age} days old")
    if age > 120:
        print("  !! older than 120 days — rebuild it first (see the mcp-deploy skill)"); ok = False
except Exception:
    print("  !! generatedAt does not parse"); ok = False
sys.exit(0 if ok else 1)
PY

# The transfer replaces appsettings.json wholesale, so a value hand-edited on the droplet
# and never carried back into the repo is silently overwritten. That is not theoretical:
# the first run of this script shipped the repo's "http://localhost:5000" over a live
# "http://localhost:5001", Kestrel tried to bind 5000 — which belongs to the activation
# server on that box — and the payload aborted on startup. The rollback caught it, but a
# preflight that reads the live file catches it a minute earlier and without a restart.
say "Comparing config with what is deployed"
LIVE_SETTINGS="$("${SSH[@]}" "cat $APP_DIR/appsettings.json 2>/dev/null" || true)"
if [ -n "$LIVE_SETTINGS" ]; then
    # Parsed, not byte-compared: indentation, key order and a missing trailing newline
    # are not configuration changes, and treating them as one would block every deploy.
    # Via a file, not a pipe: the heredoc below already owns stdin.
    LIVE_TMP="$(mktemp)"
    printf '%s' "$LIVE_SETTINGS" > "$LIVE_TMP"
    python3 - "$LIVE_TMP" "$SERVER_DIR/appsettings.json" <<'PY' || { rm -f "$LIVE_TMP"; die "the deploy would overwrite live configuration. Carry the droplet's value into the repo (or the other way round), then re-run."; }
import json, sys

def flatten(node, prefix=""):
    if isinstance(node, dict):
        for k, v in node.items():
            yield from flatten(v, f"{prefix}.{k}" if prefix else k)
    else:
        yield prefix, node

try:
    live = dict(flatten(json.load(open(sys.argv[1]))))
except Exception as exc:
    print(f"  could not parse the deployed appsettings.json: {exc}"); sys.exit(1)
mine = dict(flatten(json.load(open(sys.argv[2]))))

diffs = []
for key in sorted(set(live) | set(mine)):
    if live.get(key) != mine.get(key):
        diffs.append(f"    {key}: droplet {live.get(key)!r} -> shipping {mine.get(key)!r}")
if diffs:
    print("  appsettings.json DIFFERS from the deployed copy:")
    print("\n".join(diffs))
    sys.exit(1)
print("  appsettings.json matches the deployed copy")
PY
    rm -f "$LIVE_TMP"
else
    echo "  no appsettings.json deployed yet — nothing to compare"
fi

say "Building the server (Release, linux-x64, framework-dependent)"
if [ "$DRY_RUN" = "1" ]; then
    echo "  --dry-run: skipping build"
else
    rm -rf "$PUBLISH_DIR"
    dotnet publish "$SERVER_DIR" -c Release -o "$PUBLISH_DIR" \
        --runtime linux-x64 --self-contained false --nologo -v q
    [ -f "$PUBLISH_DIR/VisioForge.MCP.Server.dll" ] || die "publish produced no server dll."
    [ -f "$PUBLISH_DIR/Data/api-index.json" ] || die "publish produced no Data/api-index.json."
    [ -f "$PUBLISH_DIR/server.json" ] || die "publish produced no server.json."
    echo "  payload: $(du -sh "$PUBLISH_DIR" | cut -f1)"
fi

STAMP="$(date +%Y%m%d-%H%M%S)"
if [ "$DRY_RUN" = "1" ]; then
    say "Dry run — nothing was shipped"
    echo "  would back up $APP_DIR to $APP_DIR.bak-$STAMP, stage $PUBLISH_DIR beside it and swap"
    exit 0
fi

# Back up the live install beside itself before touching anything.
say "Backing up the live install"
"${SSH[@]}" "set -e
    cp -a '$APP_DIR' '$APP_DIR.bak-$STAMP'
    ls -1dt '$APP_DIR'.bak-* 2>/dev/null | tail -n +$((KEEP_BACKUPS + 1)) | xargs -r rm -rf
    # .new-* too: a swap that fails before the mv leaves a full payload copy behind, and
    # globbing only .bak-* meant those accumulated in /opt forever.
    rm -rf '$APP_DIR'.new-* 2>/dev/null || true
    echo \"  backed up to $APP_DIR.bak-$STAMP\"
    ls -1dt '$APP_DIR'.bak-* | sed 's/^/  keeping /'"

# tar over ssh rather than rsync: rsync is not present on the Windows box that runs
# deploy-help.ps1, and this is the same transfer the API-docs step of that script already
# uses against this droplet.
#
# It unpacks into a staging directory and swaps, rather than emptying the live one and
# unpacking into it. rsync --delete-after had no window; a pipe does — a dropped ssh, a
# local tar error or a full disk would have left an empty /opt/visioforge-mcp with the
# service already stopped, and the rollback only covered the health check. Now a failed
# transfer costs a staging directory and touches nothing that is running.
STAGE_DIR="$APP_DIR.new-$STAMP"
say "Shipping to $STAGE_DIR"
# COPYFILE_DISABLE and --no-xattrs: macOS bsdtar otherwise ships AppleDouble metadata
# that GNU tar on the droplet reports as unknown extended headers, once per file.
COPYFILE_DISABLE=1 tar --no-xattrs -czf - -C "$PUBLISH_DIR" . \
    | "${SSH[@]}" "rm -rf '$STAGE_DIR' && mkdir -p '$STAGE_DIR' && tar -xzf - -C '$STAGE_DIR'"
"${SSH[@]}" "test -f '$STAGE_DIR/VisioForge.MCP.Server.dll'" \
    || { "${SSH[@]}" "rm -rf '$STAGE_DIR'" || true; die "the payload did not arrive intact; nothing was swapped."; }

# From here the live install is being replaced, so every failure has to put it back.
trap 'restore "Deploy failed while swapping in the new install."' ERR INT TERM

say "Swapping"
"${SSH[@]}" "set -e
    systemctl stop $SERVICE
    rm -rf '$APP_DIR'
    mv '$STAGE_DIR' '$APP_DIR'
    chown -R www-data:www-data '$APP_DIR'
    chmod +x '$APP_DIR/VisioForge.MCP.Server'
    systemctl start $SERVICE
    sleep 3
    systemctl is-active --quiet $SERVICE && echo '  service is running' || { echo '  service did NOT start'; exit 1; }"

say "Verifying"
sleep 2
HEALTH="$("${SSH[@]}" "curl -sf --max-time 10 http://localhost:5000/health || curl -sf --max-time 10 http://localhost:5001/health" || true)"
echo "  local:  ${HEALTH:-<no response>}"

if ! printf '%s' "$HEALTH" | python3 -c '
import json, sys
try:
    d = json.load(sys.stdin)
except Exception:
    sys.exit(1)
sys.exit(0 if d.get("deploymentGuideCount", 0) > 0 and not str(d.get("sdkVersion", "")).startswith("2026.2.") else 1)
' 2>/dev/null; then
    restore "The payload started but did not report a usable index."
fi
# server.json problems only log a WARNING and 404 this route, so /health cannot see them.
WK="$("${SSH[@]}" "curl -sf --max-time 10 -o /dev/null http://localhost:5000/.well-known/mcp.json || curl -sf --max-time 10 -o /dev/null http://localhost:5001/.well-known/mcp.json" && echo ok || true)"
[ "$WK" = ok ] || restore "The payload started but /.well-known/mcp.json is not served (check server.json)."
# Back to the pre-swap contract rather than no trap at all: from here the new install is
# live and verified, so any late failure is "nothing more was touched", not an unknown.
trap 'exit 1' ERR INT TERM

echo "  public: $(curl -sf --max-time 15 "$PUBLIC_URL/health" || echo '<unreachable>')"
echo "  well-known: HTTP $(curl -s -o /dev/null -w '%{http_code}' --max-time 15 "$PUBLIC_URL/.well-known/mcp.json")"

say "Done"
echo "  Backup kept at $APP_DIR.bak-$STAMP on the host."
echo "  Re-run this script after any change that should reach production."
