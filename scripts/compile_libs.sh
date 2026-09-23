#!/bin/bash

cd "$(dirname "$(realpath "$0")")/../SubrosaWebIntercept" || exit 1
mkdir -p ../build/bin/libs

echo "Compiling Linux library..."
g++ -shared -fPIC src/linux_intercept.c -o ../build/bin/libs/hook.so -ldl

echo "Compiling Windows library..."
x86_64-w64-mingw32-g++ \
    -shared \
    src/windows_intercept.cpp \
    src/wsock32.def \
    -o ../build/bin/libs/WSOCK32.dll \
    -lws2_32 \
    -static \
    -static-libgcc \
    -static-libstdc++

echo "Generating intercept.cfg..."
echo "127.0.0.1" > ../build/bin/libs/intercept.cfg
echo "80" >> ../build/bin/libs/intercept.cfg