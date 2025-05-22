#!/bin/sh

set -e

cd $(dirname $0)

DIR=../scylla-frontend/src/app/protocol

mkdir -p $DIR

mono bin/igorc.exe -d -v -t ts \
    -x 'gen_ts/*.cs' \
    -p igor/common \
    -p igor/data \
    -p igor/schema \
    -p igor/api \
    -o $DIR \
  '*.igor'

cp ts/igor.ts $DIR
