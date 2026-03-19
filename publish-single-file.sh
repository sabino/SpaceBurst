#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project="$script_dir/SpaceBurst/SpaceBurst.csproj"
artifacts_root="$script_dir/artifacts/singlefile"

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
    -p:DebugType=None \
    -p:DebugSymbols=false \
    -o "$output"
done
