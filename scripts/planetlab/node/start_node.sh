#!/bin/bash
PLAB_ACCOUNT=`whoami`

# starts 1 instance of SimpleNode.exe on the local PlanetLab node

export LD_LIBRARY_PATH=/lib:/usr/lib:/usr/local/lib:/home/$PLAB_ACCOUNT/node

nn=`ps -ef --width 1000 | grep deetoonode | grep -v grep | wc -l`
if [ $nn -gt 0 ]
then
   echo "brunet nodes already running... " 
   # This needs to be changed to a more intelligent method, but for now, its what I'm going with
#   if [ `date +'%k'` > 1 && ! -f /home/$PLAB_ACCOUNT/node/node.log.`date +'%y%m%d'` ]; then
     echo "And it really is running!"
     exit
#  fi
  log="/home/$PLAB_ACCOUNT/node/kill.log.%y%m%d"
  echo "Killing node..." >> $log
  uptime >> $log
  free >> $log
  date >> $log
  echo "but apparently it is CRAP!"
  pkill -KILL deetoonode
fi

for i in `ps uax | grep "monitor.sh" | grep -v grep | awk -F" " '{print $2}'`; do
  kill -KILL $i
done

echo "Attempting to start deetoonode"
cd /home/$PLAB_ACCOUNT/node
export MONO_NO_SMP=1; /home/$PLAB_ACCOUNT/node/deetoonode /home/$PLAB_ACCOUNT/node/deetoo_cache.config /home/$PLAB_ACCOUNT/node/deetoo_query.config 2>&1 | /home/$PLAB_ACCOUNT/node/cronolog --period="1 day" /home/$PLAB_ACCOUNT/node/node.log.%y%m%d.txt &
#python /home/$PLAB_ACCOUNT/node/server.py `whoami` &
