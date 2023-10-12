#!/bin/bash
NODE_PROJ='Phantasma.Node/src/Phantasma.Node.csproj'
PUBLISH_ROOT='./Phantasma.Node/src/bin/Debug/net6.0/linux-x64/publish/'

# Stop
docker-compose down

# Stop old containers
docker container stop phantasma-devnet
docker container rm phantasma-devnet

# Remove old images
echo y | docker image prune -a

rm -rf ./DOCKER/NodePublish/

# Out of the box, docker-compose does not support ARM64, so we need to use a custom build
dotnet build "$NODE_PROJ" -r linux-x64 #-o ./DOCKER/NodePublish/
dotnet publish "$NODE_PROJ" --sc -r linux-x64 #-o ./DOCKER/NodePublish/

mkdir -p ./DOCKER/NodePublish/
cp -R "$PUBLISH_ROOT"*  ./DOCKER/NodePublish/
mkdir -p ./DOCKER/NodePublish/Storage


# Set the default number of nodes if NUM_NODES is not provided
NUM_NODES=${NUM_NODES:-4}

# Export the NUM_NODES variable for all services
export NUM_NODES
export LOCATION=$(pwd)

#docker-compose up -d --scale node=$NUM_NODES
docker-compose up -d