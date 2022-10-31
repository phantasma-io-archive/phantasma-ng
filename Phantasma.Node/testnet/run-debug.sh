TESTNET_ROOT=$(pwd)
PUBLISH_ROOT=$(pwd)/../bin/Debug/net6.0/publish/
# reset all nodes 
TMHOME=node0 ./tendermint unsafe-reset-all

# kill all tendermint sessions
screen -ls | grep node0 | cut -d. -f1 | awk '{print $1}' | xargs kill
pkill tendermint
killall tendermint

# start all tendermint sessions
screen -S node0 -dm bash -c 'TMHOME=node0 ./tendermint node; exec sh'
screen -S node1 -dm bash -c 'TMHOME=node1 ./tendermint node; exec sh'
screen -S node2 -dm bash -c 'TMHOME=node2 ./tendermint node; exec sh'
screen -S node3 -dm bash -c 'TMHOME=node3 ./tendermint node; exec sh'

cd $TESTNET_ROOT
cd ..
dotnet publish

mkdir -p "$TESTNET_ROOT"/node0/publish/

cp -R "$PUBLISH_ROOT" "$TESTNET_ROOT"/node0/publish/

cp -R "$TESTNET_ROOT"/node0/config_node0.json "$TESTNET_ROOT"/node0/publish/config.json 

#screen -S node0p -dm bash -c 'cd /home/merl/source/phantasma/new/phantasma-ng/Phantasma.Node/testnet/node0/publish/; dotnet phantasma-node.dll --urls "http://localhost:5101"; exec sh'
