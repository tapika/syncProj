@echo off
rem %1 - ARM, ARM64, ..., %2 - Release/Debug
rem Uncomment next line to use also for debug same keystore as for release
rem goto useThisKeyStore
if "%2" == "Debug" (
    echo Using debug signing keys...
    exit /b 0
)
rem -----------------------------------------------------------------------------------------
rem Android requires that all APKs be digitally signed with a certificate before they can be installed
rem 
rem When making final application, please select good storage place of keystore file, as well
rem as good password. Meanwhile if you want just try out something - remove "rem " from next line
rem to create just some default keystore
rem -----------------------------------------------------------------------------------------
rem goto useThisKeyStore
echo %~dpnx0^(9^): Error: APK must be signed, please configure your keystore file
exit /b 1

:useThisKeyStore
if "%JAVA_HOME%" == "" call %~dp0gradlew.bat env
rem -----------------------------------------------------------------------------------------
rem .apk signing configuration
rem -----------------------------------------------------------------------------------------
set SIGN_KEY_ALIAS=androiddebugkey
set SIGN_STORE_PASSWORD=android
set SIGN_STORE_FILE=app.keystore
rem Following line forces keystore name to be configuration specific (E.g. release.keystore / debug.keystore)
rem Comment line with 'rem' if you want to use generic store file
rem if not exist "%~dp0%SIGN_STORE_FILE%" set SIGN_STORE_FILE=%2.keystore

if not exist "%~dp0%SIGN_STORE_FILE%" (

    if not exist "%JAVA_HOME%\bin\keytool.exe" (
        echo %~dpnx0^(35^): Error: keytool.exe was not found.
        exit /b 2
    )

    echo Generating new keystore %SIGN_STORE_FILE%...
    "%JAVA_HOME%\bin\keytool.exe" -keyalg RSA -genkeypair -alias %SIGN_KEY_ALIAS% -keypass %SIGN_STORE_PASSWORD% -keystore "%~dp0%SIGN_STORE_FILE%" -storepass %SIGN_STORE_PASSWORD% -dname "CN=Android Debug,O=Android,C=US" -validity 9999
)

echo Using %SIGN_STORE_FILE% for singing apk package...
echo %SIGN_STORE_FILE%
echo %SIGN_STORE_PASSWORD%
echo %SIGN_KEY_ALIAS%
exit /b 0
