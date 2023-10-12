#!/bin/bash
# To create an env variable temporarly, use:
# export PASSPHRASE=YOUR_PASSPHRASE
# export IV=/path/to/YOUR_IV
# To create an env variable permanently, use:
# echo "export PASSPHRASE=YOUR_PASSPHRASE" >> ~/.bashrc
# source ~/.bashrc
# To remove an env variable, use:
# unset PASSPHRASE
# To list all env variables, use:
# env
# To list all env variables with their values, use:
# set
# To list all env variables with their values, use:
# printenv
# Read the passphrase and IV from environment variables
passphrase=$PASSPHRASE
iv=`cat $IV`

# Decrypt the keystore file using the passphrase and IV
openssl enc -md sha512 -pbkdf2 -iter 50000 -d -in keystore.enc -out keystore.key -pass "pass:$passphrase" -iv $iv
LOCALKEY=$(<keystore)

# Delete the decrypted keystore file
rm keystore.key

# Start the node using the decrypted keystore file
dotnet Phantasma.Node.dll --tendermint.key="$LOCALKEY"


