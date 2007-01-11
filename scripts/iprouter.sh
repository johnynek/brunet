#!/bin/bash
# RC Script for running mkbundle IPOP on Linux

if [ $1 = "start" ]; then
  # set up tap device
  tunctl -u root -t tap0

  echo "tap configuration completed"

  /usr/bin/iprouter /etc/ipop.config &> /var/log/ipop.log &

  echo "IPOP has started"

elif [ $1 = "stop" ]; then
  pkill iprouter
  ifdown tap0
  tunctl -d tap0
elif [ $1 = "restart" ]; then
  /etc/init.d/iprouter.sh stop
  /etc/init.d/iprouter.sh start
else
    echo "Run script with start, restart, or stop"
fi