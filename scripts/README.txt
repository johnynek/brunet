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

2) Windows XP/Vista Instructions
--------------------------------
Run Setup.cmd, then

For XP, Double-click on SocialVPN.exe

For Vista, right-click on SocialVPN.exe and "Run as administrator"

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

Frequently Asked Questions
==========================
1) How are the connections made?

When a user adds a friend by email address, SocialVPN creates VPN connections
to computers with the matching email address. All SocialVPN nodes with the
same email address are automatically connected to each other.

2) How long does it take to make the connections?

At startup, SocialVPN may take up to 60 seconds before discovering connections.

2) How can I verify that I have a truly private connection?

You should call your friend and ask him/her to verify their fingerprints,
if the fingerprints are the same on both sides, then your communication is
secure. For the experts, you can also take a look at the security source
code under the BruNet repository (http://github.com/davidiw/brunet/) to verify
the validity of the security code.

3) Why can my friends automatically connect to me without my consent?

SocialVPN uses a "Global_Access" flag at start-up which determines whether to
allow friends to automatically access your network by just adding your email
address to their SocialVPN, you can disable "Global_Access" by using the
SVPN tool or the web interface.


Questions/Comments:
===================
Contact Pierre St Juste <ptony82@ufl.edu> for any questions.

License:
========
This code is licensed under the GPLv2 (or at your option any later version).
