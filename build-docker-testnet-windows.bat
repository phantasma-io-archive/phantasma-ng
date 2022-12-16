@echo off

SET VERSION=0.34.21
SET TESTNET_ROOT=%CD%\DOCKER\testnet
SET PUBLISH_ROOT=%CD%\Phantasma.Node\bin\Debug\net6.0\linux-x64\publish\
SET NODE_PROJ=Phantasma.Node\Phantasma.Node.csproj
SET LAST_COMMIT=git rev-parse --short HEAD
for /f %%x in ('git rev-parse --short HEAD') do set LAST_COMMIT=%%x
curl "-LJO" "https:/\github.com\tendermint\tendermint\releases\download\v"%VERSION%"\tendermint_"%VERSION%"_linux_amd64.tar.gz" "-s" "-o" "tendermint_"%VERSION%"_linux_amd64.tar.gz"
mkdir "-p" "DOCKER\bin"
tar "-xzf" "tendermint_"%VERSION%"_linux_amd64.tar.gz" "-C" "%CD%\DOCKER\bin\"
DEL  "tendermint_"%VERSION%"_linux_amd64.tar.gz"
dotnet "publish" "%NODE_PROJ%" "--sc" "-r" "linux-x64"
mkdir "%TESTNET_ROOT%"\node0\publish\
mkdir "%TESTNET_ROOT%"\node1\publish\
mkdir "%TESTNET_ROOT%"\node2\publish\
mkdir "%TESTNET_ROOT%"\node3\publish\
COPY  "%PUBLISH_ROOT%" "%TESTNET_ROOT%"\node0\publish
COPY  "%PUBLISH_ROOT%" "%TESTNET_ROOT%"\node1\publish
COPY  "%PUBLISH_ROOT%" "%TESTNET_ROOT%"\node2\publish
COPY  "%PUBLISH_ROOT%" "%TESTNET_ROOT%"\node3\publish
COPY  "%TESTNET_ROOT%"\node0\config_node0.json "%TESTNET_ROOT%"\node0\publish\config.json
COPY  "%TESTNET_ROOT%"\node1\config_node1.json "%TESTNET_ROOT%"\node1\publish\config.json
COPY  "%TESTNET_ROOT%"\node2\config_node2.json "%TESTNET_ROOT%"\node2\publish\config.json
COPY  "%TESTNET_ROOT%"\node3\config_node3.json "%TESTNET_ROOT%"\node3\publish\config.json
docker build --platform=linux/x86_64 -t "phantasma-devnet" -f "DOCKER\DockerfileTestnet" .
docker tag "phantasma-devnet:latest" "phantasmachain/phantasma-devnet:%LAST_COMMIT%"