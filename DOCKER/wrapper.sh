#! /bin/bash
# reset all nodes 
TMHOME=/app/testnet/node0 testnet/tendermint unsafe-reset-all
TMHOME=/app/testnet/node1 testnet/tendermint unsafe-reset-all
TMHOME=/app/testnet/node2 testnet/tendermint unsafe-reset-all
TMHOME=/app/testnet/node3 testnet/tendermint unsafe-reset-all

# start all tendermint sessions
screen -S node0 -dm bash -c 'TMHOME=/app/testnet/node0 /app/testnet/tendermint node; exec sh'
screen -S node1 -dm bash -c 'TMHOME=/app/testnet/node1 /app/testnet/tendermint node; exec sh'
screen -S node2 -dm bash -c 'TMHOME=/app/testnet/node2 /app/testnet/tendermint node; exec sh'
screen -S node3 -dm bash -c 'TMHOME=/app/testnet/node3 /app/testnet/tendermint node; exec sh'

screen -S node0p -dm bash -c 'cd /app/testnet/node0/publish/; rm -rf Storage; ./phantasma-node --urls "http://*:5101"; exec sh'
screen -S node1p -dm bash -c 'cd /app/testnet/node1/publish/; rm -rf Storage; ./phantasma-node --urls "http://*:5102"; exec sh'
screen -S node2p -dm bash -c 'cd /app/testnet/node2/publish/; rm -rf Storage; ./phantasma-node --urls "http://*:5103"; exec sh'
screen -S node3p -dm bash -c 'cd /app/testnet/node3/publish/; rm -rf Storage; ./phantasma-node --urls "http://*:5104"; exec sh'

/bin/bash #screen -rd node0p
