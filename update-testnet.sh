#!/bin/bash

TESTNET_ROOT='./DOCKER/testnet'
PUBLISH_ROOT='./Phantasma.Node/src/bin/Debug/net6.0/linux-x64/publish/'
NODE_PROJ='Phantasma.Node/src/Phantasma.Node.csproj'

dotnet publish "$NODE_PROJ" --sc -r linux-x64

mkdir "$TESTNET_ROOT"/node0/publish/
mkdir "$TESTNET_ROOT"/node1/publish/
mkdir "$TESTNET_ROOT"/node2/publish/
mkdir "$TESTNET_ROOT"/node3/publish/

docker stop phantasma-devnet 

cp -R "$PUBLISH_ROOT" "$TESTNET_ROOT"/node0/publish
cp -R "$PUBLISH_ROOT" "$TESTNET_ROOT"/node1/publish
cp -R "$PUBLISH_ROOT" "$TESTNET_ROOT"/node2/publish
cp -R "$PUBLISH_ROOT" "$TESTNET_ROOT"/node3/publish

docker start phantasma-devnet 