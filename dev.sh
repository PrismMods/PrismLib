#!/usr/bin/env bash
# Build and install straight into the game — no git tag, no GitHub release, no feed.
#
#   ./dev.sh
#
# The release path exists so END USERS get the library; it has no business in the edit-test loop.
# This drops PrismLib.dll where the bootstrapper would have installed it and PrismLib.UI.dll into
# every mod that ships it, then leaves the version alone. The bootstrapper only replaces its copy
# when the feed's version is strictly NEWER, so an unbumped dev build survives the next launch.
set -euo pipefail
cd "$(dirname "$0")"

# macOS, then Linux's two common Steam library locations — whichever exists. ADOFAI_ROOT
# overrides all of them, for anywhere else (Windows, a second library, ...).
if [ -z "${ADOFAI_ROOT:-}" ]; then
    for p in \
        "$HOME/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice" \
        "$HOME/.local/share/Steam/steamapps/common/A Dance of Fire and Ice" \
        "$HOME/.steam/steam/steamapps/common/A Dance of Fire and Ice"
    do [ -d "$p" ] && { ADOFAI_ROOT="$p"; break; }; done
    ADOFAI_ROOT="${ADOFAI_ROOT:-$HOME/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice}"
fi
GAME="$ADOFAI_ROOT"
# Prefer whichever folder actually HOLDS mods: some UMM installs carry a stale, empty UMMMods/
# alongside the Mods/ folder UMM is actually configured to read (seen on this machine — the
# game's own Player.log names "Mods" as the active path). Existence alone would silently install
# PrismLib where the game never looks for it.
UMM_N=0; [ -d "$GAME/UMMMods" ] && UMM_N=$(find "$GAME/UMMMods" -mindepth 1 -maxdepth 1 -type d | wc -l)
MODS_N=0; [ -d "$GAME/Mods" ]    && MODS_N=$(find "$GAME/Mods"    -mindepth 1 -maxdepth 1 -type d | wc -l)
if   [ "$UMM_N" -gt 0 ];  then MODS="$GAME/UMMMods"
elif [ "$MODS_N" -gt 0 ]; then MODS="$GAME/Mods"
elif [ -d "$GAME/UMMMods" ]; then MODS="$GAME/UMMMods"
elif [ -d "$GAME/Mods" ];    then MODS="$GAME/Mods"
else echo "ERROR: no UMMMods/ or Mods/ under $GAME (set ADOFAI_ROOT?)" >&2; exit 1
fi

# AdofaiRoot is the matching MSBuild property the csproj reads for its own default — xbuild does
# not see the shell's ADOFAI_ROOT on its own, so without this a Linux build silently fell back to
# the csproj's hardcoded macOS path and failed to resolve every UnityEngine.* reference.
FACET=$(sed -n 's:.*<Reference Include="\(Facet\.[0-9]*\.[0-9]*\)".*:\1:p' PrismLib.UI/PrismLib.UI.csproj)
[ -n "$FACET" ] || { echo "no Facet reference in PrismLib.UI/PrismLib.UI.csproj" >&2; exit 1; }
[ -f "lib/$FACET.dll" ] || tools/update-facet.sh
xbuild /p:Configuration=Release /p:AdofaiRoot="$GAME" /verbosity:quiet PrismLib/PrismLib.csproj
xbuild /p:Configuration=Release /p:AdofaiRoot="$GAME" /verbosity:quiet PrismLib.UI/PrismLib.UI.csproj
DLL="PrismLib/bin/Release/PrismLib.dll"
UIDLL="PrismLib.UI/bin/Release/PrismLib.UI.dll"

mcs -r:"$DLL" -out:/tmp/prismlibtest.exe tools/PrismLibTest.cs PrismLib.UI/Toolkit/LayoutMath.cs PrismLib.UI/Toolkit/Words.cs PrismLib.UI/Toolkit/SuiteModel.cs
cp "$DLL" /tmp/ && mono /tmp/prismlibtest.exe | tail -1

# REPOS (space-separated) picks the repos; REPOS="" opts out of all. Unset = the default list, right on a
# dev machine with the mods checked out beside PrismLib. A worktree run must name its own.
REPO_LIST="${REPOS-../Sapphire ../Topaz ../CamDisplay ../sapphire ../topaz}"
echo "game       $GAME"
echo "mods       $MODS"
for repo in $REPO_LIST; do [ -d "$repo/lib" ] && echo "repo       $repo/lib"; done
[ -n "$REPO_LIST" ] || echo "repo       (none)"

# The shared PrismLib.dll lives in each loader's folder (UMM's, and MelonLoader's Mods/PrismLib that
# CamDisplay's ML build reads): refresh every one that exists, and always the one chosen above.
mkdir -p "$MODS/PrismLib"
DONE=""
for d in "$MODS/PrismLib" "$GAME/UMMMods/PrismLib" "$GAME/Mods/PrismLib"; do
    [ -d "$d" ] || continue
    case "$DONE" in *"|$d|"*) continue ;; esac
    DONE="$DONE|$d|"
    cp "$DLL" "$d/"
    echo "installed  $d/PrismLib.dll"
done

# Every mod folder that already has a copy is one that ships it.
for dir in "$MODS"/*/; do
    # Facet also lives alone in its own mod folder (UMMMods/Facet): refresh that copy too.
    [ -f "$dir/$FACET.dll" ] && { cp "lib/$FACET.dll" "$dir"; echo "installed  ${dir}$FACET.dll"; }
    [ -f "$dir/PrismLib.UI.dll" ] || continue
    cp "$UIDLL" "lib/$FACET.dll" "$dir"
    echo "installed  ${dir}PrismLib.UI.dll + $FACET.dll"
done

# A runtime store (Quartz) is a copy folder too: <mods>/<Mod>/Runtime/versions/<ver>/ under either mods
# root, and <game>/UserData/<Mod>/Runtime/versions/<ver>/. Only where PrismLib.UI already is.
for store in "$GAME"/UMMMods/*/Runtime/versions/*/ "$GAME"/Mods/*/Runtime/versions/*/ "$GAME"/UserData/*/Runtime/versions/*/; do
    [ -f "${store}PrismLib.UI.dll" ] || continue
    cp "$UIDLL" "lib/$FACET.dll" "$store"
    echo "installed  ${store}PrismLib.UI.dll + $FACET.dll"
done

# MelonLoader loads UserLibs before any mod folder and the first copy wins: a stale one there
# runs instead of everything installed above.
if [ -f "$GAME/UserLibs/PrismLib.UI.dll" ]; then
    cp "$UIDLL" "lib/$FACET.dll" "$GAME/UserLibs/"
    echo "installed  $GAME/UserLibs/PrismLib.UI.dll"
fi

# ...and the source trees, so their next build compiles against the same thing.
# REPOS (space-separated) picks the repos: a worktree run names its own, e.g. REPOS="../Topaz-suite".
for repo in $REPO_LIST; do
    [ -d "$repo/lib" ] || continue
    cp "$DLL" "$UIDLL" "lib/$FACET.dll" "$repo/lib/"
    echo "refreshed  $repo/lib"
done

echo "Reload in-game (Ctrl+F10). No release was made."
