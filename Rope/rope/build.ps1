pushd $PSScriptRoot
gcc (ls *.c) -o msrope.dll -shared 
cp msrope.dll ..\
popd
