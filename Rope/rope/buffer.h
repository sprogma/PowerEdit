#ifndef BUFFER
#define BUFFER

#include "stddef.h"
#include "node_allocator.h"


struct buffer
{
    struct textblock **blocks;
    size_t             blocks_len;
    size_t             blocks_alloc;
    size_t             avr_block_size;

    struct node_allocator allocator;

    /* metadata */
    // TODO: make file to load from + net connection, etc.

    // TODO: structure which stores positions of newlines
};



#endif
