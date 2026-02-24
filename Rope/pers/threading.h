#ifndef THREADING_H
#define THREADING_H


#include "inttypes.h"


#ifdef _WIN32
    #include "windows.h"
    typedef SRWLOCK lock_t;
    typedef HANDLE thread_t;
    #define lockExclusive(x) AcquireSRWLockExclusive(x)
    #define freeExclusive(x) ReleaseSRWLockExclusive(x)
    #define lockShared(x) AcquireSRWLockShared(x)
    #define freeShared(x) ReleaseSRWLockShared(x)
    static inline thread_t StartNewThread(int32_t (*fn)(void *), void *param)
    {
        return CreateThread(NULL, 0, (LPTHREAD_START_ROUTINE)fn, param, 0, NULL);
    }
#else
    #error TODO threading not on windows
#endif



#endif
