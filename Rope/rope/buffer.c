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


/* inner functions */

static int buffer_reserve_blocks(struct buffer *b, int64_t size)
{
    while (size > b->blocks_alloc)
    {
        b->blocks_alloc += 2;
    }
    void *new_ptr = realloc(b->blocks, sizeof(*b->blocks) * b->blocks_alloc);
    if (new_ptr == NULL)
    {
        return 1;
    }
    b->blocks = new_ptr;
    return 0;
}

static int buffer_delete_block(struct buffer *b, int64_t index)
{
    assert(0 <= index && index < b->blocks_len);

    struct textblock *deleted = b->blocks[index];
    memmove(b->blocks + index, b->blocks + index + 1, sizeof(*b->blocks) * (b->blocks_len - index - 1));
    b->blocks_len--;
    textblock_destroy(deleted);
    free(deleted);
    return 0;
}

static int buffer_new_version(struct buffer *buf, int64_t parent, int64_t *result_version)
{
    assert(parent < buf->version_tree_len);
    assert(buf->version_tree_len <= buf->version_tree_alloc);

    // printf("create new version?\n");
    
    if (buf->version_tree_len >= buf->version_tree_alloc)
    {
        int old_size = buf->version_tree_alloc;
        buf->version_tree_alloc = 2 * buf->version_tree_alloc + !(buf->version_tree_alloc);
        void *new_ptr_1 = realloc(buf->version_tree, sizeof(*buf->version_tree) * buf->version_tree_alloc);
        void *new_ptr_2 = realloc(buf->version_depth, sizeof(*buf->version_depth) * buf->version_tree_alloc);
        void *new_ptr_3 = realloc(buf->version_skiplist, sizeof(*buf->version_skiplist) * buf->version_tree_alloc);
        void *new_ptr_4 = realloc(buf->version_cursors, sizeof(*buf->version_cursors) * buf->version_tree_alloc);
        void *new_ptr_5 = realloc(buf->version_cursors_count, sizeof(*buf->version_cursors_count) * buf->version_tree_alloc);
        if (new_ptr_1 == NULL || new_ptr_2 == NULL || new_ptr_3 == NULL || new_ptr_4 == NULL || new_ptr_5 == NULL)
        {
            return 1;
        }
        buf->version_tree = new_ptr_1;
        buf->version_depth = new_ptr_2;
        buf->version_skiplist = new_ptr_3;
        buf->version_cursors = new_ptr_4;
        buf->version_cursors_count = new_ptr_5;
        memset(buf->version_tree + old_size, 0, sizeof(*buf->version_tree) * (buf->version_tree_alloc - old_size));
        memset(buf->version_depth + old_size, 0, sizeof(*buf->version_depth) * (buf->version_tree_alloc - old_size));
        memset(buf->version_skiplist + old_size, 0, sizeof(*buf->version_skiplist) * (buf->version_tree_alloc - old_size));
        memset(buf->version_cursors + old_size, 0, sizeof(*buf->version_cursors) * (buf->version_tree_alloc - old_size));
        memset(buf->version_cursors_count + old_size, 0, sizeof(*buf->version_cursors_count) * (buf->version_tree_alloc - old_size));
    }
    buf->version_tree[buf->version_tree_len] = parent;
    buf->version_depth[buf->version_tree_len] = buf->version_depth[parent] + 1;
    if (buf->version_depth[parent] - buf->version_depth[buf->version_skiplist[parent]] ==
        buf->version_depth[buf->version_skiplist[parent]] - buf->version_depth[buf->version_skiplist[buf->version_skiplist[parent]]])
    {
        buf->version_skiplist[buf->version_tree_len] = buf->version_depth[buf->version_skiplist[buf->version_skiplist[parent]]];
    }
    else
    {
        buf->version_skiplist[buf->version_tree_len] = parent;
    }
    *result_version = buf->version_tree_len++;
    return 0;
}


/* auto memory management wrappers */

