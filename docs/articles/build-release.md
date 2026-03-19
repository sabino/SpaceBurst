# Build and Release

## Local Build

Prerequisite: install the .NET 8 SDK.

From the repository root:

```powershell
dotnet restore
dotnet build SpaceBurst.sln
dotnet test SpaceBurst.Runtime.Tests/SpaceBurst.Runtime.Tests.csproj
.\build-all.ps1
```

`build-all.ps1` produces a deterministic `artifacts/release/latest` bundle for local testing.

## Local Documentation

```powershell
.\build-docs.ps1
```

The generated site is written to `docs/_site`.

## GitHub Actions

The repository defines three automation lanes:

- **PR Checks**: Windows build/tests, Linux publish smoke, Android smoke build, and DocFX smoke.
- **Master Prerelease**: Windows zip, Linux tarball, signed Android APK, hashes, and release notes.
- **Publish Docs**: DocFX build and GitHub Pages deployment.

## Android Signing Secrets

The master prerelease workflow expects these GitHub secrets:

- `ANDROID_KEYSTORE_BASE64`
- `ANDROID_KEYSTORE_PASSWORD`
- `ANDROID_KEY_ALIAS`
- `ANDROID_KEY_PASSWORD`
