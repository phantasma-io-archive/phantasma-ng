#!/bin/bash
# reset all nodes 
TROOT=`pwd`
DIR="$TROOT/testnet/node0/data/"
if [ -d "$DIR" ]; then
  # Take action if $DIR exists. #
  echo "Tendermint run..."
else
  TMHOME="$TROOT"/testnet/node0 tendermint unsafe-reset-all
  TMHOME="$TROOT"/testnet/node1 tendermint unsafe-reset-all
  TMHOME="$TROOT"/testnet/node2 tendermint unsafe-reset-all
  TMHOME="$TROOT"/testnet/node3 tendermint unsafe-reset-all
fi

# Clear old screens
screen -ls | grep '(Detached)' | awk '{print $1}' | xargs -I % -t screen -X -S % quit
screen -wipe

# start all tendermint sessions
screen -S node0 -dm bash -c 'TMHOME="$(pwd)"/testnet/node0 tendermint node; exec sh'
screen -S node1 -dm bash -c 'TMHOME="$(pwd)"/testnet/node1 tendermint node; exec sh'
screen -S node2 -dm bash -c 'TMHOME="$(pwd)"/testnet/node2 tendermint node; exec sh'
screen -S node3 -dm bash -c 'TMHOME="$(pwd)"/testnet/node3 tendermint node; exec sh'

screen -S node0p -dm bash -c 'cd "$(pwd)"/testnet/node0/publish/; ./phantasma-node --urls "http://*:5101"; exec sh'
screen -S node1p -dm bash -c 'cd "$(pwd)"/testnet/node1/publish/; ./phantasma-node --urls "http://*:5102"; exec sh'
screen -S node2p -dm bash -c 'cd "$(pwd)"/testnet/node2/publish/; ./phantasma-node --urls "http://*:5103"; exec sh'
screen -S node3p -dm bash -c 'cd "$(pwd)"/testnet/node3/publish/; ./phantasma-node --urls "http://*:5104"; exec sh'

/bin/bash #screen -rd node0p