int buffer_create(struct buffer **buf)
{
    *buf = malloc(sizeof(**buf));
    return buffer_init(*buf);
}

int buffer_moditify_star(struct buffer *buf, uint64_t type, int64_t pos, int64_t len, char *text)
{
    struct modification mod = {type, pos, len, text};
    return buffer_moditify(buf, &mod);
}


/* functions */

int buffer_init(struct buffer *buf)
{
    buf->version = 0;
    buf->version_tree_len = 0;
    buf->version_tree_alloc = 16;
    buf->version_tree = calloc(1, sizeof(*buf->version_tree) * buf->version_tree_alloc);
    buf->version_depth = calloc(1, sizeof(*buf->version_skiplist) * buf->version_tree_alloc);
    buf->version_skiplist = calloc(1, sizeof(*buf->version_skiplist) * buf->version_tree_alloc);
    buf->version_cursors = calloc(1, sizeof(*buf->version_cursors) * buf->version_tree_alloc);
    buf->version_cursors_count = calloc(1, sizeof(*buf->version_cursors_count) * buf->version_tree_alloc);
    
    buf->blocks_len = 0;
    buf->blocks_alloc = 0;
    buf->blocks = NULL;
    
    node_allocator_init(&buf->allocator);

    buf->avr_block_size = 64 * 1024LL;


    /* create loop on first version! */
    buf->version_tree[0] = 0;
    buf->version_depth[0] = 0;
    buf->version_skiplist[0] = 0;
    buf->version_tree_len = 1;
    
    /* create first text block */
    buffer_reserve_blocks(buf, 1);
    buf->blocks_len++;
    buf->blocks[0] = malloc(sizeof(*buf->blocks[0]));
    textblock_init(buf->blocks[0]);
}

int buffer_destroy(struct buffer *buf)
{
    for (int64_t i = 0; i < buf->blocks_len; ++i)
    {
        textblock_destroy(buf->blocks[i]);
        free(buf->blocks[i]);
    }
    free(buf->blocks);
    buf->blocks = NULL;
    buf->blocks_len = 0;
    buf->blocks_alloc = 0;

    node_allocator_init(&buf->allocator);
}

