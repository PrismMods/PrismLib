#!/usr/bin/env bash
# ./release.sh                      build + run the self-check
# ./release.sh 0.2.0 [notes.md]     also stamp the version everywhere, regenerate prismlib.json, and
#                                   offer to publish: commit, tag, GitHub release, then push main
#
# prismlib.json is the feed PrismBootstrap reads. Its sha256 is computed from the DLLs built RIGHT
# HERE, so publish the same files: upload both bin/Release DLLs to the tag's release, then push.
# A mismatch is not a broken install — the bootstrapper discards the download and every mod falls
# back to running standalone, silently. That is why main (which carries the feed) is pushed LAST,
# only after the uploaded assets are downloaded back and their hashes checked against the feed.
# raw.githubusercontent caches the feed for a few minutes, so a fresh release is not visible to
# clients (or to lib/update-prismlib.sh) straight away.
set -euo pipefail
cd "$(dirname "$0")"

REPO="PrismMods/PrismLib"
VER="${1:-}"
NOTES="${2:-}"

# Taken before stamping: publishing commits with `git add -A`, so anything already uncommitted
# would ride into the release commit unseen.
DIRTY=""
[ -n "$VER" ] && DIRTY=$(git status --porcelain)

if [ -n "$VER" ]; then
    # perl -pi, not sed -i '': sed's in-place flag takes its backup suffix differently on BSD
    # (macOS, needs the empty '' as a separate argument) and GNU (Linux, needs it attached, no
    # space) — one spelling breaks on the other. Perl's is identical on both.
    perl -pi -e "s/AssemblyVersion\(\"[0-9.]+\"\)/AssemblyVersion(\"$VER.0\")/; s/AssemblyFileVersion\(\"[0-9.]+\"\)/AssemblyFileVersion(\"$VER.0\")/" PrismLib/Properties/AssemblyInfo.cs
    perl -pi -e "s/new Version\([0-9]+, [0-9]+, [0-9]+\);/new Version(${VER//./, });/" PrismLib/Prism.cs
    perl -pi -e "s/AssemblyVersion\(\"[0-9.]+\"\)/AssemblyVersion(\"$VER.0\")/; s/AssemblyFileVersion\(\"[0-9.]+\"\)/AssemblyFileVersion(\"$VER.0\")/" PrismLib.UI/Properties/AssemblyInfo.cs
    echo "stamped $VER"
fi

xbuild /p:Configuration=Release /verbosity:minimal PrismLib/PrismLib.csproj
xbuild /p:Configuration=Release /verbosity:minimal PrismLib.UI/PrismLib.UI.csproj
DLL="PrismLib/bin/Release/PrismLib.dll"
UIDLL="PrismLib.UI/bin/Release/PrismLib.UI.dll"

mcs -r:"$DLL" -out:/tmp/prismlibtest.exe tools/PrismLibTest.cs
cp "$DLL" /tmp/
mono /tmp/prismlibtest.exe

# The bootstrapper drops it into a mod, so it must keep compiling on its own.
mcs -target:library -r:System.dll -out:/tmp/prismbootcheck.dll bootstrap/PrismBootstrap.cs
echo "bootstrap compiles standalone"

[ -n "$VER" ] || exit 0

SHA=$(shasum -a 256 "$DLL" | cut -d' ' -f1)
UISHA=$(shasum -a 256 "$UIDLL" | cut -d' ' -f1)
# The bootstrapper reads version/url/sha256 for the arbitration half, which it installs into a
# shared folder. The ui block is for lib/update-prismlib.sh: mods SHIP that one, so it is
# fetched at build time, not at runtime.
cat > prismlib.json <<JSON
{
    "version": "$VER",
    "url": "https://github.com/$REPO/releases/download/v$VER/PrismLib.dll",
    "sha256": "$SHA",
    "ui": {
        "url": "https://github.com/$REPO/releases/download/v$VER/PrismLib.UI.dll",
        "sha256": "$UISHA"
    }
}
JSON
echo "wrote prismlib.json ($SHA / ui $UISHA)"

ANSWER=n
if [ -t 0 ] || [ -r /dev/tty ]; then
    read -r -p "Publish v$VER now (commit, tag, GitHub release, push main)? [y/N] " ANSWER < /dev/tty || ANSWER=n
fi
case "$ANSWER" in
    [yY]|[yY][eE][sS]) ;;
    *)
        echo "Not published. The stamped files and prismlib.json are left uncommitted; do not rebuild"
        echo "before publishing by hand, or the feed's sha256 stops matching bin/Release."
        exit 0 ;;
esac

die() { echo "publish aborted: $*" >&2; exit 1; }

# Everything that can refuse is checked before the first write to the remote.
[ -z "$DIRTY" ] || die "the tree had uncommitted changes before this run — commit them first:
$DIRTY"
command -v gh >/dev/null || die "gh (GitHub CLI) is not installed"
gh auth status >/dev/null 2>&1 || die "gh is not logged in (gh auth login)"
[ "$(git rev-parse --abbrev-ref HEAD)" = main ] || die "not on main — clients read the feed from main"
git fetch -q origin main
git merge-base --is-ancestor origin/main HEAD || die "origin/main has commits this branch lacks — pull first"
git rev-parse -q --verify "refs/tags/v$VER" >/dev/null && die "tag v$VER already exists locally"
[ -z "$(git ls-remote --tags origin "refs/tags/v$VER")" ] || die "tag v$VER already exists on origin"
[ -z "$NOTES" ] || [ -f "$NOTES" ] || die "notes file not found: $NOTES"

git add -A
git commit -q -m "release: $VER"
git tag "v$VER"
git push -q origin "v$VER"
echo "pushed tag v$VER"

NOTE_ARGS=(--generate-notes)
[ -n "$NOTES" ] && NOTE_ARGS=(--notes-file "$NOTES")
gh release create "v$VER" "$DLL" "$UIDLL" --repo "$REPO" --verify-tag \
    --title "PrismLib $VER" "${NOTE_ARGS[@]}"

# Download what GitHub now serves and hold it against the feed before the feed goes live.
CHECK=$(mktemp -d)
trap 'rm -rf "$CHECK"' EXIT
gh release download "v$VER" --repo "$REPO" --dir "$CHECK" --pattern PrismLib.dll --pattern PrismLib.UI.dll
GOT=$(shasum -a 256 "$CHECK/PrismLib.dll" | cut -d' ' -f1)
UIGOT=$(shasum -a 256 "$CHECK/PrismLib.UI.dll" | cut -d' ' -f1)
if [ "$GOT" != "$SHA" ] || [ "$UIGOT" != "$UISHA" ]; then
    die "release assets do not match prismlib.json (want $SHA / $UISHA, got $GOT / $UIGOT).
main was NOT pushed, so no client sees v$VER. Fix the release assets, then: git push origin main"
fi
echo "release assets match prismlib.json"

git push -q origin HEAD:main
echo "pushed main — the feed now says $VER"

# raw.githubusercontent lags a few minutes; wait so a mod's lib/update-prismlib.sh run right after
# this gets the new version.
FEED="https://raw.githubusercontent.com/$REPO/main/prismlib.json"
for _ in $(seq 1 30); do
    if curl -fsS "$FEED" 2>/dev/null | grep -q "\"version\": \"$VER\""; then
        echo "feed is live: $FEED"
        echo "Next: ./lib/update-prismlib.sh in each mod (Sapphire, Bismuth)."
        exit 0
    fi
    sleep 10
done
echo "Published, but the cached feed still shows the old version after 5 minutes."
echo "It will catch up; run ./lib/update-prismlib.sh in each mod once it does."
