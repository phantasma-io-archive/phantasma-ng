#!/bin/bash

#VERSION=0.35.6
VERSION=0.34.21
TESTNET_ROOT='./DOCKER/testnet'
PUBLISH_ROOT='./Phantasma.Node/bin/Debug/net6.0/linux-x64/publish/'
NODE_PROJ='Phantasma.Node/Phantasma.Node.csproj'
LAST_COMMIT=`git rev-parse --short HEAD`

wget --no-check-certificate --content-disposition https://github.com/tendermint/tendermint/releases/download/v"$VERSION"/tendermint_"$VERSION"_linux_amd64.tar.gz 

mkdir -p DOCKER/bin

tar -xzf tendermint_"$VERSION"_linux_amd64.tar.gz -C DOCKER/bin/

rm tendermint_"$VERSION"_linux_amd64.tar.gz

dotnet publish "$NODE_PROJ" --sc -r linux-x64

mkdir -p "$TESTNET_ROOT"/node0/publish/
mkdir -p "$TESTNET_ROOT"/node1/publish/
mkdir -p "$TESTNET_ROOT"/node2/publish/
mkdir -p "$TESTNET_ROOT"/node3/publish/

cp -R "$PUBLISH_ROOT" "$TESTNET_ROOT"/node0/publish
cp -R "$PUBLISH_ROOT" "$TESTNET_ROOT"/node1/publish
cp -R "$PUBLISH_ROOT" "$TESTNET_ROOT"/node2/publish
cp -R "$PUBLISH_ROOT" "$TESTNET_ROOT"/node3/publish

cp -R "$TESTNET_ROOT"/node0/config_testnet_node0.json "$TESTNET_ROOT"/node0/publish/config.json
cp -R "$TESTNET_ROOT"/node1/config_testnet_node1.json "$TESTNET_ROOT"/node1/publish/config.json
cp -R "$TESTNET_ROOT"/node2/config_testnet_node2.json "$TESTNET_ROOT"/node2/publish/config.json
cp -R "$TESTNET_ROOT"/node3/config_testnet_node3.json "$TESTNET_ROOT"/node3/publish/config.json

docker build --platform=linux/x86_64 -t phantasma-devnet -f DOCKER/DockerfileTestnet .

docker tag phantasma-devnet:latest phantasmaio/phantasma-devnet:$LAST_COMMIT