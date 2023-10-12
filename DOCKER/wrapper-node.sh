#!/bin/bash
# reset all nodes 

mkdir -p /app/node/phantasma-node/
cp -r /app/build/* /app/node/phantasma-node/
chmod -R +x /app/node/tendermint/

if [ -d "$DIR" ]; then
  # Take action if $DIR exists. #
  echo "Tendermint run..."
else
  TMHOME=/app/node/tendermint /app/bin/tendermint unsafe-reset-all
fi

mkdir -p /app/node/tendermint/
cp -r /app/node/tendermint/config.json /app/node/phantasma-node/config.json
cd /app/node/phantasma-node/
dotnet phantasma-node.dll

# Keep it going.
tail -f /dev/null