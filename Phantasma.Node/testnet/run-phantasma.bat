
cd .\node1\net6.0
rmdir /s /q Storage
START  .\phantasma-node.exe  --urls http://127.0.0.1:50100

cd ..\..\
cd .\node2\net6.0
rmdir /s /q Storage
START  .\phantasma-node.exe  --urls http://127.0.0.1:50101

cd ..\..\
cd .\node3\net6.0
rmdir /s /q Storage
START   .\phantasma-node.exe  --urls http://127.0.0.1:50102