taskkill /IM "XBatteryStatus.exe" /F
timeout /T 1 /nobreak
xcopy /s %~dp0\XBatteryStatus.exe "C:\ProgramData\Microsoft\Windows\Start Menu\Programs\StartUp" /Y
start "" "C:\ProgramData\Microsoft\Windows\Start Menu\Programs\StartUp\XBatteryStatus.exe"