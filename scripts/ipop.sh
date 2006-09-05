#!/bin/bash
# This is the RC script for Grid-Appliance and performs the following functions
# 1) Check for a pre-existing configuration (grid_client_lastcall)
# 2a) If it does not exist, access the IPOP "DHCP" server and obtain an IP
#     address, condor configuration file, and finally generate /etc/hosts
# 2b) If it does exist, continue
# 3) Initialize the tap device
# 4) Start iprouter
# 5) Apply the iptables rules

dir="/usr/local/ipop"

if [[ $1 = "start" || $1 = "restart" ]]; then
  if [[ $1 = start ]]; then
    echo "Starting Grid Services..."
  else
    echo "Restarting Grid Services..."
  fi
  pkill iprouter 

  # set up tap device
  $dir/tools/tunctl -u root -t tap0

  echo "tap configuration completed"

  # Create config file for IPOP and start it up
  if test -f $dir/var/ipop.config; then
    test
  else
    cp $dir/config/ipop_dhcp_linux.config $dir/var/ipop.config
  fi

  cd $dir/tools
  $dir/tools/iprouter $dir/var/ipop.config &> $dir/var/ipoplog &
  cd -

  rm -f /var/log/ipop
  ln -s $dir/var/ipoplog /var/log/ipop

  for pid in `ps uax | grep "dhclient tap0" | awk -F" " '{print $2}'`; do
    kill $pid
  done
  dhclient tap0

  echo "IPOP has started"

elif [[ $1 = "stop" ]]; then
    pkill iprouter
    ifdown tap0
    $dir/tools/tunctl -d tap0

else
    echo "Run script with start, restart, or stop"
 fi
