#!/bin/bash
# reset all nodes 
TMHOME=/app/testnet/node0 testnet/tendermint unsafe-reset-all
TMHOME=/app/testnet/node1 testnet/tendermint unsafe-reset-all
TMHOME=/app/testnet/node2 testnet/tendermint unsafe-reset-all
TMHOME=/app/testnet/node3 testnet/tendermint unsafe-reset-all

# start all tendermint sessions
TMHOME=/app/testnet/node0 /app/testnet/tendermint node &
TMHOME=/app/testnet/node1 /app/testnet/tendermint node &
TMHOME=/app/testnet/node2 /app/testnet/tendermint node &
TMHOME=/app/testnet/node3 /app/testnet/tendermint node &

cd /app/testnet/node0/publish/; rm -rf Storage
./phantasma-node --urls "http://*:5101" &

cd /app/testnet/node1/publish/; rm -rf Storage 
./phantasma-node --urls "http://*:5102" &

cd /app/testnet/node2/publish/; rm -rf Storage 
./phantasma-node --urls "http://*:5103" &

cd /app/testnet/node3/publish/; rm -rf Storage 
./phantasma-node --urls "http://*:5104" &

wait -n

exit $?

