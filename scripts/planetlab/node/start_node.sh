#!/bin/bash
PLAB_ACCOUNT=`whoami`

# starts 1 instance of SimpleNode.exe on the local PlanetLab node

export LD_LIBRARY_PATH=/lib:/usr/lib:/usr/local/lib:/home/$PLAB_ACCOUNT/node

echo "Attempting to start basicnode"
cd /home/$PLAB_ACCOUNT/node
export MONO_NO_SMP=1; /home/$PLAB_ACCOUNT/node/basicnode /home/$PLAB_ACCOUNT/node/node.config.$PLAB_ACCOUNT 2>&1 | /home/$PLAB_ACCOUNT/node/cronolog --period="1 day" /home/$PLAB_ACCOUNT/node/node.log.%y%m%d.txt &
# users will need to configure this themselves n server.py ... don't steal pre-allocated ports!
python /home/$PLAB_ACCOUNT/node/server.py `whoami` &
