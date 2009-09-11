SocialVPN - The Social Virtual Private Network
==============================================
This application allows you to create encrypted P2P VPN links through the use
of the IPOP/BruNet P2P Library. You can download it from socialvpn.org.

Running SocialVPN
=================
Download and extract binaries from socialvpn.org.

1) Linux Instructions
---------------------
Install necessary packages 
(Debian-based distributions)

apt-get install resolvconf nant
/etc/init.d/networking restart 

(you have to do this only when you first install resovlconf)

tar -xzvf socialvpn.x.x.x
cd socialvpn.x.x.x
./setup.sh (only at first run)
su
./socialvpn

To stop SocialVPN, kill -INT (svpnpid) 

2) Windows XP/Vista Instructions
--------------------------------
Run setup.cmd to install Virtual Network Interface

To start SocialVPN, run start_socialvpn.cmd

To stop SocialVPN, run stop_socialvpn.cmd

For Vista, right-click and "Run as administrator" for each step.

Using SocialVPN
===============
SocialVPN can be managed through a Web browser or the svpncmd.exe command 
line utility.

1) Web Access
-- Point your Mozilla-based (Firefox or Chrome) browser to http://127.0.0.1:58888/
-- To add a friend, click on "Add Friends", enter friend's email address and 
   and fingerprint then submit

   jabberid@jabber.org SVPN:1232312892123213

2) Console Access
-- mono svpncmd.exe info - shows user info and list of friends
-- mono svpncmd.exe add email fpr - add a friend by email address

Questions/Comments:
===================
Contact Pierre St Juste <ptony82@ufl.edu> for any questions.

License:
========
This code is licensed under the GPLv2 (or at your option any later version).
