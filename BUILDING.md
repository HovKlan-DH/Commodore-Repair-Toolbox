# Classic Repair Toolbox


## Compiler instructions for different operating systems
- [Windows](#windows)
- [Linux](#linux)
- [macOS](#macos) 


## Windows

- Load `Classic-Repair-Toolbox.slnx` in Visual Studio
- Change build to use `RELEASE` instead of `DEBUG`
- Build it with `Build` > `Build Solution`
- Executable is now available in `bin\Release\net10.0\Classic-Repair-Toolbox.exe`


## Linux

### Common for all Linux builds

- Make sure .NET10 SDK is installed.
  - Install via your internal package management system
  - .. or download from here, https://dotnet.microsoft.com/en-us/download/dotnet/10.0
- Fork the _CRT_ GitHub repository
- Clone the fork to your local computer

### **Fedora**
- Compile RELEASE build
  - `dotnet publish -c Release -f net10.0 --self-contained`
- Run application:  
  - `./bin/Debug/net10.0/Classic-Repair-Toolbox`

### **Gentoo**
- Show all available .NET SDK versions
  - `eselect dotnet list`
- Choose .NET10 SDK, which is profile (1) in this example
  - `eselect dotnet set 1`
- Reload system environment variables
  - `. /etc/profile`
- Verify the active .NET SDK
  - `dotnet --list-sdks`
- Compile RELEASE build
  - `dotnet publish -c Release -f net10.0 --self-contained`
- Run application:
  - `bin/Release/net10.0/linux-x64/Classic-Repair-Toolbox`

A `DEBUG` build and a `RELEASE` build behave identically — the update check, the data sync and the diagnostics are the same in both. `RELEASE` is still what you want for a build you intend to use or measure, since `DEBUG` is JIT-only and starts noticeably slower.

To see the update banner without publishing a release, start the application with `--simulate-update` (or `--simulate-update=2.7.0` for a specific version). It offers a dummy update, fakes the download and does not restart. This works in both build configurations; the log says so in capitals and the banner itself is marked `(simulated)`.

To skip the online data check while working offline, untick "Check for new or updated data at application launch" in the Configuration tab.


## macOS

No help on this yet - please help me in providing these steps.
