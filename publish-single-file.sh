#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project="$script_dir/SpaceBurst/SpaceBurst.csproj"
artifacts_root="$script_dir/artifacts/singlefile"
commit="$(git -C "$script_dir" rev-parse --short HEAD)"
base_version="1.1"
stamp="$(date -u +%y%j%H%M)"
display_version="$base_version.$stamp"
build_version="$(date -u +1.1.%y%j.%H%M)"
informational_version="$display_version+$commit"

if [ "$#" -eq 0 ]; then
  runtimes=(win-x64 linux-x64)
else
  runtimes=("$@")
fi

dotnet tool restore

for rid in "${runtimes[@]}"; do
  output="$artifacts_root/$rid"
  rm -rf "$output"

  dotnet publish "$project" \
    -c Release \
    -r "$rid" \
    -p:SelfContained=true \
    -p:PublishSingleFile=true \
    -p:Version="$build_version" \
    -p:InformationalVersion="$informational_version" \
    -p:DebugType=None \
    -p:DebugSymbols=false \
    -o "$output"

  if [ -f "$output/SpaceBurst.exe" ]; then
    cp "$output/SpaceBurst.exe" "$artifacts_root/SpaceBurst-$rid-v$display_version.exe"
  elif [ -f "$output/SpaceBurst" ]; then
    cp "$output/SpaceBurst" "$artifacts_root/SpaceBurst-$rid-v$display_version"
  fi
done
