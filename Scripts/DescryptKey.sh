#!/bin/bash
# To execute this file use:
# ./DescryptKey.sh YOUR_PASSPHRASE YOUR_IV
# Read the passphrase and IV from the command line arguments
passphrase=$1
iv=$2

# Decrypt the address key using the passphrase and IV
openssl enc -aes-256-cbc -d -in keystore.enc -out keystore -pass "pass:$passphrase" -iv $iv