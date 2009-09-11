%~d0
CD %~dp0
@echo Installing Virtual Tap Network Interface...
cd drivers\windows_tap
@wscript uninstall_tap.vbs
@wscript install_tap.vbs
cd ..\..\
@echo Installing SocialVPN as a service...
@%SystemRoot%\Microsoft.NET\Framework\v2.0.50727\installutil SocialVPNService.exe
@cscript create_cert.js
@echo To start SocialVPN, right-click on start_socialvpn.cmd, run as administrator
@echo To stop SocialVPN, right-click on stop_socialvpn.cmd, run as administrator
@echo Use your firefox browser and go to this URL: http://127.0.0.1:58888/
@pause
