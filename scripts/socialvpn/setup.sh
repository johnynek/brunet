#!/bin/bash

user=`whoami`

if [[ $user == "root" ]]; then
  echo "Please run as NON-root"
  exit
fi

if [[ $user != "root"  ]]; then
  echo -n "Enter userid (jabberid@host.com): "
  read userid
  echo -n "Enter PCID (home-pc): "
  read pcid
  echo "Creating certificate..."
  mono svpncmd.exe cert $userid $pcid 
  chmod 600 private_key
  echo "Run ./socialvpn as root"
fi
