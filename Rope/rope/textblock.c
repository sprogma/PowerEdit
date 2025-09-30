#define __USE_MINGW_ANSI_STDIO 1
#include "stdio.h"

#include "malloc.h"
#include "string.h"
#include "assert.h"


#include "buffer.h"
#include "export.h"
#include "textblock.h"
#include "texttree.h"
#include "node_allocator.h"
#include "modification.h"


static int textblock_history_reserve(struct textblock *t, ssize_t size)
{
    while (size > t->history_buffer_alloc)
    {
        t->history_buffer_alloc = 2 * t->history_buffer_alloc + !(t->history_buffer_alloc);
    }
    void *new_ptr = realloc(t->history_buffer, sizeof(*t->history_buffer) * t->history_buffer_alloc);
    if (new_ptr == NULL)
    {
        return 1;
    }
    t->history_buffer = new_ptr;
    return 0;
}


int textblock_init(struct textblock *t)
{
    t->string = NULL;
    t->len = 0;

    t->history_buffer_alloc = 16;
    t->history_buffer = malloc(sizeof(*t->history_buffer) * t->history_buffer_alloc);
}

int textblock_destroy(struct textblock *t)
{
    free(t->string);
}

int textblock_modificate(struct textblock *t, struct modification *mod, ssize_t version)
{
    printf("get modification mod! %zd %zd %s IN %zd\n", mod->pos, mod->len, mod->text, version);
    
    textblock_history_reserve(t, version);
    
    switch (mod->type)
    {
        case ModificationInsert:
        { 
            char *old = t->string;
            char *tmp = malloc(t->len + mod->len);
            memcpy(tmp + 0, old, mod->pos);
            memcpy(tmp + mod->pos, mod->text, mod->len);
            memcpy(tmp + mod->pos + mod->len, old + mod->pos, t->len - mod->pos);
            t->string = tmp;
            t->len = t->len + mod->len;
            free(old);
            break;
        }
        case ModificationDelete:
        {
            char *old = t->string;
            char *tmp = malloc(t->len + mod->len);
            memcpy(tmp, old, mod->pos);
            memcpy(tmp + mod->pos, old + mod->pos + mod->len, t->len - mod->pos);
            t->string = tmp;
            t->len = t->len - mod->len;
            free(old);
            break;
        }
        default:
            return 3;
    }
    return 0;
}

int textblock_get_size(struct textblock *t, ssize_t *size, ssize_t version)
{
    textblock_history_reserve(t, version);
    
    printf("return len: %zd\n", t->len);
    *size = t->len;
    return 0;
}

int textblock_read(struct textblock *t, ssize_t from, ssize_t length, char *buffer, ssize_t version)
{
    printf("read from %zd len %zd : %c [%d]\n", from, length, t->string[from], t->string[from]);
    memcpy(buffer, t->string + from, length);
    return 0;
}
