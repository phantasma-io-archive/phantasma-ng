#!/bin/bash
# Setup Node
TROOT=`pwd`
NODENAME="node"
TMHOME=$TROOT/$NODENAME/
TENDERMINTPATH=./tendermint/tendermint # Change this to `./tendermint/tendermint

# Setup node if not setuped previously
DIR="./node/data/"
if [ -d "$DIR" ]; then
  echo "Node already configured"
else
  $TENDERMINTPATH --home "$(pwd)/$NODENAME/" init
fi

# start tendermint
# --proxy_app "tcp://127.0.0.1:26558" is used to setup the proxy_app (Phantasma.Node) ip address / port that is going to list to
# --p2p.seeds "node_ID@ip_address:node_port" -> can be used as a parameter to provide the seeds other way is going into the config.toml file.
$TENDERMINTPATH --home "$(pwd)/$NODENAME/" node