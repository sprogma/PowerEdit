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
    size_t position;
    size_t length;
    char *text;
};


#endif
