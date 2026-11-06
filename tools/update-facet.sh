#!/usr/bin/env bash
# Rebuild Facet (the sibling repo) and refresh lib/Facet.0.1.dll, the copy PrismLib.UI builds
# against and every mod ships beside PrismLib.UI.dll. Facet targets netstandard2.1.
set -euo pipefail
cd "$(dirname "$0")/.."
FACET=${FACET_ROOT:-../Facet}
(cd "$FACET" && dotnet build Facet/Facet.csproj -c Release --nologo -v q)
mkdir -p lib
cp "$FACET/Facet/bin/Release/netstandard2.1/Facet.0.1.dll" lib/
echo "lib/Facet.0.1.dll  $(md5 -q lib/Facet.0.1.dll 2>/dev/null || md5sum lib/Facet.0.1.dll | cut -d' ' -f1)"
