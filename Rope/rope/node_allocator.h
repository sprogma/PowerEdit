#ifndef NODE_ALLOCATOR
#define NODE_ALLOCATOR


#include "stddef.h"


/*
    free [len, alloc]
        - stores free allocated nodes
*/
struct node_allocator
{
    struct node **free;
    size_t        free_len;
    size_t        free_alloc;
};

#endif
