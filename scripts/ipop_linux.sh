#!/bin/bash
source /etc/ipop.vpn.config

function get_pid()
{
  ps uax | grep $1 | grep -v grep | grep -v get_pid | awk -F" " {'print $2'} | grep -oE "[0-9]+"
}

function trace()
{
  pid=`get_pid DhtIpopNode.exe`
  if [[ $pid ]]; then
    kill -USR2 $pid
  fi
}

function stop()
{
  echo "Stopping Grid Services..."

  dhcp_pid=`get_pid tapipop`
  if [[ $dhcp_pid ]]; then
    kill -KILL $dhcp_pid
  fi

  pid=`get_pid DhtIpopNode.exe`

  if [[ ! $pid ]]; then
    return 1
  fi

  kill -SIGINT $pid &> /dev/null

  while [[ `get_pid DhtIpopNode.exe` ]]; do
    sleep 5
    kill -KILL $pid &> /dev/null
  done

  echo "Shutting off the TAP device"
  if [[ -e /proc/sys/net/ipv4/neigh/tapipop ]]; then
    ./tunctl -d tapipop 2>1 | logger -t ipop
  fi
}

function start()
{
  echo "Starting IPOP..."
  pid=`get_pid DhtIpopNode.exe`
  if [[ $pid ]]; then
    echo "IPOP Already running..."
    return 1
  fi

  reguid=$uid
  if [[ ! "$uid" ]]; then
    reguid=$UID
    uid=\#$UID
  fi

  if [[ "$USE_IPOP_HOSTNAME" ]]; then
    #service will throw exceptions if we don't have a FQDN
    oldhostname=`hostname`
    hostname localhost
  fi

  if [[ ! -e /proc/sys/net/ipv4/neigh/tapipop ]]; then
    chmod 666 /dev/net/tun
    tunctl -t tapipop -u $reguid &> /dev/null
  fi

  cd $DIR/bin &> /dev/null
  ld_lib="LD_LIBRARY_PATH=$LD_LIBRARY_PATH:$DIR/lib"
  mono_lib="MONO_PATH=$MONO_PATH:$DIR/lib"
  chown -R $reguid $DIR/etc
  chown -R $reguid $DIR/var
#trace is only enabled to help find bugs, to use it execute ipop_linux.sh trace
  sudo -u $uid $ld_lib $mono_lib mono --trace=disabled DhtIpopNode.exe -n $DIR/etc/node.config -i $DIR/etc/ipop.config -d $DIR/etc/dhcp.config 2>&1 | sudo -u $uid cronolog --period="1 day" --symlink=$DIR/var/ipoplog $DIR/var/ipop.log.%y%m%d &
  cd - &> /dev/null
  pid=`get_pid DhtIpopNode.exe`
  if [[ ! $pid ]]; then
    sleep 5
    pid=`get_pid DhtIpopNode.exe`
  fi

  if [[ ! $pid ]]; then
    echo "Error starting IPOP!"
    exit
  fi

  renice -19 -p $pid &> /dev/null

  if [[ ! "$DHCP" ]]; then
    if [[ "`which dhclient3 2> /dev/null`" ]]; then
      DHCP=dhclient3
    elif [[ "`which dhcpcd 2> /dev/null`" ]]; then
      DHCP=dhcpcd
    else
      echo "No valid DHCP client"
      exit
    fi
  fi

  if [[ $DHCP == "dhclient3" ]]; then
    dhclient3 -pf /var/run/dhclient.$DEVICE.pid -lf /var/lib/dhcp3/dhclient.$DEVICE.leases $DEVICE
  elif [[ $DHCP == "dhcpcd" ]]; then
    dhcpcd $DEVICE
  else
    echo "No valid DHCP client"
    exit
  fi

# setup logging
  ln -sf $DIR/var/ipoplog /var/log/ipop
  echo "IPOP has started"
}

case "$1" in
  start)
    start
    ;;
  stop)
    stop
    ;;
  restart)
    stop
    start
    ;;
  trace)
    trace
    ;;
  *)
    echo "usage: start, stop, restart, trace"
  ;;
esac
exit 0
