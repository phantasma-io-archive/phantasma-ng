#!/bin/bash
NODE_PROJ='Phantasma.Node/src/Phantasma.Node.csproj'
PUBLISH_ROOT='./Phantasma.Node/src/bin/Debug/net6.0/linux-x64/publish/'
VERSION=0.34.21
wget --no-check-certificate --content-disposition https://github.com/tendermint/tendermint/releases/download/v"$VERSION"/tendermint_"$VERSION"_linux_amd64.tar.gz 
mkdir -p DOCKER/bin
tar -xzf tendermint_"$VERSION"_linux_amd64.tar.gz -C DOCKER/bin/
rm tendermint_"$VERSION"_linux_amd64.tar.gz

# Stop
docker-compose down

# Stop old containers
docker container stop phantasma-devnet
docker container rm phantasma-devnet

# Remove old images
echo y | docker image prune -a

# Export the NUM_NODES variable for all services
export NUM_NODES
export LOCATION=$(pwd)

#docker-compose up -d --scale node=$NUM_NODES
docker-compose up -d