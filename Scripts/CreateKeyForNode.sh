#!/bin/bash
# To execute this file use:
# ./CreateKeyForNode.sh YOUR_PASSPHRASE
# This will generate a new key and encrypt it using the passphrase
# And will also generate the IV file
# Generate a random initialization vector (IV)
iv=$(openssl rand -hex 16)

# Read the passphrase and IV from the command line arguments
passphrase=$1

# Encrypt the address key using the passphrase and IV
openssl enc -aes-256-cbc -e -in keystore -out keystore.enc -pass "pass:$passphrase" -iv $iv

# Save the IV to a file
echo $iv > iv.txt