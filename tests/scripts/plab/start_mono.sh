#!/bin/bash

######################################################
#
# Start the Brunet EchoTester app on a PlanetLab node
#
######################################################
NODE_UPTIME=108000
INTERVAL=5
NUM_ITER=$1
pid_file="/home/uclaee_brunet1/joe/pid.txt"
> $pid_file

ps u -C mono;
exit_code=$?;
if [[ $exit_code == 0 ]]; then
   killall -9 mono;
fi

ps u -C sleep;
exit_code=$?;
if [[ $exit_code == 0 ]]; then
   killall -9 sleep;
fi

if ! [ -d "data" ] ; then
   mkdir "data"
fi

for file in ./data/*; do
  if [[ -e $file ]]; then 
    rm $file
  fi
done

for ((  i = 0 ;  i < $NUM_ITER;  i++  ))
do 
  ./start_program.sh $i &
  sleep $INTERVAL
done
  
sleep $NODE_UPTIME && killall -9 mono &

exit
