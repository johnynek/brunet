#!/bin/bash
source /etc/ipop.vpn.config

ip=$(/sbin/ifconfig $DEVICE | \
  awk -F"inet addr:" {'print $2'} | \
  awk -F" " {'print $1'} | \
  grep -oE "[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+")
if [[ ! $ip ]]; then
  exit 0
fi
hostname="C"
for (( i = 2; i < 5; i++ )); do
  temp=`echo $ip | awk -F"." '{print $'$i'}' | awk -F"." '{print $1}'`
  if [[ $temp  -lt 10 ]]; then
    hostname=$hostname"00"
  elif [[ $temp -lt 100 ]]; then
    hostname=$hostname"0"
  fi
hostname=$hostname$temp
done
hostname $hostname.ipop
