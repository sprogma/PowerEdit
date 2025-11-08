#ifndef TEXTBLOCK
#define TEXTBLOCK

#include "stddef.h"
#include "inttypes.h"

#include "export.h"
#include "buffer.h"
#include "modification.h"

#include "newline_splay.h"


struct textblock
{
    // TODO: mutex
    char   **history_buffer;
    int64_t *history_len_buffer;
    int64_t history_buffer_alloc;

    // struct texttree *tree;
    struct newline_tree *newline_tree;
};


int textblock_init(struct textblock *tb);

int textblock_destroy(struct textblock *tb);

int textblock_modificate(struct buffer *buf, struct textblock *tb, struct modification *mod, int64_t version);

int textblock_get_size(struct buffer * buf, struct textblock *tb, int64_t *size, int64_t version);

int textblock_read(struct buffer * buf, struct textblock *tb, int64_t from, int64_t length, char *buffer, int64_t version);


#endif


