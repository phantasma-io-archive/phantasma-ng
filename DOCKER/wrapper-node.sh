#!/bin/bash
# reset all nodes 
TROOT=/app/node
DIR="/app/node/tendermint/data/"

mkdir -p /app/node/phantasma-node/
cp -r /app/build/* /app/node/phantasma-node/

mkdir -p /app/node/tendermint/
cp -r /app/node/tendermint/config.json /app/node/phantasma-node/config.json

if [ -d "$DIR" ]; then
  # Take action if $DIR exists. #
  echo "Tendermint run..."
else
  TMHOME=/app/node/tendermint /app/bin/tendermint unsafe-reset-all
fi

cd /app/node/phantasma-node/
#dotnet phantasma-node.dll

# Keep it going.
tail -f /dev/null