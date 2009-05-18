#!/bin/sh
mono SocialVPN.exe brunet.config ipop.config 58888 $1 $2 "$3" &> log.txt &
sleep 30
dhclient tapipop
