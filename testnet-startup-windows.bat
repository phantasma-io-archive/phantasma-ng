@echo off

docker container "stop" "phantasma-devnet"
docker container "rm" "phantasma-devnet"
echo "y" | docker image prune -a
call build-docker-testnet-windows.bat
docker run --name "phantasma-devnet" -v "%cd%\DOCKER\testnet:/app/testnet" -tid -p "5102:5102" -p "5101:5101" -p "26057:26057" phantasma-devnet