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
@echo ======================================================
@echo Run configure script to set user name and machine id
@echo Run start_socialvpn to start socialvpn service
@echo Run stop_socialvpn to stop socialvpn service
@echo Point browser to http://127.0.0.1:58888/ for interface
@echo ======================================================
@pause

