#!/bin/bash
 
########################################################
# Generates xml for pasting into 
# a Brunet network configuration file
########################################################

NUM_NODES_PM=$1; #Number of plab nodes per machine
LIST=$2;

  echo -e "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r"
  echo -e "<NetworkConfiguration xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">\r"
  echo -e "  <Nodes>\r"

if (( $NUM_NODES_PM <= 10 )); then 
for node in $(< ${LIST}); do
  for ((  i = 0 ;  i < $NUM_NODES_PM;  i++  )); do 
    echo -e "    <Node>\r"
    echo -e "      <TransportAddresses>\r"
    echo -e "        <TransportAddress>\r"
    echo -e "          <Protocol>tcp</Protocol>\r"
    echo -e "          <Address>$node</Address>\r"
    echo -e "          <Port>2500""$i""</Port>\r"
    echo -e "        </TransportAddress>\r"
    echo -e "      </TransportAddresses>\r"  
    echo -e "    </Node>\r"  
  done
done
elif (( $NUM_NODES_PM <= 100 )); then 
for node in $(< ${LIST}); do
  for ((  i = 0 ;  i < 10;  i++  )); do 
    echo -e "    <Node>\r"
    echo -e "      <TransportAddresses>\r"
    echo -e "        <TransportAddress>\r"
    echo -e "          <Protocol>tcp</Protocol>\r"
    echo -e "          <Address>$node</Address>\r"
    echo -e "          <Port>2500""$i""</Port>\r"
    echo -e "        </TransportAddress>\r"
    echo -e "      </TransportAddresses>\r"  
    echo -e "    </Node>\r"  
  done
  for ((  i = 10 ;  i < $NUM_NODES_PM;  i++  )); do 
    echo -e "    <Node>\r"
    echo -e "      <TransportAddresses>\r"
    echo -e "        <TransportAddress>\r"
    echo -e "          <Protocol>tcp</Protocol>\r"
    echo -e "          <Address>$node</Address>\r"
    echo -e "          <Port>250""$i""</Port>\r"
    echo -e "        </TransportAddress>\r"
    echo -e "      </TransportAddresses>\r"  
    echo -e "    </Node>\r"  
  done
done
else 
for node in $(< ${LIST}); do
  for ((  i = 0 ;  i < 10;  i++  )); do 
    echo -e "    <Node>\r"
    echo -e "      <TransportAddresses>\r"
    echo -e "        <TransportAddress>\r"
    echo -e "          <Protocol>tcp</Protocol>\r"
    echo -e "          <Address>$node</Address>\r"
    echo -e "          <Port>2500""$i""</Port>\r"
    echo -e "        </TransportAddress>\r"
    echo -e "      </TransportAddresses>\r"  
    echo -e "    </Node>\r"  
  done
  for ((  i = 10 ;  i < 100;  i++  )); do 
    echo -e "    <Node>\r"
    echo -e "      <TransportAddresses>\r"
    echo -e "        <TransportAddress>\r"
    echo -e "          <Protocol>tcp</Protocol>\r"
    echo -e "          <Address>$node</Address>\r"
    echo -e "          <Port>250""$i""</Port>\r"
    echo -e "        </TransportAddress>\r"
    echo -e "      </TransportAddresses>\r"  
    echo -e "    </Node>\r"  
  done
    for ((  i = 100 ;  i < $NUM_NODES_PM;  i++  )); do 
    echo -e "    <Node>\r"
    echo -e "      <TransportAddresses>\r"
    echo -e "        <TransportAddress>\r"
    echo -e "          <Protocol>tcp</Protocol>\r"
    echo -e "          <Address>$node</Address>\r"
    echo -e "          <Port>25""$i""</Port>\r"
    echo -e "        </TransportAddress>\r"
    echo -e "      </TransportAddresses>\r"  
    echo -e "    </Node>\r"  
  done
done
fi

 echo -e "  </Nodes>\r"
 echo -e "</NetworkConfiguration>\r"
