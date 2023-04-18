#!/bin/bash
# reset all nodes 
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

#remove screen
screen -ls | grep '(Detached)' | awk '{print $1}' | xargs -I % -t screen -X -S % quit
screen -wipe

# start all tendermint sessions
screen -S node0 -dm bash -c 'TMHOME=/app/testnet/node0 /app/bin/tendermint node; exec sh'
screen -S node1 -dm bash -c 'TMHOME=/app/testnet/node1 /app/bin/tendermint node; exec sh'
screen -S node2 -dm bash -c 'TMHOME=/app/testnet/node2 /app/bin/tendermint node; exec sh'
screen -S node3 -dm bash -c 'TMHOME=/app/testnet/node3 /app/bin/tendermint node; exec sh'

screen -S node0p -dm bash -c 'cd /app/testnet/node0/publish/; ./phantasma-node --urls "http://*:5101"; exec sh'
screen -S node1p -dm bash -c 'cd /app/testnet/node1/publish/; ./phantasma-node --urls "http://*:5102"; exec sh'
screen -S node2p -dm bash -c 'cd /app/testnet/node2/publish/; ./phantasma-node --urls "http://*:5103"; exec sh'
screen -S node3p -dm bash -c 'cd /app/testnet/node3/publish/; ./phantasma-node --urls "http://*:5104"; exec sh'

#screen -rd node0p
#/bin/bash
tail -f /dev/null
