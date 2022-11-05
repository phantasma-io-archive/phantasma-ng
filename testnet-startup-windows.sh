#!/bin/bash

# Stop old containers
docker container stop phantasma-devnet
docker container rm phantasma-devnet

# Remove old images
echo y | docker image prune -a

# Run the build script
./build-docker-testnet-windows.sh

# Run the testnet
docker run --name phantasma-devnet -v $(pwd)/DOCKER/testnet:/app/testnet -tid -p 5102:5102 -p 5101:5101 -p 26057:26057 phantasma-devnet