@echo off
"C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\MSBuild.exe" "r:\HDD R\ZC SYMLINK\USERS\source\repos\ghostminhtoan\get link manga\Comic-GMTPC.csproj" /t:Restore /v:minimal /nologo
"C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\MSBuild.exe" "r:\HDD R\ZC SYMLINK\USERS\source\repos\ghostminhtoan\get link manga\Comic-GMTPC.csproj" /t:Rebuild /p:Configuration=Release /p:Platform=AnyCPU /v:minimal /nologo