int buffer_moditify(struct buffer *buf, struct modification *mod)
{
    assert(0 <= mod->pos);
    assert(0 <= mod->len); // TODO: fix bugs and make it 0 -> 1

    if (mod->len == 0)
    {
        return 0;
    }

    // printf("MOD from %lld type: %lld of len %lld\n", mod->pos, mod->type, mod->len);

    /* create new version */
    // int64_t old_version = b->version;
    int64_t version = 0;
    if (buffer_new_version(buf, buf->version, &version) != 0 || version == 0)
    {
        return 4;
    }
    buf->version = version;
        
    /* find buffer to modificate */
    switch (mod->type)
    {
        case ModificationInsert:
        {
            int64_t pos = 0, size = 0;
            for (int64_t i = 0; i < buf->blocks_len; ++i, pos += size)
            {
                size = 0;
                if (textblock_get_size(buf, buf->blocks[i], &size, buf->version) != 0)
                {
                    return 9;
                }
                if (pos <= mod->pos && mod->pos < pos + size)
                {
                    /* this buffer contains position for modification */
                    if (textblock_modificate(buf, buf->blocks[i], mod, buf->version) != 0)
                    {
                        return 9;
                    }
                    return 0;
                }
            }
            /* here we don't inserted modification into any block, so write to end of last block */
            if (buf->blocks_len == 0 && mod->pos == 0)
            {
                /* create first block */
                if (buffer_reserve_blocks(buf, 1) != 0)
                {
                    return 5;
                }
                
                buf->blocks_len++;
                buf->blocks[0] = malloc(sizeof(*buf->blocks[0]));
                if (textblock_init(buf->blocks[0]) != 0)
                {
                    return 9;
                }
                if (textblock_modificate(buf, buf->blocks[buf->blocks_len - 1], mod, buf->version) != 0)
                {
                    return 9;
                }
            }
            else // if (pos == mod->pos)
            {
                mod->pos = pos;
                if (textblock_modificate(buf, buf->blocks[buf->blocks_len - 1], mod, buf->version) != 0)
                {
                    return 9;
                }
            }
            // else
            // {
            //     /* error: we get wrong insert position */
            //     return 1;
            // }
            return 0;
        }
        case ModificationDelete:
        {
            int64_t pos = 0, size = 0;
            assert(buf->blocks_len == 1);
            for (int64_t i = 0; i < buf->blocks_len; ++i, pos += size)
            {
                size = 0;
                if (textblock_get_size(buf, buf->blocks[i], &size, buf->version))
                {
                    return 9;
                }
                /* is block fully deleted? */
                // printf("IS DEL FULL: %zd %zd   %zd %zd\n", mod->pos, mod->len, pos, size);
                if (mod->pos <= pos && pos + size <= mod->pos + mod->len)
                {
                    // CAN't delete block, becouse of history, which is stored in it.
                    // /* delete entire block */
                    // if (buffer_delete_block(b, i) != 0)
                    // {
                    //     return 9;
                    // }
                    // /* update this loop counter */
                    // --i;
                    // continue;
                    struct modification new_mod = *mod;
                    new_mod.pos = 0;
                    new_mod.len = mod->pos + mod->len - pos;
                    if (textblock_modificate(buf, buf->blocks[i], &new_mod, buf->version) != 0)
                    {
                        return 9;
                    }
                }
                /* is deleted some prefix of block? */
                else if (mod->pos <= pos && mod->pos + mod->len > pos)
                {
                    struct modification new_mod = *mod;
                    new_mod.pos = 0;
                    new_mod.len = mod->pos + mod->len - pos;
                    if (textblock_modificate(buf, buf->blocks[i], &new_mod, buf->version) != 0)
                    {
                        return 9;
                    }
                }
                /* is deleted some suffix of block? */
                else if (mod->pos < pos + size && mod->pos + mod->len >= pos + size)
                {
                    struct modification new_mod = *mod;
                    new_mod.pos = mod->pos - pos;
                    new_mod.len = pos + size - mod->pos;
                    if (textblock_modificate(buf, buf->blocks[i], &new_mod, buf->version) != 0)
                    {
                        return 9;
                    }
                }
                /* else - is is something ? */
                else if (pos < mod->pos && mod->pos + mod->len < pos + size)
                {
                    struct modification new_mod = *mod;
                    new_mod.pos = mod->pos - pos;
                    new_mod.len = mod->len;
                    if (textblock_modificate(buf, buf->blocks[i], &new_mod, buf->version) != 0)
                    {
                        return 9;
                    }
                }
                else
                {
                    /* this branch mean that this modification doen't modificates this block */
                    assert(mod->pos + mod->len <= pos || mod->pos >= pos + size);
                }
            }
            return 0;
        }
        default:
            /* wrong modification type */
            return 3;
    }
}

int buffer_get_size(struct buffer *buf, int64_t *length)
{
    int64_t len = 0;
    for (int64_t i = 0; i < buf->blocks_len; ++i)
    {
        int64_t size = 0;
        textblock_get_size(buf, buf->blocks[i], &size, buf->version);
        len += size;
    }
    *length = len;
}

int buffer_read(struct buffer *buf, int64_t from, int64_t length, char *buffer)
{
    assert(0 <= from);
    assert(0 <= length); // TODO: fix bugs and make it 0 -> 1

    if (length == 0)
    {
        return 0;
    }
    
    /* find first block to read from */
    int64_t pos = 0, size = 0;
    int64_t total_read = 0;
    for (int64_t i = 0; i < buf->blocks_len; ++i, pos += size)
    {
        size = 0;
        textblock_get_size(buf, buf->blocks[i], &size, buf->version);
        // printf("SPAN: %zd-%zd READ %zd-%zd\n", pos, size, from, length);
        /* if we read any part of this block */
        if (pos <= from && from < pos + size)
        {
            /* find this part's size and position */
            int64_t start = (from > pos ? from : pos);
            int64_t end = (from + length > pos + size ? pos + size : from + length);
            textblock_read(buf, buf->blocks[i], start, end - start, buffer, buf->version);
            buffer += end - start;
            total_read += end - start;
        }
    }
    #ifdef BAN_READ_WRONG_PIECES
        assert(total_read == length);
    #endif
    return 0;
}


