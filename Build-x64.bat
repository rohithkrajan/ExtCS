rem @echo off
call "C:\Program Files (x86)\Microsoft Visual Studio 11.0\VC\vcvarsall.bat" x64
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe ExtCS.sln /target:ExtCS /property:Configuration=Release;Platform=x64
