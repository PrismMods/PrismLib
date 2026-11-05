#!/bin/bash
# Build the LayoutProbe dev mod against this repo's PrismLib.UI and install it into UMMMods/.
# Run ./dev.sh first so every mod folder carries the same PrismLib.UI build.
set -euo pipefail
cd "$(dirname "$0")"
G=${ADOFAI_ROOT:-"$HOME/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice"}
M="$G/ADanceOfFireAndIce.app/Contents/Resources/Data/Managed"
UI=../../PrismLib.UI/bin/Release/PrismLib.UI.dll
R=()   # an array: the game path has spaces
for a in mscorlib System System.Core netstandard UnityEngine.CoreModule UnityEngine.UIModule UnityEngine.UI \
         UnityEngine.InputLegacyModule UnityEngine.UIElementsModule UnityEngine.TextCoreTextEngineModule \
         UnityEngine.TextCoreFontEngineModule UnityEngine.IMGUIModule; do
    if [ -f "$M/$a.dll" ]; then R+=("-r:$M/$a.dll"); fi
done
mcs -target:library -out:LayoutProbe.dll -nostdlib "${R[@]}" -r:"$G/UserLibs/UnityModManager.dll" -r:"$UI" LayoutProbe.cs
mkdir -p "$G/UMMMods/LayoutProbe"
cp LayoutProbe.dll Info.json "$UI" "$G/UMMMods/LayoutProbe/"
rm -f "$G/UMMMods/LayoutProbe/probe.log"
echo "installed to $G/UMMMods/LayoutProbe"
