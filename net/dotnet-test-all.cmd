@echo off

SqlLocalDB start

dotnet test --no-build "%~dp0DXAutomap.AspNet.Data.slnx" -m:1 -nodeReuse:false %*
set dotnet_status=%errorlevel%

rem Leave the shared developer/CI instance running; other tasks may be using it.

exit /b %dotnet_status%
