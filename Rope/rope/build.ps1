pushd $PSScriptRoot
clang (ls *.c) -o msrope.dll -shared -g -D_CRT_SECURE_NO_WARNINGS -D_CRT_NONSTDC_NO_DEPRECATE
cp msrope.dll ..\
cp msrope.pdb ..\
popd


