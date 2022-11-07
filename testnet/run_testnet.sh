TESTNET_ROOT='/home/merl/source/phantasma/new/phantasma-ng/Phantasma.Node/testnet'
PUBLISH_ROOT='/home/merl/source/phantasma/new/phantasma-ng/Phantasma.Node/bin/Debug/net6.0/publish/'
# reset all nodes 
TMHOME=node0 ./tendermint unsafe-reset-all
TMHOME=node1 ./tendermint unsafe-reset-all
TMHOME=node2 ./tendermint unsafe-reset-all
TMHOME=node3 ./tendermint unsafe-reset-all

# kill all tendermint sessions
pkill screen
killall screen
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

cp -r "$PUBLISH_ROOT" "$TESTNET_ROOT"/node0/
cp -r "$PUBLISH_ROOT" "$TESTNET_ROOT"/node1/
cp -r "$PUBLISH_ROOT" "$TESTNET_ROOT"/node2/
cp -r "$PUBLISH_ROOT" "$TESTNET_ROOT"/node3/

cp -r "$TESTNET_ROOT"/node0/config_node0.json "$TESTNET_ROOT"/node0/publish/config.json 
cp -r "$TESTNET_ROOT"/node1/config_node1.json "$TESTNET_ROOT"/node1/publish/config.json
cp -r "$TESTNET_ROOT"/node2/config_node2.json "$TESTNET_ROOT"/node2/publish/config.json
cp -r "$TESTNET_ROOT"/node3/config_node3.json "$TESTNET_ROOT"/node3/publish/config.json

rm -rf /home/merl/source/phantasma/new/phantasma-ng/Phantasma.Node/testnet/node0/publish/Storage
rm -rf /home/merl/source/phantasma/new/phantasma-ng/Phantasma.Node/testnet/node1/publish/Storage
rm -rf /home/merl/source/phantasma/new/phantasma-ng/Phantasma.Node/testnet/node2/publish/Storage
rm -rf /home/merl/source/phantasma/new/phantasma-ng/Phantasma.Node/testnet/node3/publish/Storage

screen -S node0p -dm bash -c 'cd /home/merl/source/phantasma/new/phantasma-ng/Phantasma.Node/testnet/node0/publish/; dotnet phantasma-node.dll --urls "http://localhost:5101"; exec sh'
screen -S node1p -dm bash -c 'cd /home/merl/source/phantasma/new/phantasma-ng/Phantasma.Node/testnet/node1/publish/; dotnet phantasma-node.dll --urls "http://localhost:5102"; exec sh'
screen -S node2p -dm bash -c 'cd /home/merl/source/phantasma/new/phantasma-ng/Phantasma.Node/testnet/node2/publish/; dotnet phantasma-node.dll --urls "http://localhost:5103"; exec sh'
screen -S node3p -dm bash -c 'cd /home/merl/source/phantasma/new/phantasma-ng/Phantasma.Node/testnet/node3/publish/; dotnet phantasma-node.dll --urls "http://localhost:5104"; exec sh'
