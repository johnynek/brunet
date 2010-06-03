%~d0
CD %~dp0
@cscript create_cert.js
@echo ======================================================
@echo Run start_socialvpn to start socialvpn service
@echo Run stop_socialvpn to stop socialvpn service
@echo Point browser to http://127.0.0.1:58888/ for interface
@echo ======================================================
@pause
