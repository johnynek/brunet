#!/bin/bash

ATTEMPTED_NODES_FILE=$1
PAR=500
DUMMY_FILE=/home/jskong/scripts/temp/dummy.txt
NO_MONO_FILE=/home/jskong/scripts/temp/no_mono.txt
INPUT_FILE=/home/jskong/scripts/temp/input.txt
MONO_FOLDER=/home/jskong/mono-1.0.1/
INSTALLMONO="if [[ ! -d mono-1.1.4 ]]; then mkdir mono-1.1.4; fi && if [[ -e mono-1.1.4.tgz ]]; then cp mono-1.1.4.tgz ./mono-1.1.4/ ; fi && cd mono-1.1.4 && gunzip mono-1.1.4.tgz && tar -xvf mono-1.1.4.tar && sudo ./install_mono.sh"

#./screenplnodesnew.py $ATTEMPTED_NODES_FILE > $INPUT_FILE

timeout 300 pssh -p $PAR -t 60 -h $ATTEMPTED_NODES_FILE -l uclaee_brunet1 -o /tmp/out/ -e /tmp/error/ which mono > $DUMMY_FILE

if [ -e $NO_MONO_FILE ]
then
 rm $NO_MONO_FILE
fi

while read word0 word1 word2; do
   if [ "$word0" == "Error" ]
      then echo "$word2" | awk 'BEGIN{ FS=":" } { print $1 }' >> $NO_MONO_FILE   
   fi
done < $DUMMY_FILE

#pscp -p $PAR -t 60 -e /tmp/error/ -o /tmp/out/ -l uclaee_brunet1 -h $NO_MONO_FILE -r $MONO_FOLDER /home/uclaee_brunet1/ 
timeout 300 ~/pldeploy $NO_MONO_FILE #> $DUMMY_FILE

#while read word0 word1 word2; do
#   if [ "$word1" == "failed" ]
#      then echo "$word0" >> $NO_MONO_FILE   
#   fi
#done < $DUMMY_FILE

timeout 500 pssh -p $PAR -t 60 -h $NO_MONO_FILE -l uclaee_brunet1 -o /tmp/out/ -e /tmp/error/ "$INSTALL_MONO" 
#~/plcmd INSTALLMONO $NO_MONO_FILE

echo Checking to see which nodes have just installed mono: 
#pssh -p $PAR -t 60 -h $ATTEMPTED_NODES_FILE -l uclaee_brunet1 -o /tmp/out/ -e /tmp/error/ which mono 
~/scripts/get_success_nodes $NO_MONO_FILE
