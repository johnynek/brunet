#!/bin/bash
pid_file="/home/uclaee_brunet1/joe/pid.txt"

i=$1  #instance of the mono program
port_base=25

if (($i < 10 )); then
  #echo -n "Start_time : " > "./data/alive_period2500""$i"".log" && date -u >> "./data/alive_period2500""$i"".log"
  nohup mono StructureTester.exe TestNetwork.brunet "$i" < /dev/null > /dev/null &
  #nohup mono StructureTester.exe TestNetwork.brunet "$i" < /dev/null > "./data/console""$port_base""00""$i"".log" 
  #nohup mono EchoTester.exe TestNetwork.brunet "$i" < /dev/null > /dev/null &
  pid=$!
  echo $i $pid >> $pid_file
  wait $pid;
  read word0 word1 word2 < "./data/brunetadd2500""$i"".log" 
  str=`date -u +%m/%d/%Y\ %H:%M:%S:%N` && echo ${str:0:23}  deletion  deletion  $word1 >> "./data/brunetadd2500""$i"".log" 
  #echo -n "End_time : " >> "./data/alive_period2500""$i"".log" && date -u >> "./data/alive_period2500""$i"".log"
elif (($i < 100 )); then
  #echo -n "Start_time : " > "./data/alive_period250""$i"".log" && date -u >> "./data/alive_period250""$i"".log" &
  nohup mono StructureTester.exe TestNetwork.brunet "$i" < /dev/null > /dev/null &
  #nohup mono StructureTester.exe TestNetwork.brunet "$i" < /dev/null > "./data/console""$port_base""0""$i"".log" 
  #nohup mono EchoTester.exe TestNetwork.brunet "$i" < /dev/null > /dev/null &
  pid=$!
  echo $i $pid >> $pid_file
  wait $pid;
  read word0 word1 word2 < "./data/brunetadd250""$i"".log" 
  str=`date -u +%m/%d/%Y\ %H:%M:%S:%N` && echo ${str:0:23}  deletion  deletion  $word1 >> "./data/brunetadd250""$i"".log" 
  #echo -n "End_time : " >> "./data/alive_period250""$i"".log" && date -u >> "./data/alive_period250""$i"".log"
else 
  #echo -n "Start_time : " > "./data/alive_period25""$i"".log" && date -u >> "./data/alive_period25""$i"".log" &
  nohup mono StructureTester.exe TestNetwork.brunet "$i" < /dev/null > /dev/null &
  #nohup mono StructureTester.exe TestNetwork.brunet "$i" < /dev/null > "./data/console""$port_base""$i"".log" 
  #nohup mono EchoTester.exe TestNetwork.brunet "$i" < /dev/null > /dev/null &
  pid=$!
  echo $i $pid >> $pid_file
  wait $pid;
  read word0 word1 word2 < "./data/brunetadd25""$i"".log" 
  str=`date -u +%m/%d/%Y\ %H:%M:%S:%N` && echo ${str:0:23}  deletion  deletion  $word1 >> "./data/brunetadd25""$i"".log" 
  #echo -n "End_time : " >> "./data/alive_period25""$i"".log" && date -u >> "./data/alive_period25""$i"".log"
fi

exit
