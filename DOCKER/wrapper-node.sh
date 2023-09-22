#!/bin/bash
# reset all nodes 
TROOT=/app/node
DIR="/app/node/data/"

mkdir -p /app/node/data/
cp -r /app/build/* /app/node/data/

mkdir -p /app/node/config/
cp -r /app/node/config/config.json /app/node/config.json
cp -r /app/node/config/config.json /app/node/data/config.json

if [ -d "$DIR" ]; then
  # Take action if $DIR exists. #
  echo "Tendermint run..."
else
  TMHOME=/app/node/config /app/bin/tendermint unsafe-reset-all
fi

# Clear old screens
#screen -ls |  grep 'node' | grep '(Detached)' | awk '{print $1}' | xargs -I % -t screen -X -S % quit
#screen -wipe
#pkill -f "tendermint"

# Move config files
#cp /app/node/config/config_node.json /app/node/publish/config.json

# start all tendermint sessions
#screen -S node -dm bash -c 'cd /app/node/data/; ./phantasma-node; exec sh'

#screen -rd node0p
#/bin/bash
#tail -f /dev/null

cd /app/node/data/
dotnet phantasma-node.dll
