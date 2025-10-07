#ifndef BUFFER
#define BUFFER

#include "stddef.h"
#include "node_allocator.h"


struct buffer
{
    ssize_t  version;
    ssize_t *version_tree;
    ssize_t *version_depth;
    ssize_t *version_skiplist;
    ssize_t  version_tree_len;
    ssize_t  version_tree_alloc;

    struct textblock **blocks;
    ssize_t             blocks_len;
    ssize_t             blocks_alloc;
    ssize_t             avr_block_size;

    struct node_allocator allocator;

    /* metadata */
    // TODO: make file to load from + net connection, etc.

    // TODO: structure which stores positions of newlines
};



#endif
