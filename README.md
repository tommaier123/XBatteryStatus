# XBatteryStatus
A tray app that displays the battery level of most bluetooth game controllers

![Tray Icon](/Icons/icon100.png)

## Features 
* Support for every bluetooth game controller that uses the standard ble battery service (Tested on Xbox Series X Controller)
* Battery level indication as dynamic tray icon
* Battery percentage when hovering over the icon
* Low battery notification at 15%, 10% and 5%
* Support for multiple paired controllers (but only one connected at a time)
* Tray icon will hide itself when no controller is connected

## Installation
* Install the [.NET 5 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/5.0)
* Make sure to pair your controller before running the app or restart after pairing a new one
* Download the [latest release](https://github.com/tommaier123/XBatteryStatus/releases/latest)
* Run the install.bat **with admin permissions** or manually move the XBatteryStatus.exe to the autostart folder
