%~d0
CD %~dp0
@net stop SocialVPN
@%SystemRoot%\Microsoft.NET\Framework\v2.0.50727\installutil /uninstall SocialVPNService.exe
cd drivers\windows_tap
@wscript uninstall_tap.vbs
cd ..\..\
cd drivers\windows_tap
@wscript install_tap.vbs
cd ..\..\
@%SystemRoot%\Microsoft.NET\Framework\v2.0.50727\installutil SocialVPNService.exe
