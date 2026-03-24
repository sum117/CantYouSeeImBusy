#!/bin/bash
set -euo pipefail

# ── Config ──────────────────────────────────────────────────────────
WORKSHOP_ID="3688506794"
MOD_DIR="$(cd "$(dirname "$0")" && pwd)"
# SteamCMD on Windows needs native paths (C:\...), not MSYS/Git Bash paths (/c/...)
MOD_DIR_WIN="$(cygpath -w "$MOD_DIR" 2>/dev/null || echo "$MOD_DIR")"
STAGING_DIR="$MOD_DIR/_publish_staging"
STAGING_DIR_WIN="$MOD_DIR_WIN\\_publish_staging"
STEAMCMD="steamcmd"
CSPROJ="$MOD_DIR/1.6/Source/CantYouSeeImBusy.csproj"
ABOUT_XML="$MOD_DIR/About/About.xml"

# Folders to include in the workshop upload
INCLUDE_DIRS=(
  "1.6/Assemblies"
  "1.6/Defs"
  "About"
  "Languages"
)

# ── Parse args ──────────────────────────────────────────────────────
BUMP="${1:-}"
CHANGENOTE="${2:-}"

if [ -z "$BUMP" ] || [ -z "$CHANGENOTE" ]; then
  echo "Usage: ./publish.sh <patch|minor|major> \"<change note>\""
  echo "Example: ./publish.sh patch \"Fix mental break protection not applying to prisoners\""
  exit 1
fi

# ── Bump version ────────────────────────────────────────────────────
CURRENT=$(sed -n 's/.*<modVersion>\(.*\)<\/modVersion>.*/\1/p' "$ABOUT_XML")
IFS='.' read -r MAJOR MINOR PATCH <<< "$CURRENT"

case "$BUMP" in
  major) MAJOR=$((MAJOR + 1)); MINOR=0; PATCH=0 ;;
  minor) MINOR=$((MINOR + 1)); PATCH=0 ;;
  patch) PATCH=$((PATCH + 1)) ;;
  *) echo "ERROR: bump must be patch, minor, or major"; exit 1 ;;
esac

NEW_VERSION="$MAJOR.$MINOR.$PATCH"
echo "==> Bumping version: $CURRENT -> $NEW_VERSION"

# Update About.xml
sed -i "s|<modVersion>$CURRENT</modVersion>|<modVersion>$NEW_VERSION</modVersion>|" "$ABOUT_XML"

# Update .csproj (AssemblyVersion + FileVersion)
sed -i "s|<AssemblyVersion>$CURRENT</AssemblyVersion>|<AssemblyVersion>$NEW_VERSION</AssemblyVersion>|" "$CSPROJ"
sed -i "s|<FileVersion>$CURRENT</FileVersion>|<FileVersion>$NEW_VERSION</FileVersion>|" "$CSPROJ"

# ── Build ───────────────────────────────────────────────────────────
echo "==> Building mod (Release)..."
dotnet build "$CSPROJ" -c Release --nologo -v quiet
echo "    Build succeeded ✓"

# ── Validate Steam credentials ──────────────────────────────────────
if [ -z "${STEAM_USER:-}" ]; then
  read -rp "Steam username: " STEAM_USER
fi

# ── Stage files ─────────────────────────────────────────────────────
echo "==> Staging mod files..."
rm -rf "$STAGING_DIR"

for dir in "${INCLUDE_DIRS[@]}"; do
  src="$MOD_DIR/$dir"
  dest="$STAGING_DIR/$dir"
  if [ ! -d "$src" ]; then
    echo "WARNING: $dir not found, skipping"
    continue
  fi
  mkdir -p "$dest"
  cp -r "$src/." "$dest/"
  echo "    $dir ✓"
done

# ── Generate VDF ────────────────────────────────────────────────────
VDF_PATH="$STAGING_DIR/_workshop.vdf"
cat > "$VDF_PATH" <<EOF
"workshopitem"
{
  "appid"           "294100"
  "publishedfileid" "$WORKSHOP_ID"
  "contentfolder"   "$STAGING_DIR_WIN"
  "changenote"      "$CHANGENOTE"
}
EOF

VDF_PATH_WIN="$(cygpath -w "$VDF_PATH" 2>/dev/null || echo "$VDF_PATH")"

echo "==> Uploading to Workshop (ID: $WORKSHOP_ID)..."
"$STEAMCMD" +login "$STEAM_USER" +workshop_build_item "$VDF_PATH_WIN" +quit
RESULT=$?

# ── Cleanup ─────────────────────────────────────────────────────────
echo "==> Cleaning up staging directory..."
rm -rf "$STAGING_DIR"

if [ $RESULT -eq 0 ]; then
  echo "==> Published v$NEW_VERSION successfully!"
else
  echo "==> Upload failed (exit code $RESULT)"
  exit $RESULT
fi
