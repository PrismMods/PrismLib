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

GAME="${ADOFAI_ROOT:-$HOME/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice}"
if   [ -d "$GAME/UMMMods" ]; then MODS="$GAME/UMMMods"
elif [ -d "$GAME/Mods" ];    then MODS="$GAME/Mods"
else echo "ERROR: no UMMMods/ or Mods/ under $GAME (set ADOFAI_ROOT?)" >&2; exit 1
fi

xbuild /p:Configuration=Release /verbosity:quiet PrismLib/PrismLib.csproj
xbuild /p:Configuration=Release /verbosity:quiet PrismLib.UI/PrismLib.UI.csproj
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
