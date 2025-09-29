#ifndef TEXTBLOCK
#define TEXTBLOCK

#include "stddef.h"

#include "export.h"
#include "buffer.h"


struct textblock
{
    // TODO: mutex

    struct texttree *tree;
};



#endif
