#include "structure.h"
#include "text_api.h"

typedef void (*LogCallback)(enum LogLevel level, const char* message);
LogCallback CsLogger;

void SetLogger(LogCallback callback)
{
    CsLogger = callback;
}

__attribute__((format(printf, 2, 3)))
void Log(enum LogLevel level, const char* format, ...) 
{
    if (CsLogger == NULL) return;

    char buffer[4096];
    va_list args;

    va_start(args, format);
    vsnprintf(buffer, sizeof(buffer), format, args);
    va_end(args);

    CsLogger(level, buffer);
}
