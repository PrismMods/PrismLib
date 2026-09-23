#!/usr/bin/env bash
# ./release.sh            build + run the self-check
# ./release.sh 0.2.0      also stamp the version everywhere and regenerate prismlib.json
#
# prismlib.json is the feed PrismBootstrap reads. Its sha256 is computed from the DLL built RIGHT
# HERE, so publish the same file: upload bin/Release/PrismLib.dll to the tag's release, then push.
# A mismatch is not a broken install — the bootstrapper discards the download and every mod falls
# back to running standalone, silently. raw.githubusercontent caches the feed for a few minutes, so
# a fresh release is not visible to clients (or to lib/update-prismlib.sh) straight away.
set -euo pipefail
cd "$(dirname "$0")"

REPO="PrismMods/PrismLib"
VER="${1:-}"

if [ -n "$VER" ]; then
    sed -i '' -E "s/AssemblyVersion\(\"[0-9.]+\"\)/AssemblyVersion(\"$VER.0\")/;s/AssemblyFileVersion\(\"[0-9.]+\"\)/AssemblyFileVersion(\"$VER.0\")/" PrismLib/Properties/AssemblyInfo.cs
    sed -i '' -E "s/new Version\([0-9]+, [0-9]+, [0-9]+\);/new Version(${VER//./, });/" PrismLib/Prism.cs
    echo "stamped $VER"
fi

xbuild /p:Configuration=Release /verbosity:minimal PrismLib/PrismLib.csproj
DLL="PrismLib/bin/Release/PrismLib.dll"

mcs -r:"$DLL" -out:/tmp/prismlibtest.exe tools/PrismLibTest.cs
cp "$DLL" /tmp/
mono /tmp/prismlibtest.exe

# The bootstrapper drops it into a mod, so it must keep compiling on its own.
mcs -target:library -r:System.dll -out:/tmp/prismbootcheck.dll bootstrap/PrismBootstrap.cs
echo "bootstrap compiles standalone"

if [ -n "$VER" ]; then
    SHA=$(shasum -a 256 "$DLL" | cut -d' ' -f1)
    cat > prismlib.json <<JSON
{
    "version": "$VER",
    "url": "https://github.com/$REPO/releases/download/v$VER/PrismLib.dll",
    "sha256": "$SHA"
}
JSON
    echo "wrote prismlib.json ($SHA)"
fi
