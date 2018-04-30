@echo off

msbuild "Genuine Channels\GenuineChannels.Desktop.sln" /t:Rebuild /fl /flp:LogFile=msbuild.log /p:Configuration=Release

nuget pack "Nuget\GenuineChannels.nuspec"
