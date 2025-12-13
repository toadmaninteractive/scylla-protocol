#!/bin/sh

set -e

cd $(dirname $0)

DIR=${1:-generated}

mkdir -p $DIR

mono bin/igorc.exe -d -v -t elixir \
  -set experimental_server=true \
  -set experimental_client=true \
    -x gen_elixir/gen_example.cs \
    $(echo igor/* | sed 's!igor/!-I igor/!g') \
    -o $DIR \
  igor/**/*.igor

# replacements
sed -r -i 's|Logger\.warn\(|Logger.warning(|g' $DIR/*.ex
sed -r -i 's|, do: raise (.*)$|, do: raise(\1)|g' $DIR/*.ex

#    -x gen_elixir/gen_db.cs \

# mono bin/igorc.exe -d -v -t postman \
#     -x postman.cs \
#     -i igor/**/*.igor \
#     -o . \
#   igor/api/*.igor
