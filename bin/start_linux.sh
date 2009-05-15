#!/bin/sh
mono SocialVPN.exe brunet.config ipop.config 58888 &> log.txt &
sleep 30
dhclient tapipop
