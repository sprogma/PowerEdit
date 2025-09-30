#ifndef MODIFICATIONS
#define MODIFICATIONS



#include "stddef.h"
#include "inttypes.h"

#include "buffer.h"



enum
{
    ModificationInsert,
    ModificationDelete,
};


struct modification
{
    uint64_t type;
    ssize_t pos;
    ssize_t len;
    char *text;
};


#endif
