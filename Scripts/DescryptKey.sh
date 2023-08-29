#!/bin/bash
# To execute this file use:
# ./DescryptKey.sh YOUR_PASSPHRASE {/path/to/YOUR_IV}
# Read the passphrase and IV from the command line arguments
passphrase=$1
iv=`cat $2`

# Decrypt the address key using the passphrase and IV
openssl enc -md sha512 -pbkdf2 -iter 50000 -d -in keystore.enc -out keystore.key -pass "pass:$passphrase" -iv $iv