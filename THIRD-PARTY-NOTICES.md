# FoosVision Third-Party Notices

This file lists third-party components identified for the FoosVision product
tree (`./Product`) as of 2026-08-22.

FoosVision itself is licensed under the GNU General Public License,
version 3 or later. See `LICENSE.md`.

## .NET, .NET for Android, and .NET MAUI

FoosVision product apps are built with .NET and .NET MAUI, targeting
`net10.0-android` for the Recorder and Viewer applications.

The product directly references .NET and .NET MAUI packages including:

- `Microsoft.Maui.Controls` 10.0.71

License: `MIT`

Primary references:

- [.NET license information](https://github.com/dotnet/core/blob/main/license-information.md)
- [.NET runtime third-party notices](https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT)
- [.NET MAUI license](https://github.com/dotnet/maui/blob/main/LICENSE.txt)
- [.NET for Android](https://github.com/dotnet/android)

## Direct Product NuGet Packages

The following packages are centrally versioned in the repository root
`Directory.Packages.props` and are used by projects under `./Product`.

| Package                       | Version  | License                                    | Project                                                                              |
| ----------------------------- | -------- | ------------------------------------------ | ------------------------------------------------------------------------------------ |
| `Microsoft.Maui.Controls`     | 10.0.71  | `MIT`                                      | [dotnet/maui](https://github.com/dotnet/maui)                                        |
| `Serilog`                     | 4.3.1    | `Apache-2.0`                               | [serilog.net](https://serilog.net/)                                                  |
| `Serilog.Formatting.Compact`  | 3.0.0    | `Apache-2.0`                               | [serilog-formatting-compact](https://github.com/serilog/serilog-formatting-compact)  |
| `Serilog.Sinks.File`          | 7.0.0    | `Apache-2.0`                               | [serilog-sinks-file](https://github.com/serilog/serilog-sinks-file)                  |
| `Serilog.Sinks.Seq`           | 9.1.0    | `Apache-2.0`                               | [serilog-sinks-seq](https://github.com/serilog/serilog-sinks-seq)                    |
| `NetMQ`                       | 4.0.4.3  | `LGPL-3.0 with special linking exception`  | [zeromq/netmq](https://github.com/zeromq/netmq)                                      |
| `MessagePack`                 | 3.1.7    | `MIT`                                      | [MessagePack-CSharp](https://github.com/MessagePack-CSharp/MessagePack-CSharp)       |
| `MessagePack.Annotations`     | 3.1.7    | `MIT`                                      | [MessagePack-CSharp](https://github.com/MessagePack-CSharp/MessagePack-CSharp)       |
| `FFmpeg.AutoGen`              | 8.1.0    | `MIT`                                      | [FFmpeg.AutoGen](https://github.com/Ruslan-B/FFmpeg.AutoGen)                         |

## Third-Party Source Incorporated in Product

### NetDiscovery and NetDiscovery.Udp

| Field              | Value                                                                                              |
| ------------------ | -------------------------------------------------------------------------------------------------- |
| Source path        | `Product/Infrastructure/NetDiscovery`                                                              |
| Based on           | [Malcolmnixon/NetDiscovery](https://github.com/Malcolmnixon/NetDiscovery/tree/master/NetDiscovery) |
| License            | `MIT`                                                                                              |
| Copyright          | Copyright (c) 2019 Malcolm Nixon                                                                   |
| Local license file | `Product/Infrastructure/NetDiscovery/LICENSE`                                                      |

## Third-Party Product Assets

### Open Sans Fonts

| Field                     | Value                                                                                                                             |
| ------------------------- | --------------------------------------------------------------------------------------------------------------------------------- |
| Source paths              | `Product/Apps/FoosVision/Resources/Fonts/OpenSans-Regular.ttf`<br>`Product/Apps/FoosVision/Resources/Fonts/OpenSans-Semibold.ttf` |
| License                   | `Apache-2.0`                                                                                                                      |
| Embedded font notice      | Digitized data copyright (c) 2010-2011, Google Corporation.                                                                       |
| Embedded font license URL | <http://www.apache.org/licenses/LICENSE-2.0>                                                                                      |

## License Text References

Canonical license texts are available at:

- `MIT`: <https://licenses.nuget.org/MIT>
- `Apache-2.0`: <https://licenses.nuget.org/Apache-2.0>
- `LGPL-3.0`: <https://www.gnu.org/licenses/lgpl-3.0.txt>
- `GPL-3.0`: `LICENSE.md`
