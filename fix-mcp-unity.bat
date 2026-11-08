@echo off
chcp 65001 >nul
echo Ustanavlivayu Python 3.12 dlya uv...
uv python install 3.12
echo Zakreplyayu Python 3.12 dlya MCP for Unity (UV_PYTHON=3.12)...
setx UV_PYTHON 3.12
echo Chishu kesh neudachnoy sborki...
uv cache clean pyyaml
echo.
echo Gotovo. Polnostyu zakroyte Unity i Unity Hub, zatem otkroyte proekt snova.
pause
