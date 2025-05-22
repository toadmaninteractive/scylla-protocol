#!/bin/sh

mono bin/igorc.exe -v -schema \
    -p igor/sample \
    -o ./ \
  test-datetime.igor
