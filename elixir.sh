#!/bin/sh

set -e

cd $(dirname $0)

DIR=${1:-generated}

mkdir -p $DIR

mono bin/igorc.exe -d -v -t elixir \
  -set experimental_server=true \
  -set experimental_client=true \
    -x gen_elixir/gen_db.cs \
    -x gen_elixir/gen_example.cs \
    $(echo igor/* | sed 's!igor/!-I igor/!g') \
    -o $DIR \
  igor/**/*.igor

# mono bin/igorc.exe -d -v -t postman \
#     -x postman.cs \
#     -i igor/**/*.igor \
#     -o . \
#   igor/api/*.igor
