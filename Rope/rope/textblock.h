#ifndef TEXTBLOCK
#define TEXTBLOCK

#include "stddef.h"

#include "export.h"
#include "buffer.h"
#include "modification.h"


struct textblock
{
    // TODO: mutex
    char *history_buffer;
    ssize_t history_buffer_alloc;

    char *string;
    ssize_t len;

    // struct texttree *tree;
};


int textblock_init(struct textblock *t);

int textblock_destroy(struct textblock *t);

int textblock_copy_version(struct textblock *t, ssize_t version);

int textblock_modificate(struct textblock *t, struct modification *mod, ssize_t version);

int textblock_get_size(struct textblock *t, ssize_t *size, ssize_t version);

int textblock_read(struct textblock *t, ssize_t from, ssize_t length, char *buffer, ssize_t version);


#endif
