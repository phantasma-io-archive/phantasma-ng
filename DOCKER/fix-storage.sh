#!/bin/bash
rm -rf testnet/node0/publish/Storage
rm -rf testnet/node1/publish/Storage
rm -rf testnet/node2/publish/Storage
rm -rf testnet/node3/publish/Storage

rm -rf testnet/node0/publish/Dumps
rm -rf testnet/node1/publish/Dumps
rm -rf testnet/node2/publish/Dumps
rm -rf testnet/node3/publish/Dumps

rm -rf testnet/node0/data/
rm -rf testnet/node1/data/
rm -rf testnet/node2/data/
rm -rf testnet/node3/data/

rm -rf testnet/node0/config/write-file-atomic*
rm -rf testnet/node1/config/write-file-atomic*
rm -rf testnet/node2/config/write-file-atomic*
rm -rf testnet/node3/config/write-file-atomic*

rm -rf testnet/node0/config/addrbook.json
rm -rf testnet/node1/config/addrbook.json
rm -rf testnet/node2/config/addrbook.json
rm -rf testnet/node3/config/addrbook.json

rm -rf testnet/node0/config/write-file-atomic*
rm -rf testnet/node1/config/write-file-atomic*
rm -rf testnet/node2/config/write-file-atomic*
rm -rf testnet/node3/config/write-file-atomic*

#cp -R ./Storage testnet/node0/publish
#cp -R ./Storage testnet/node1/publish
#cp -R ./Storage testnet/node2/publish
#cp -R ./Storage testnet/node3/publish

chmod -R 777 testnet/node0/publish/Storage
chmod -R 777 testnet/node1/publish/Storage
chmod -R 777 testnet/node2/publish/Storage
chmod -R 777 testnet/node3/publish/Storage