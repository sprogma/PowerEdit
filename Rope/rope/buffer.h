#ifndef BUFFER
#define BUFFER

#include "stddef.h"
#include "node_allocator.h"
#include "inttypes.h"


struct cursor_t
{
    int64_t begin;
    int64_t end;
};

struct buffer
{
    int64_t           version;
    int64_t          *version_tree;
    int64_t          *version_depth;
    int64_t          *version_skiplist;
    struct cursor_t **version_cursors;
    size_t           *version_cursors_count;
    int64_t           version_tree_len;
    int64_t           version_tree_alloc;

    struct textblock **blocks;
    int64_t             blocks_len;
    int64_t             blocks_alloc;
    int64_t             avr_block_size;

    struct node_allocator allocator;

    /* metadata */
    // TODO: make file to load from + net connection, etc.

    // TODO: structure which stores positions of newlines
};



#endif


