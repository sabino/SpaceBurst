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

## Single-File Builds

To create the final self-contained single-file app for both Windows and Linux:

```powershell
./publish-single-file.ps1
```

This writes the outputs to:

- `artifacts/singlefile/win-x64/SpaceBurst.exe`
- `artifacts/singlefile/linux-x64/SpaceBurst`

You can also publish only one target:

```powershell
./publish-single-file.ps1 -RuntimeIdentifier win-x64
./publish-single-file.ps1 -RuntimeIdentifier linux-x64
```

On Linux, the publish output is a single executable file as well. If you build the Linux artifact on Linux, it should be directly runnable from the desktop or terminal. If you generate the Linux artifact on Windows and then copy it to Linux, you may still need to mark it executable once with `chmod +x SpaceBurst`.

## Android APK

There is now an Android project at `SpaceBurst.Android/SpaceBurst.Android.csproj`.

Touch controls:

- Left side drag moves the ship
- Right side touch aims and fires
- The game keeps the original 800x600 playfield and scales it to the phone screen

To build an installable debug APK on Windows:

```powershell
./build-android.ps1 -InstallDependencies
```

The first run installs the Android SDK into `C:\Android\sdk`.

The generated APK is written to:

- `SpaceBurst.Android/bin/Debug/net8.0-android34.0/com.sabino.spaceburst-Signed.apk`

If your phone is connected with USB debugging enabled, you can also install it directly:

```powershell
./build-android.ps1 -InstallOnDevice
```
