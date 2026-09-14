@echo off
REM PATH shim: block git write unless REPO_GIT_MCP=1 (set by perform_commit).
setlocal EnableDelayedExpansion
set "REAL_GIT="
for %%I in (git.exe) do (
  if /I not "%%~$PATH:I"=="%~f0" if defined REAL_GIT (
    rem keep first real
  ) else (
    rem find first git.exe not this shim — walk PATH manually below
  )
)

REM Resolve real git: prefer Git for Windows common paths, else where.exe skipping this dir
set "SHIM_DIR=%~dp0"
set "REAL_GIT="
if exist "%ProgramFiles%\Git\cmd\git.exe" set "REAL_GIT=%ProgramFiles%\Git\cmd\git.exe"
if not defined REAL_GIT if exist "%ProgramFiles(x86)%\Git\cmd\git.exe" set "REAL_GIT=%ProgramFiles(x86)%\Git\cmd\git.exe"
if not defined REAL_GIT (
  for /f "delims=" %%G in ('where git 2^>nul') do (
    echo %%G | findstr /I /C:"%SHIM_DIR%" >nul
    if errorlevel 1 (
      if not defined REAL_GIT set "REAL_GIT=%%G"
    )
  )
)

if not defined REAL_GIT (
  echo git-shim: cannot find real git.exe >&2
  exit /b 1
)

set "BLOCK=0"
set "ARGS=%*"
echo %ARGS% | findstr /I /R /C:"\<commit\>" /C:"\<push\>" /C:"\<merge\>" /C:"\<rebase\>" /C:"\<cherry-pick\>" /C:"\< am\>" /C:"\<revert\>" /C:"\<tag\>" /C:"\<update-ref\>" >nul && set "BLOCK=1"

if "%REPO_GIT_MCP%"=="1" set "BLOCK=0"

if "%BLOCK%"=="1" (
  echo Blocked: use MCP repo-git.commit for commits; push is owner-only. >&2
  exit /b 1
)

"%REAL_GIT%" %*
exit /b %ERRORLEVEL%
