#!/bin/bash

cd "$(dirname "$(realpath "$0")")" || exit 1

mkdir -p bin

echo "Compiling Linux library..."
g++ -shared -fPIC src/linux_intercept.c -o bin/hook.so -ldl

echo "Compiling Windows library..."
x86_64-w64-mingw32-g++ \
    -shared \
    src/windows_intercept.cpp \
    src/wsock32.def \
    -o bin/WSOCK32.dll \
    -lws2_32 \
    -static \
    -static-libgcc \
    -static-libstdc++