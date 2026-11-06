#!/usr/bin/env bash
# Rebuild Facet (the sibling repo, or FACET_ROOT) and refresh lib/<minor>.dll, the copy PrismLib.UI
# builds against and every mod ships beside PrismLib.UI.dll. The minor is whichever PrismLib.UI.csproj
# references. Facet targets netstandard2.1.
set -euo pipefail
cd "$(dirname "$0")/.."
FACET=$(sed -n 's:.*<Reference Include="\(Facet\.[0-9]*\.[0-9]*\)".*:\1:p' PrismLib.UI/PrismLib.UI.csproj)
[ -n "$FACET" ] || { echo "no Facet reference in PrismLib.UI/PrismLib.UI.csproj" >&2; exit 1; }
ROOT=${FACET_ROOT:-../Facet}
# This copy ships with every mod (and with PrismLib's release), so it must be a committed Facet.
if [ "${FORCE:-}" != 1 ] && [ -n "$(git -C "$ROOT" status --porcelain)" ]; then
    echo "$ROOT has uncommitted changes; commit them first (or FORCE=1 $0 for a throwaway build)" >&2
    git -C "$ROOT" status --short >&2
    exit 1
fi
(cd "$ROOT" && dotnet build Facet/Facet.csproj -c Release --nologo -v q)
mkdir -p lib
BUILT="$ROOT/Facet/bin/Release/netstandard2.1/$FACET.dll"
[ -f "$BUILT" ] || { echo "$ROOT does not build $FACET.dll" >&2; exit 1; }
cp "$BUILT" lib/
echo "lib/$FACET.dll  $(md5 -q lib/$FACET.dll 2>/dev/null || md5sum lib/$FACET.dll | cut -d' ' -f1)"
