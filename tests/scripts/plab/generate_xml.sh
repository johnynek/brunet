#!/bin/bash
 
########################################################
# Generates xml for pasting into 
# a Brunet network configuration file
########################################################

LIST=$1;

  echo -e "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r"
  echo -e "<NetworkConfiguration xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">\r"
  echo -e "  <Nodes>\r"

for node in $(< ${LIST}); do
  echo -e "    <Node>\r"
  echo -e "      <TransportAddresses>\r"
  echo -e "        <TransportAddress>\r"
  echo -e "          <Protocol>tcp</Protocol>\r"
  echo -e "          <Address>$node</Address>\r"
  echo -e "          <Port>25010</Port>\r"
  echo -e "        </TransportAddress>\r"
  echo -e "      </TransportAddresses>\r"  
  echo -e "    </Node>\r"  
done;

for node in $(< ${LIST}); do
  echo -e "    <Node>\r"
  echo -e "      <TransportAddresses>\r"
  echo -e "        <TransportAddress>\r"
  echo -e "          <Protocol>tcp</Protocol>\r"
  echo -e "          <Address>$node</Address>\r"
  echo -e "          <Port>25020</Port>\r"
  echo -e "        </TransportAddress>\r"
  echo -e "      </TransportAddresses>\r"  
  echo -e "    </Node>\r"  
done;

for node in $(< ${LIST}); do
  echo -e "    <Node>\r"
  echo -e "      <TransportAddresses>\r"
  echo -e "        <TransportAddress>\r"
  echo -e "          <Protocol>tcp</Protocol>\r"
  echo -e "          <Address>$node</Address>\r"
  echo -e "          <Port>25030</Port>\r"
  echo -e "        </TransportAddress>\r"
  echo -e "      </TransportAddresses>\r"  
  echo -e "    </Node>\r"  
done;

for node in $(< ${LIST}); do
  echo -e "    <Node>\r"
  echo -e "      <TransportAddresses>\r"
  echo -e "        <TransportAddress>\r"
  echo -e "          <Protocol>tcp</Protocol>\r"
  echo -e "          <Address>$node</Address>\r"
  echo -e "          <Port>25040</Port>\r"
  echo -e "        </TransportAddress>\r"
  echo -e "      </TransportAddresses>\r"  
  echo -e "    </Node>\r"  
done;

for node in $(< ${LIST}); do
  echo -e "    <Node>\r"
  echo -e "      <TransportAddresses>\r"
  echo -e "        <TransportAddress>\r"
  echo -e "          <Protocol>tcp</Protocol>\r"
  echo -e "          <Address>$node</Address>\r"
  echo -e "          <Port>25050</Port>\r"
  echo -e "        </TransportAddress>\r"
  echo -e "      </TransportAddresses>\r"  
  echo -e "    </Node>\r"  
done;

for node in $(< ${LIST}); do
  echo -e "    <Node>\r"
  echo -e "      <TransportAddresses>\r"
  echo -e "        <TransportAddress>\r"
  echo -e "          <Protocol>tcp</Protocol>\r"
  echo -e "          <Address>$node</Address>\r"
  echo -e "          <Port>25060</Port>\r"
  echo -e "        </TransportAddress>\r"
  echo -e "      </TransportAddresses>\r"  
  echo -e "    </Node>\r"  
done;

for node in $(< ${LIST}); do
  echo -e "    <Node>\r"
  echo -e "      <TransportAddresses>\r"
  echo -e "        <TransportAddress>\r"
  echo -e "          <Protocol>tcp</Protocol>\r"
  echo -e "          <Address>$node</Address>\r"
  echo -e "          <Port>25070</Port>\r"
  echo -e "        </TransportAddress>\r"
  echo -e "      </TransportAddresses>\r"  
  echo -e "    </Node>\r"  
done;

for node in $(< ${LIST}); do
  echo -e "    <Node>\r"
  echo -e "      <TransportAddresses>\r"
  echo -e "        <TransportAddress>\r"
  echo -e "          <Protocol>tcp</Protocol>\r"
  echo -e "          <Address>$node</Address>\r"
  echo -e "          <Port>25080</Port>\r"
  echo -e "        </TransportAddress>\r"
  echo -e "      </TransportAddresses>\r"  
  echo -e "    </Node>\r"  
done;

for node in $(< ${LIST}); do
  echo -e "    <Node>\r"
  echo -e "      <TransportAddresses>\r"
  echo -e "        <TransportAddress>\r"
  echo -e "          <Protocol>tcp</Protocol>\r"
  echo -e "          <Address>$node</Address>\r"
  echo -e "          <Port>25090</Port>\r"
  echo -e "        </TransportAddress>\r"
  echo -e "      </TransportAddresses>\r"  
  echo -e "    </Node>\r"  
done;

for node in $(< ${LIST}); do
  echo -e "    <Node>\r"
  echo -e "      <TransportAddresses>\r"
  echo -e "        <TransportAddress>\r"
  echo -e "          <Protocol>tcp</Protocol>\r"
  echo -e "          <Address>$node</Address>\r"
  echo -e "          <Port>25100</Port>\r"
  echo -e "        </TransportAddress>\r"
  echo -e "      </TransportAddresses>\r"  
  echo -e "    </Node>\r"  
done;

  echo -e "  </Nodes>\r"
  echo -e "</NetworkConfiguration>\r"

