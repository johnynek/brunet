#!/bin/bash
source /etc/ipop.vpn.config

function get_pid()
{
  ps uax | grep $1 | grep -v grep | grep -v get_pid | awk -F" " {'print $2'} | grep -oE "[0-9]+"
}

function trace()
{
  pid=`get_pid P2PNode.exe`
  if [[ $pid ]]; then
    kill -USR2 $pid
  fi
}

function stop()
{
  echo "Stopping IPOP Bootstrap..."

  pid=`get_pid P2PNode.exe`

  if [[ $pid ]]; then
    kill -SIGINT $pid &> /dev/null

    while [[ `get_pid P2PNode.exe` ]]; do
      sleep 5
      kill -KILL $pid &> /dev/null
    done
  fi

  echo "Stopped IPOP Bootstrap..."
}

function start()
{
  pid=`get_pid P2PNode.exe`
  if [[ $pid ]]; then
    echo "IPOP Bootstrap Already running..."
    return 1
  fi

  echo "Starting IPOP Bootstrap..."

  if [[ "$USER" ]]; then
    chown -R $USER $DIR/etc
    chown -R $USER $DIR/var
    user="--user="$USER
    tunctl_user="-u "$USER
    if [[ "$GROUP" ]]; then
      group="--group="$GROUP
    fi
  fi

  cd $DIR/bin &> /dev/null
  export MONO_PATH=$MONO_PATH:$DIR/lib
#trace is only enabled to help find bugs, to use it execute groupvpn.sh trace
  python daemon.py $user $group "mono --trace=disabled P2PNode.exe -c 10 -n $DIR/etc/bootstrap.config 2>&1 | cronolog --period=\"1 day\" --symlink=$DIR/var/bootstraplog $DIR/var/bootstrap.log.%y%m%d"
  cd - &> /dev/null
  pid=`get_pid P2PNode.exe`
  if [[ ! $pid ]]; then
    sleep 5
    pid=`get_pid P2PNode.exe`
  fi

  if [[ ! $pid ]]; then
    echo "Error starting IPOP Bootstrap!"
    exit
  fi

# setup logging
  ln -sf $DIR/var/ipoplog /var/log/ipop
  echo "Started IPOP Bootstrap..."
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
