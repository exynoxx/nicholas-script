#!/bin/sh
# Self-hosting bootstrap: the F# ns builds nsc1 from nsc.ns; nsc1 builds nsc2; nsc2 builds nsc3.
# nsc2 and nsc3 are built by the NS-written compiler from the same source, so their IR must be identical.
set -e
cd "$(dirname "$0")"
mkdir -p build
cp ../runtime.c build/
ns build nsc.ns -o build/nsc1
build/nsc1 nsc.ns -o build/nsc2
build/nsc2 nsc.ns -o build/nsc3
cmp build/nsc2.ll build/nsc3.ll && echo "fixed point: nsc2 and nsc3 are identical"
