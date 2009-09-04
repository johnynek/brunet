#!/bin/bash

user=`whoami`

if [[ $user == "root" ]]; then
  echo "Please run as NON-root"
  exit
fi

if [[ ! -d certificates ]]; then
  echo -n "Enter userid (jabberid@host.com): "
  read userid
  echo -n "Enter PCID (home-pc): "
  read pcid
  echo -n "Enter Name (Jane Doe): "
  read name
  mono svpncmd.exe cert $userid $pcid "$name"
  chmod 600 private_key
  if [[ -d certificates ]]; then
    echo "Run ./socialvpn as root"
  fi
fi
