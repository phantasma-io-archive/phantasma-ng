#!/bin/bash
if [[ $(id -u) -ne 0 ]] ; then echo "Please run as root" ; exit 1 ; fi

# Stop old containers
docker container rm phantasma-devnet

# Remove old images
docker image prune -a

# Run the build script
chown u+x ./build-script.sh
./build-docker.sh

# Run the testnet
docker run --name phantasma-devnet -tid -p 5102:5102 -p 5101:5101 -p 26057:26057 phantasma-devnet