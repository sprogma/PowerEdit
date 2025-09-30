#ifndef TEXTBLOCK
#define TEXTBLOCK

#include "stddef.h"

#include "export.h"
#include "buffer.h"
#include "modification.h"


struct textblock
{
    // TODO: mutex

    char *string;
    ssize_t len;

    // struct texttree *tree;
};


int textblock_init(struct textblock *t);

int textblock_destroy(struct textblock *t);

int textblock_modificate(struct textblock *t, struct modification *mod);

int textblock_get_size(struct textblock *t, ssize_t *size);

int textblock_read(struct textblock *t, ssize_t from, ssize_t length, char *buffer);


#endif
