#!/bin/sh

set -e

cd $(dirname $0)

mono bin/igorc.exe -d -v -t postman \
    -set collection_name=\"`basename $PWD '-protocol'`\" \
    -x gen/postman.cs \
    -i igor/**/*.igor \
    -o . \
  igor/api/*.igor
