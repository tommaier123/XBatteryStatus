@echo off
goto check_Permissions

:check_Permissions
    echo Administrative permissions required. Detecting permissions...
    
    net session >nul 2>&1
    if %errorLevel% == 0 (
        echo Success: Administrative permissions confirmed.
    ) else (
        echo Failure: Current permissions inadequate.
    )
    
    pause >nul

taskkill /IM "XBatteryStatus.exe" /F
timeout /T 1 /nobreak
xcopy /s %~dp0\XBatteryStatus.exe "C:\ProgramData\Microsoft\Windows\Start Menu\Programs\StartUp" /Y
start "" "C:\ProgramData\Microsoft\Windows\Start Menu\Programs\StartUp\XBatteryStatus.exe"
