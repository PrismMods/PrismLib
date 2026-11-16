#!/bin/bash
# Build the LayoutProbe dev mod against this repo's PrismLib.UI and install it into UMMMods/.
# Run ./dev.sh first so every mod folder carries the same PrismLib.UI build.
set -euo pipefail
cd "$(dirname "$0")"
G=${ADOFAI_ROOT:-"$HOME/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice"}
M="$G/ADanceOfFireAndIce.app/Contents/Resources/Data/Managed"
FACET=$(sed -n 's:.*<Reference Include="\(Facet\.[0-9]*\.[0-9]*\)".*:\1:p' ../../PrismLib.UI/PrismLib.UI.csproj)
[ -n "$FACET" ] || { echo "no Facet reference in ../../PrismLib.UI/PrismLib.UI.csproj" >&2; exit 1; }
UI=../../PrismLib.UI/bin/Release/PrismLib.UI.dll
R=()   # an array: the game path has spaces
for a in mscorlib System System.Core netstandard UnityEngine.CoreModule UnityEngine.UIModule UnityEngine.UI \
         UnityEngine.InputLegacyModule UnityEngine.UIElementsModule UnityEngine.TextCoreTextEngineModule \
         UnityEngine.TextCoreFontEngineModule UnityEngine.IMGUIModule Unity.TextMeshPro; do
    if [ -f "$M/$a.dll" ]; then R+=("-r:$M/$a.dll"); fi
done
mcs -target:library -out:LayoutProbe.dll -nostdlib "${R[@]}" -r:"$G/UserLibs/UnityModManager.dll" -r:"$UI" -r:"../../lib/$FACET.dll" LayoutProbe.cs AutoTest.cs AutoTest.Suite.cs AutoTest.Shell.cs AutoTest.Filter.cs DemoSuite.cs Perf.cs
mkdir -p "$G/UMMMods/LayoutProbe"
cp LayoutProbe.dll Info.json "$UI" "../../lib/$FACET.dll" "$G/UMMMods/LayoutProbe/"
rm -f "$G/UMMMods/LayoutProbe/probe.log"
echo "installed to $G/UMMMods/LayoutProbe"
