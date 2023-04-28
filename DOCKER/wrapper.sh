#!/bin/bash
# reset all nodes 
TROOT=/app/testnet
DIR="/app/testnet/node0/data/"

if [ -d "$DIR" ]; then
  # Take action if $DIR exists. #
  echo "Tendermint run..."
else
  TMHOME=/app/testnet/node0 /app/bin/tendermint unsafe-reset-all
  TMHOME=/app/testnet/node1 /app/bin/tendermint unsafe-reset-all
  TMHOME=/app/testnet/node2 /app/bin/tendermint unsafe-reset-all
  TMHOME=/app/testnet/node3 /app/bin/tendermint unsafe-reset-all
fi

# Clear old screens
screen -ls |  grep 'node' | grep '(Detached)' | awk '{print $1}' | xargs -I % -t screen -X -S % quit
screen -wipe
#pkill -f "tendermint"

# Move config files
cp /app/testnet/node0/config_node0.json /app/testnet/node0/publish/config.json
cp /app/testnet/node1/config_node1.json /app/testnet/node1/publish/config.json
cp /app/testnet/node2/config_node2.json /app/testnet/node2/publish/config.json
cp /app/testnet/node3/config_node3.json /app/testnet/node3/publish/config.json

# start all tendermint sessions
screen -S node0p -dm bash -c 'cd /app/testnet/node0/publish/; ./phantasma-node; exec sh'
screen -S node1p -dm bash -c 'cd /app/testnet/node1/publish/; ./phantasma-node; exec sh'
screen -S node2p -dm bash -c 'cd /app/testnet/node2/publish/; ./phantasma-node; exec sh'
screen -S node3p -dm bash -c 'cd /app/testnet/node3/publish/; ./phantasma-node; exec sh'

#screen -rd node0p
#/bin/bash
tail -f /dev/null
