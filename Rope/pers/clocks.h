#ifndef CLOCKS_H
#define CLOCKS_H

#include "inttypes.h"

typedef int64_t ptime_t;
ptime_t get_time_us();

#ifdef _WIN32
	#include <windows.h>
	#define msleep(ms) Sleep(ms)
#else
	#include <unistd.h>
	#define msleep(ms) usleep((ms) * 1000)
#endif

#endif
