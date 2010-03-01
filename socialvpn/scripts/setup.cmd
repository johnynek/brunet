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
@cscript create_cert.js
@echo ========================================================================
@echo To start SocialVPN, right-click on start_socialvpn, run as administrator
@echo To stop SocialVPN, right-click on stop_socialvpn, run as administrator
@echo Use your firefox browser and go to this URL: http://127.0.0.1:58888/
@echo ========================================================================
@pause
