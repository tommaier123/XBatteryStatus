# XBatteryStatus
[![Release](https://img.shields.io/github/release/tommaier123/XBatteryStatus.svg)](https://github.com/tommaier123/XBatteryStatus/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/tommaier123/XBatteryStatus/total)](https://github.com/tommaier123/XBatteryStatus/releases/latest)
[![Twitter](https://img.shields.io/twitter/follow/Nova_Max_?style=social)](https://twitter.com/Nova_Max_)
[<img src="https://ko-fi.com/img/githubbutton_sm.svg" height="20">](https://ko-fi.com/W7W6PHPZ3)

A clean and lightweight tray app that displays the battery level of most bluetooth game controllers

![Tray Icon](/Icons/icon100.png)

## Features 
* Support for every bluetooth game controller that uses the standard ble battery service (Tested on Xbox Series X Controller)
* Battery level indication as dynamic tray icon
* Battery percentage when hovering over the icon
* Low battery notification at 15%, 10% and 5%
* Support for multiple paired controllers (but only one connected at a time)
* Tray icon will hide itself when no controller is connected
* Support for Windows light mode and dark mode
* Update notifications for new versions

## Installation via Installer
* Install the [.NET 5 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/5.0)
* Download the [latest release](https://github.com/tommaier123/XBatteryStatus/releases/latest)
* Run the XBatteryStatusInstaller.msi

## Installation via Winget
* ```winget install Nova_Max.XBatteryStatus```
* .net Desktop Runtime will be installed automatically
* Run XBatteryStatus

## Updating
(When a new version is released you will get notified only three times)
* Stop XBatteryStatus by rightclicking on the icon and selecting "Exit" (optinal but speeds up installation)
* Download the [latest release](https://github.com/tommaier123/XBatteryStatus/releases/latest)
* Run the install.msi

## Setup
* After pairing a new controller you need to stop and restart XBatteryStatus or turn off and on Bluetooth
* This needs to be done only once after pairing after that everything will work automatically

## Settings
* Theme: Override for automatic light/dark theme detection
* Auto Hide: When enabled the tray icon will hide itself when no controller is connected