int buffer_version_set(struct buffer *buf, int64_t version)
{
    assert(0 <= version && version < buf->version_tree_len);
    
    buf->version = version;
    return 0;
}


int buffer_version_get(struct buffer *buf, int64_t *version)
{
    *version = buf->version;
    return 0;
}


int buffer_version_before(struct buffer *buf, int64_t version, int64_t steps, int64_t *result)
{
    assert(0 <= version && version < buf->version_tree_len);

    for (int64_t i = 0; i < steps; ++i)
    {
        version = buf->version_tree[version];
        assert(0 <= version && version < buf->version_tree_len);
    }
    
    *result = version;
    return 0;
}


static int64_t up_variant(struct buffer *buf, int64_t x, int64_t add)
{
    while (buf->version_tree[x] != x && add > 0)
    {
        int64_t d = buf->version_depth[x] - buf->version_depth[buf->version_skiplist[x]];
        if (d <= add)
        {
            x = buf->version_skiplist[x];
            add -= d;
        }
        else
        {
            x = buf->version_tree[x];
            add--;
        }
    }

    return x;
}


int buffer_version_lca(struct buffer *buf, int64_t version_a, int64_t version_b, int64_t *result)
{
    if (buf->version_depth[version_a] > buf->version_depth[version_b])
    {
        version_a = up_variant(buf, version_a, buf->version_depth[version_a] - buf->version_depth[version_b]);
    }
    if (buf->version_depth[version_a] < buf->version_depth[version_b])
    {
        version_b = up_variant(buf, version_b, buf->version_depth[version_b] - buf->version_depth[version_a]);
    }

    assert(buf->version_depth[version_a] == buf->version_depth[version_b]);

    int64_t jump = version_a;

    while (version_a != version_b)
    {
        int64_t tmp_a = up_variant(buf, version_a, jump);
        int64_t tmp_b = up_variant(buf, version_b, jump);
        if (tmp_a != tmp_b)
        {
            version_a = tmp_a;
            version_b = tmp_b;
        }
        else
        {
            jump /= 2;
        }
    }

    *result = version_a;
    return 0;
}

int buffer_read_versions_count(struct buffer *buf, int64_t *result)
{
    *result = buf->version_tree_len;
    return 0;
}

int buffer_read_versions(struct buffer *buf, int64_t count, int64_t *result)
{
    memcpy(result, buf->version_tree, sizeof(*buf->version_tree) * count);
    return 0;
}

int buffer_set_version_cursors(struct buffer* buf, int64_t version, int64_t count, struct cursor_t* cursors)
{
    free(buf->version_cursors[version]);
    buf->version_cursors_count[version] = count;
    buf->version_cursors[version] = malloc(sizeof(*buf->version_cursors[version]) * count);
    memcpy(buf->version_cursors[version], cursors, sizeof(*buf->version_cursors[version]) * count);
    //printf("Saved cursors: %d items first at %d %d\n", count, cursors[0].begin, cursors[0].end);
    return 0;
}

int buffer_get_version_cursors_count(struct buffer *buf, int64_t version, int64_t *count)
{
    *count = buf->version_cursors_count[version];
    return 0;
}

int buffer_get_version_cursors(struct buffer *buf, int64_t version, int64_t count, struct cursor_t* cursors)
{
    memcpy(cursors, buf->version_cursors[version], sizeof(*cursors) * count);
    //printf("Read cursors: %d items first at %d %d\n", count, cursors[0].begin, cursors[0].end);
    return 0;
}

