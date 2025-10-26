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
    ssize_t old_size = t->history_buffer_alloc;
    while (size > t->history_buffer_alloc)
    {
        t->history_buffer_alloc = 2 * t->history_buffer_alloc + !(t->history_buffer_alloc);
    }
    void *new_ptr_1 = realloc(t->history_buffer, sizeof(*t->history_buffer) * t->history_buffer_alloc);
    void *new_ptr_2 = realloc(t->history_len_buffer, sizeof(*t->history_len_buffer) * t->history_buffer_alloc);
    if (new_ptr_1 == NULL || new_ptr_2 == NULL)
    {
        return 1;
    }
    memset(t->history_buffer + old_size, 0, sizeof(*t->history_buffer) * (t->history_buffer_alloc - old_size));
    memset(t->history_len_buffer + old_size, 0, sizeof(*t->history_len_buffer) * (t->history_buffer_alloc - old_size));
    t->history_buffer = new_ptr_1;
    t->history_len_buffer = new_ptr_2;
    return 0;
}


static size_t base_of(struct buffer *b, struct textblock *t, ssize_t version)
{
    ssize_t parent = version;

    while (t->history_buffer[parent] == NULL && parent != b->version_tree[parent])
    {
        parent = b->version_tree[parent];
    }
    return parent;
}


int textblock_init(struct textblock *t)
{
    t->history_buffer_alloc = 128;
    t->history_buffer = calloc(1, sizeof(*t->history_buffer) * t->history_buffer_alloc);
    t->history_len_buffer = calloc(1, sizeof(*t->history_len_buffer) * t->history_buffer_alloc);

    t->history_buffer[0] = strdup("");
    t->history_len_buffer[0] = 0;
}

int textblock_destroy(struct textblock *t)
{
    for (ssize_t i = 0; i < t->history_buffer_alloc; ++i)
    {
        if (t->history_buffer[i] != NULL)
        {
            free(t->history_buffer[i]);
        }
    }
    free(t->history_buffer);
    free(t->history_len_buffer);
}

int textblock_modificate(struct buffer *b, struct textblock *t, struct modification *mod, ssize_t version)
{
    printf("get modification mod! %zd %zd %s IN %zd\n", mod->pos, mod->len, mod->text, version);
    
    textblock_history_reserve(t, version);

    /* find block state in this version: */
    ssize_t parent = base_of(b, t, version);
    if (t->history_buffer[parent] == NULL)
    {
        return 1;
    }
    
    switch (mod->type)
    {
        case ModificationInsert:
        { 
            char *old = t->history_buffer[parent];
            char *tmp = malloc(t->history_len_buffer[parent] + mod->len);
            memcpy(tmp + 0, old, mod->pos);
            memcpy(tmp + mod->pos, mod->text, mod->len);
            memcpy(tmp + mod->pos + mod->len, old + mod->pos, t->history_len_buffer[parent] - mod->pos);
            t->history_buffer[version] = tmp;
            t->history_len_buffer[version] = t->history_len_buffer[parent] + mod->len;
            break;
        }
        case ModificationDelete:
        {
            char *old = t->history_buffer[parent];
            char *tmp = malloc(t->history_len_buffer[parent] + mod->len);
            memcpy(tmp, old, mod->pos);
            memcpy(tmp + mod->pos, old + mod->pos + mod->len, t->history_len_buffer[parent] - mod->pos);
            t->history_buffer[version] = tmp;
            t->history_len_buffer[version] = t->history_len_buffer[parent] - mod->len;
            break;
        }
        default:
            return 3;
    }
    return 0;
}

int textblock_get_size(struct buffer *b, struct textblock *t, ssize_t *size, ssize_t version)
{
    /* find block state in this version: */
    ssize_t parent = base_of(b, t, version);
    if (t->history_buffer[parent] == NULL)
    {
        return 1;
    }
   
    printf("return len: %zd\n", t->history_len_buffer[parent]);
    *size = t->history_len_buffer[parent];
    return 0;
}

int textblock_read(struct buffer *b, struct textblock *t, ssize_t from, ssize_t length, char *buffer, ssize_t version)
{
    /* find block state in this version: */
    ssize_t parent = base_of(b, t, version);
    if (t->history_buffer[parent] == NULL)
    {
        return 1;
    }
    printf("read from %zd len %zd : %c [%d]\n", from, length, t->history_buffer[parent][from], t->history_buffer[parent][from]);
    memcpy(buffer, t->history_buffer[parent] + from, length);
    return 0;
}
