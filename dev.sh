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
xbuild /p:Configuration=Release /p:AdofaiRoot="$GAME" /verbosity:quiet PrismLib/PrismLib.csproj
xbuild /p:Configuration=Release /p:AdofaiRoot="$GAME" /verbosity:quiet PrismLib.UI/PrismLib.UI.csproj
DLL="PrismLib/bin/Release/PrismLib.dll"
UIDLL="PrismLib.UI/bin/Release/PrismLib.UI.dll"

mcs -r:"$DLL" -out:/tmp/prismlibtest.exe tools/PrismLibTest.cs
cp "$DLL" /tmp/ && mono /tmp/prismlibtest.exe | tail -1

mkdir -p "$MODS/PrismLib"
cp "$DLL" "$MODS/PrismLib/"
echo "installed  $MODS/PrismLib/PrismLib.dll"

# Every mod folder that already has a copy is one that ships it.
for dir in "$MODS"/*/; do
    [ -f "$dir/PrismLib.UI.dll" ] || continue
    cp "$UIDLL" "$dir"
    echo "installed  ${dir}PrismLib.UI.dll"
done

# ...and the source trees, so their next build compiles against the same thing.
for repo in ../Sapphire ../Bismuth; do
    [ -d "$repo/lib" ] || continue
    cp "$DLL" "$UIDLL" "$repo/lib/"
    echo "refreshed  $repo/lib"
done

echo "Reload in-game (Ctrl+F10). No release was made."
