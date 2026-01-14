# <img src="https://github.com/tommaier123/XBatteryStatus/blob/master/Icons/png/icon.png" height="24"/> XBatteryStatus 
[![Release](https://img.shields.io/github/release/tommaier123/XBatteryStatus.svg)](https://github.com/tommaier123/XBatteryStatus/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/tommaier123/XBatteryStatus/total)](https://github.com/tommaier123/XBatteryStatus/releases/latest)
[<img src="https://ko-fi.com/img/githubbutton_sm.svg" height="20">](https://ko-fi.com/W7W6PHPZ3)

A clean and lightweight tray app that displays the battery level of most bluetooth game controllers

![Screenshot](/Icons/Screenshot1.png)

![Tray Icon](/Icons/png/icon70.png)
![Tray Icon](/Icons/png/iconNumeric.png)

## Features 
* Support for every bluetooth game controller that uses the standard ble battery service (Tested on Xbox Series X Controller)
* Battery level indication as dynamic tray icon
* Battery percentage when hovering over the icon
* Low battery notification at 15%, 10% and 5%
* Support for multiple paired controllers (but only one connected at a time)
* Tray icon will hide itself when no controller is connected
* Support for Windows light mode and dark mode
* Update notifications for new versions with an automatic update option

## Installation via Installer
* Download the [latest release](https://github.com/tommaier123/XBatteryStatus/releases/latest)
* Run the XBatteryStatus.msi

## Installation via Winget
* ```winget install Nova_Max.XBatteryStatus```
* Run XBatteryStatus

## Installation via Microsoft Store
<a href="https://apps.microsoft.com/detail/xpdmdt0hpqbm7z?referrer=appbadge&mode=direct">
	<img src="https://get.microsoft.com/images/en-us%20dark.svg" width="200"/>
</a>

## Requirements
* [.NET 8 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/8.0) (automatically installed)

## Updating
* When a new version is released you will get notified three times
* Either manually download and install the latest version
* Or click on update to install it automatically


## Settings
* Theme: Override for automatic light/dark theme detection
* Auto Hide: When enabled the tray icon will hide itself when no controller is connected
* Numeric: Turn on the numeric display


## Sponsors

<table>
  <tr>
    <td><img src="https://github.com/user-attachments/assets/ca93d971-67dc-41dd-b945-ab4f372ea72a" height="20"/></td>
    <td>Free code signing on Windows provided by <a href="https://signpath.io">SignPath.io</a>, certificate by <a href="https://signpath.org">SignPath Foundation</a></td>
  </tr>
</table>

<table>
  <tr>
    <td><img src="https://upload.wikimedia.org/wikipedia/commons/2/22/Advanced-Installer-logo-new.png" height="20"/></td>
    <td>Installer powered by <a href="https://www.advancedinstaller.com/">Advanced Installer</a></td>
  </tr>
</table>
