# SpaceBurst
A simple cross-platform shooter and is still a work in progress.

![spaceburst](https://user-images.githubusercontent.com/982190/29991849-fddee9de-8f64-11e7-9b73-e6180c844c30.png)


## What can you do?
- Move (using WASD keys)
- Aim (mouse cursor)
- Shoot (using mouse click or arrow keys)
- Kill enemies
- Make points

## Build And Run

Prerequisite: install the .NET 8 SDK.

From the repository root:

```powershell
dotnet restore
dotnet build SpaceBurst.sln
dotnet run --project SpaceBurst\SpaceBurst.csproj
```

The repo now uses local MonoGame tooling, so you do not need a separate global MonoGame install just to build it.
