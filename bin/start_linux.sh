#!/bin/bash

# the name email
email=$1

# unique pc identifier
pcid=$2

# the user name with quotes
name=$3

# the user location
location=$4

# set variables
cert_dir=certificates

if [[ -d $cert_dir ]]; then
  mono SocialVPN.exe brunet.config ipop.config 58888 &> log.txt &
else
  if [[ $# -lt 4 ]]; then
    echo "usage (on first run): ./start_linux.sh <email> <pcid> <name> <location>"
    exit
  fi
  mono SocialVPN.exe brunet.config ipop.config 58888 $email $pcid "$name" $location &> log.txt &
fi
