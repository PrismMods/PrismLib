#!/usr/bin/env bash
# A mod vendors bootstrap/*.cs verbatim (PrismBootstrap.cs, PrismGate.cs, PrismSettingRows.cs): every copy named here must
# be byte-identical to this repo's file of the same name. Change the file here and copy it over.
#
#   tools/check-vendored.sh <copy>...
set -euo pipefail
here="$(cd "$(dirname "$0")/.." && pwd)"
bad=0
for copy in "$@"; do
    src="$here/bootstrap/$(basename "$copy")"
    if [ ! -f "$src" ]; then echo "  ??   $copy: no bootstrap/$(basename "$copy") here" >&2; bad=1
    elif cmp -s "$src" "$copy"; then echo "  ok   $copy"
    else echo "  STALE $copy differs from bootstrap/$(basename "$copy")" >&2; bad=1
    fi
done
exit $bad
