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

#include "newline_splay.h"

static int textblock_history_reserve(struct textblock *tb, int64_t size)
{
    // printf("reserve history\n");
    int64_t old_size = tb->history_buffer_alloc;
    while (size + 1 > tb->history_buffer_alloc)
    {
        tb->history_buffer_alloc = 2 * tb->history_buffer_alloc + !(tb->history_buffer_alloc);
    }
    if (tb->history_buffer_alloc != old_size)
    {
        void *new_ptr_1 = realloc(tb->history_buffer, sizeof(*tb->history_buffer) * tb->history_buffer_alloc);
        void *new_ptr_2 = realloc(tb->history_len_buffer, sizeof(*tb->history_len_buffer) * tb->history_buffer_alloc);
        if (new_ptr_1 == NULL || new_ptr_2 == NULL)
        {
            return 1;
        }
        tb->history_buffer = new_ptr_1;
        tb->history_len_buffer = new_ptr_2;
        memset(tb->history_buffer + old_size, 0, sizeof(*tb->history_buffer) * (tb->history_buffer_alloc - old_size));
        memset(tb->history_len_buffer + old_size, 0, sizeof(*tb->history_len_buffer) * (tb->history_buffer_alloc - old_size));
        // printf("ret\n");
    }
    return 0;
}


static size_t base_of(struct buffer *buf, struct textblock *tb, int64_t version)
{
    while (tb->history_buffer[version] == NULL && version != buf->version_tree[version])
    {
        version = buf->version_tree[version];
    }
    return version;
}


int textblock_init(struct textblock *tb)
{
    tb->history_buffer_alloc = 16;
    tb->history_buffer = calloc(1, sizeof(*tb->history_buffer) * tb->history_buffer_alloc);
    tb->history_len_buffer = calloc(1, sizeof(*tb->history_len_buffer) * tb->history_buffer_alloc);

    tb->history_buffer[0] = strdup("");
    tb->history_len_buffer[0] = 0;
    return 0;
}

int textblock_destroy(struct textblock *tb)
{
    for (int64_t i = 0; i < tb->history_buffer_alloc; ++i)
    {
        if (tb->history_buffer[i] != NULL)
        {
            free(tb->history_buffer[i]);
        }
    }
    free(tb->history_buffer);
    free(tb->history_len_buffer);
    return 0;
}

int textblock_modificate(struct buffer *buf, struct textblock *tb, struct modification *mod, int64_t version)
{
    // printf("get modification mod! %zd %zd %s IN %zd\n", mod->pos, mod->len, mod->text, version);
    
    textblock_history_reserve(tb, version);

    /* find block state in this version: */
    int64_t parent = base_of(buf, tb, version);
    // printf("Parent %lld Version %lld buf: %p buf %p\n", parent, version, t->history_buffer[parent], t->history_buffer[version]);
    if (tb->history_buffer[parent] == NULL)
    {
        return 1;
    }

    
    switch (mod->type)
    {
        case ModificationInsert:
        { 
            char *old = tb->history_buffer[parent];
            char *tmp = malloc(tb->history_len_buffer[parent] + mod->len);
            memcpy(tmp + 0, old, mod->pos);
            memcpy(tmp + mod->pos, mod->text, mod->len);
            memcpy(tmp + mod->pos + mod->len, old + mod->pos, tb->history_len_buffer[parent] - mod->pos);
            tb->history_buffer[version] = tmp;
            tb->history_len_buffer[version] = tb->history_len_buffer[parent] + mod->len;
            // printf("Len = %lld = %lld + %lld\n", t->history_len_buffer[version], t->history_len_buffer[parent], mod->len);
            //char c;
            //scanf("%c", &c);
            break;
        }
        case ModificationDelete:
        {
            char *old = tb->history_buffer[parent];
            char *tmp = malloc(tb->history_len_buffer[parent] + mod->len);
            memcpy(tmp, old, mod->pos);
            memcpy(tmp + mod->pos, old + mod->pos + mod->len, tb->history_len_buffer[parent] - mod->pos);
            tb->history_buffer[version] = tmp;
            tb->history_len_buffer[version] = tb->history_len_buffer[parent] - mod->len;
            break;
        }
        default:
            return 3;
    }
    return 0;
}

int textblock_get_size(struct buffer *buf, struct textblock *tb, int64_t *size, int64_t version)
{
    textblock_history_reserve(tb, version);
        
    /* find block state in this version: */
    int64_t parent = base_of(buf, tb, version);
    if (tb->history_buffer[parent] == NULL)
    {
        return 1;
    }
   
    // printf("return len [version %zd [parent %zd]]: %zd\n", version, parent, t->history_len_buffer[parent]);
    *size = tb->history_len_buffer[parent];
    return 0;
}

int textblock_read(struct buffer *buf, struct textblock *tb, int64_t from, int64_t length, char *buffer, int64_t version)
{
    textblock_history_reserve(tb, version);   

    /* find block state in this version: */
    int64_t parent = base_of(buf, tb, version);
    if (tb->history_buffer[parent] == NULL)
    {
        return 1;
    }
    // printf("read from %zd len %zd : %c [%d]\n", from, length, t->history_buffer[parent][from], t->history_buffer[parent][from]);
    memcpy(buffer, tb->history_buffer[parent] + from, length);
    return 0;
}


