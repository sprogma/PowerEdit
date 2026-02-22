pushd $PSScriptRoot
clang (ls *.c -Exclude test*) -o msrope.dll -shared -g -D_CRT_SECURE_NO_WARNINGS -D_CRT_NONSTDC_NO_DEPRECATE -fms-extensions -Wno-microsoft
clang (ls *.c) -o a.exe -g -D_CRT_SECURE_NO_WARNINGS -D_CRT_NONSTDC_NO_DEPRECATE -fms-extensions -Wno-microsoft -fsanitize=address
cp msrope.dll ..\
cp msrope.pdb ..\
popd


