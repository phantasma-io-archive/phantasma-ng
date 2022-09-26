#!/bin/bash

#VERSION=0.35.6
VERSION=0.34.21
TESTNET_ROOT='./DOCKER/testnet'
PUBLISH_ROOT='./Phantasma.Node/bin/Debug/net6.0/linux-x64/publish/'
NODE_PROJ='Phantasma.Node/Phantasma.Node.csproj'
LAST_COMMIT=`git rev-parse --short HEAD`

curl -LJO https://github.com/tendermint/tendermint/releases/download/v"$VERSION"/tendermint_"$VERSION"_linux_amd64.tar.gz -s -o tendermint_"$VERSION"_linux_amd64.tar.gz

mkdir -p DOCKER/bin

tar -xzf tendermint_"$VERSION"_linux_amd64.tar.gz -C ./DOCKER/bin/

rm tendermint_"$VERSION"_linux_amd64.tar.gz

dotnet publish "$NODE_PROJ" --sc -r linux-x64

cp -r "$PUBLISH_ROOT" "$TESTNET_ROOT"/node0/
cp -r "$PUBLISH_ROOT" "$TESTNET_ROOT"/node1/
cp -r "$PUBLISH_ROOT" "$TESTNET_ROOT"/node2/
cp -r "$PUBLISH_ROOT" "$TESTNET_ROOT"/node3/

cp -r "$TESTNET_ROOT"/node0/config_node0.json "$TESTNET_ROOT"/node0/publish/config.json
cp -r "$TESTNET_ROOT"/node1/config_node1.json "$TESTNET_ROOT"/node1/publish/config.json
cp -r "$TESTNET_ROOT"/node2/config_node2.json "$TESTNET_ROOT"/node2/publish/config.json
cp -r "$TESTNET_ROOT"/node3/config_node3.json "$TESTNET_ROOT"/node3/publish/config.json

docker build -t phantasma-devnet -f DOCKER/Dockerfile .

rm -rf "$TESTNET_ROOT"/node0/publish
rm -rf "$TESTNET_ROOT"/node1/publish
rm -rf "$TESTNET_ROOT"/node2/publish
rm -rf "$TESTNET_ROOT"/node3/publish

docker tag phantasma-devnet:latest phantasmaio/phantasma-devnet:$LAST_COMMIT
