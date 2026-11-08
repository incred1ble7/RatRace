@echo off
set OUT=%~dp0mcp-diag.txt
echo === uv version === > "%OUT%"
uv --version >> "%OUT%" 2>&1
echo === uv python list (installed) === >> "%OUT%"
uv python list --only-installed >> "%OUT%" 2>&1
echo === DLLs in 3.12 === >> "%OUT%"
dir /b "%APPDATA%\uv\python\cpython-3.12-windows-x86_64-none\DLLs\*ssl*" "%APPDATA%\uv\python\cpython-3.12-windows-x86_64-none\DLLs\*crypto*" >> "%OUT%" 2>&1
echo === ssl test 3.12 normal PATH === >> "%OUT%"
uv run --no-project --python 3.12 python -c "import ssl;print(ssl.OPENSSL_VERSION)" >> "%OUT%" 2>&1
echo === ssl test 3.12 clean PATH === >> "%OUT%"
setlocal
set PATH=%USERPROFILE%\.local\bin;C:\WINDOWS\system32;C:\WINDOWS
uv run --no-project --python 3.12 python -c "import ssl;print(ssl.OPENSSL_VERSION)" >> "%OUT%" 2>&1
endlocal
echo === ssl test 3.13 === >> "%OUT%"
uv run --no-project --python 3.13 python -c "import ssl;print(ssl.OPENSSL_VERSION)" >> "%OUT%" 2>&1
echo === Git ssl dlls === >> "%OUT%"
dir /b "C:\Program Files\Git\mingw64\bin\*ssl*" >> "%OUT%" 2>&1
echo Gotovo. Napishite Claude.
pause
