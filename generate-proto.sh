#!/usr/bin/env bash

# https://github.com/grpc/grpc/blob/master/examples/csharp/helloworld/generate_protos.bat

NUGET_PATH=$HOME/.nuget

# protoc -I=. -I=/home/merl/source/protobuf/protobuf -I=/home/merl/source/blockchain-tendermint-sample/src/Tendermint/proto -I=/home/merl/source/tendermint/proto \
#     -I=/home/merl/source/tendermint/third_party/proto \
#     --csharp_out Tendermint ./protos/types.proto --grpc_out Tendermint --plugin="protoc-gen-grpc=/home/merl/.nuget/packages/grpc.tools/2.37.1/tools/linux_x64/grpc_csharp_plugin"

protoc -I=. -I=/home/merl/source/protobuf/protobuf -I=/home/merl/source/tendermint/proto \
    -I=/home/merl/source/tendermint/third_party/proto \
    --csharp_out Tendermint/out/gogoproto \
    ./Tendermint/gogoproto/gogo.proto \
    --grpc_out Tendermint/out/crypto --plugin="protoc-gen-grpc=/home/merl/.nuget/packages/grpc.tools/2.38.1/tools/linux_x64/grpc_csharp_plugin"

protoc -I=. -I=/home/merl/source/protobuf/protobuf -I=/home/merl/source/tendermint/proto \
    -I=/home/merl/source/tendermint/third_party/proto \
    --csharp_out Tendermint/out/crypto \
    ./Tendermint/proto/tendermint/crypto/proof.proto \
    ./Tendermint/proto/tendermint/crypto/keys.proto \
    --grpc_out Tendermint/out/crypto --plugin="protoc-gen-grpc=/home/merl/.nuget/packages/grpc.tools/2.38.1/tools/linux_x64/grpc_csharp_plugin"

protoc -I=. -I=/home/merl/source/protobuf/protobuf -I=/home/merl/source/tendermint/proto \
    -I=/home/merl/source/tendermint/third_party/proto \
    --csharp_out Tendermint/out/types \
    ./Tendermint/proto/tendermint/types/types.proto \
    ./Tendermint/proto/tendermint/types/params.proto \
    --grpc_out Tendermint/out/types --plugin="protoc-gen-grpc=/home/merl/.nuget/packages/grpc.tools/2.38.1/tools/linux_x64/grpc_csharp_plugin"

protoc -I=. -I=/home/merl/source/protobuf/protobuf -I=/home/merl/source/tendermint/proto \
    -I=/home/merl/source/tendermint/third_party/proto \
    --csharp_out Tendermint/out/types \
    ./Tendermint/proto/tendermint/types/validator.proto \
    --grpc_out Tendermint/out/types --plugin="protoc-gen-grpc=/home/merl/.nuget/packages/grpc.tools/2.38.1/tools/linux_x64/grpc_csharp_plugin"

protoc -I=. -I=/home/merl/source/protobuf/protobuf -I=/home/merl/source/tendermint/proto \
    -I=/home/merl/source/tendermint/third_party/proto \
    --csharp_out Tendermint/out/abci \
    ./Tendermint/proto/tendermint/abci/types.proto \
    --grpc_out Tendermint/out/abci --plugin="protoc-gen-grpc=/home/merl/.nuget/packages/grpc.tools/2.38.1/tools/linux_x64/grpc_csharp_plugin"

protoc -I=. -I=/home/merl/source/protobuf/protobuf -I=/home/merl/source/tendermint/proto \
    -I=/home/merl/source/tendermint/third_party/proto \
    --csharp_out Tendermint/out/version \
    ./Tendermint/proto/tendermint/version/types.proto \
    --grpc_out Tendermint/out/version --plugin="protoc-gen-grpc=/home/merl/.nuget/packages/grpc.tools/2.38.1/tools/linux_x64/grpc_csharp_plugin"

# grep -rl "global::Gogoproto.GogoReflection.Descriptor," TestT/* | xargs sed -i 's/global::Gogoproto.GogoReflection.Descriptor, //g'
# grep -rl "global::Tendermint.Version.TypesReflection.Descriptor," TestT/* | xargs sed -i 's/global::Tendermint.Version.TypesReflection.Descriptor, //g'


