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
docker run --name phantasma-devnet -v $(pwd)/DOCKER/testnet:/app/testnet -tid -p 7077:7077 -p 7078:7078 -p 7079:7079 -p 7080:7080 -p 26056:26056 -p 26156:26156 -p 26256:26256 -p 26356:26356 -p 26057:26057 -p 26157:26157 -p 26257:26257 -p 26357:26357 phantasma-devnet