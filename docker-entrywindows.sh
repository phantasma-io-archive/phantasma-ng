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

# Export the NUM_NODES variable for all services
export NUM_NODES
export LOCATION=$(pwd)

#docker-compose up -d --scale node=$NUM_NODES
docker-compose up -d