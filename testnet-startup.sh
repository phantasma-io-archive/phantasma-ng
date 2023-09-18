#!/bin/bash
#if [[ $(id -u) -ne 0 ]] ; then echo "Please run as root" ; exit 1 ; fi

# Stop old containers
docker container stop phantasma-devnet
docker container rm phantasma-devnet

# Remove old images
echo y | docker image prune -a

# Run the build script
chmod u+x ./build-docker-testnet.sh
./build-docker-testnet.sh

# Run the testnet
docker run --name phantasma-devnet -v $(pwd)/DOCKER/testnet:/app/testnet -tid -p 5102:5102 -p 5101:5101 -p 26057:26057 phantasma-devnet