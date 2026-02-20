#include "clocks.h"

#ifdef _WIN32
    #include "windows.h"
#endif

ptime_t get_time_us() {
#ifdef _WIN32
    FILETIME ft;
    GetSystemTimePreciseAsFileTime(&ft);
    int64_t t = ((int64_t)ft.dwHighDateTime << 32) | ft.dwLowDateTime;
    return (t - 116444736000000000LL) / 10;
#else
    struct timespec ts;
    clock_gettime(CLOCK_REALTIME, &ts);
    return (int64_t)ts.tv_sec * 1000000LL + (ts.tv_nsec / 1000LL);
#endif
}
