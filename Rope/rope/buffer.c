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

static int buffer_reserve_blocks(struct buffer *b, ssize_t size)
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

static int buffer_delete_block(struct buffer *b, ssize_t index)
{
    assert(0 <= index && index < b->blocks_len);

    struct textblock *deleted = b->blocks[index];
    memmove(b->blocks + index, b->blocks + index + 1, sizeof(*b->blocks) * (b->blocks_len - index - 1));
    textblock_destroy(deleted);
    free(deleted);
    return 0;
}

static int buffer_new_version(struct buffer *b, ssize_t parent, ssize_t *result_version)
{
    assert(parent < b->version_tree_len);
    assert(b->version_tree_len <= b->version_tree_alloc);
    
    if (b->version_tree_len == b->version_tree_alloc)
    {
        b->version_tree_alloc = 2 * b->version_tree_alloc + !(b->version_tree_alloc);
        void *new_ptr_1 = realloc(b->version_tree, sizeof(*b->version_tree) * b->version_tree_alloc);
        void *new_ptr_2 = realloc(b->version_depth, sizeof(*b->version_depth) * b->version_tree_alloc);
        void *new_ptr_3 = realloc(b->version_skiplist, sizeof(*b->version_skiplist) * b->version_tree_alloc);
        if (new_ptr_1 == NULL || new_ptr_2 == NULL || new_ptr_3)
        {
            return 1;
        }
        b->version_tree = new_ptr_1;
        b->version_depth = new_ptr_2;
        b->version_skiplist = new_ptr_3;
    }
    b->version_tree[b->version_tree_len] = parent;
    b->version_depth[b->version_tree_len] = b->version_depth[parent] + 1;
    if (b->version_depth[parent] - b->version_depth[b->version_skiplist[parent]] ==
        b->version_depth[b->version_skiplist[parent]] - b->version_depth[b->version_skiplist[b->version_skiplist[parent]]])
    {
        b->version_skiplist[b->version_tree_len] = b->version_depth[b->version_skiplist[b->version_skiplist[parent]]];
    }
    else
    {    
        b->version_skiplist[b->version_tree_len] = parent;
    }
    *result_version = b->version_tree_len++;
    return 0;
}


/* auto memory management wrappers */

int buffer_create(struct buffer **b)
{
    *b = malloc(sizeof(**b));
    return buffer_init(*b);
}

int buffer_moditify_star(struct buffer *b, uint64_t type, ssize_t pos, ssize_t len, char *text)
{
    struct modification mod = {type, pos, len, text};
    return buffer_moditify(b, &mod);
}


/* functions */

int buffer_init(struct buffer *b)
{
    b->version = 0;
    b->version_tree_len = 0;
    b->version_tree_alloc = 16;
    b->version_tree = malloc(sizeof(*b->version_tree) * b->version_tree_alloc);
    b->version_depth = malloc(sizeof(*b->version_skiplist) * b->version_tree_alloc);
    b->version_skiplist = malloc(sizeof(*b->version_skiplist) * b->version_tree_alloc);
    
    b->blocks_len = 0;
    b->blocks_alloc = 0;
    b->blocks = NULL;
    
    node_allocator_init(&b->allocator);

    b->avr_block_size = 64 * 1024;


    /* create loop on first version! */
    b->version_tree[0] = 0;
    b->version_depth[0] = 0;
    b->version_skiplist[0] = 0;
    b->version_tree_len = 1;
    
    /* create first text block */
    buffer_reserve_blocks(b, 1);
    b->blocks_len++;
    b->blocks[0] = malloc(sizeof(*b->blocks[0]));
    textblock_init(b->blocks[0]);
}

int buffer_destroy(struct buffer *b)
{
    for (ssize_t i = 0; i < b->blocks_len; ++i)
    {
        textblock_destroy(b->blocks[i]);
        free(b->blocks[i]);
    }
    free(b->blocks);
    b->blocks = NULL;
    b->blocks_len = 0;
    b->blocks_alloc = 0;

    node_allocator_init(&b->allocator);
}

int buffer_moditify(struct buffer *b, struct modification *mod)
{
    assert(0 <= mod->pos);
    assert(0 <= mod->len); // TODO: fix bugs and make it 0 -> 1

    if (mod->len == 0)
    {
        return 0;
    }

    /* create new version */
    ssize_t version = 0;
    if (buffer_new_version(b, b->version, &version) != 0 || version == 0)
    {
        return 4;
    }
    b->version = version;
        
    /* find buffer to modificate */
    switch (mod->type)
    {
        case ModificationInsert:
        {        
            ssize_t pos = 0, size = 0;
            for (ssize_t i = 0; i < b->blocks_len; ++i, pos += size)
            {
                size = 0;
                if (textblock_get_size(b, b->blocks[i], &size, b->version) != 0)
                {
                    return 9;
                }
                if (pos <= mod->pos && mod->pos < pos + size)
                {
                    /* this buffer contains position for modification */
                    if (textblock_modificate(b, b->blocks[i], mod, b->version) != 0)
                    {
                        return 9;
                    }
                    return 0;
                }
            }
            /* here we don't inserted modification into any block, so write to end of last block */
            if (b->blocks_len == 0 && mod->pos == 0)
            {
                /* create first block */
                if (buffer_reserve_blocks(b, 1) != 0)
                {
                    return 5;
                }
                
                b->blocks_len++;
                b->blocks[0] = malloc(sizeof(*b->blocks[0]));
                if (textblock_init(b->blocks[0]) != 0)
                {
                    return 9;
                }
            }
            else if (pos == mod->pos)
            {
                if (textblock_modificate(b, b->blocks[b->blocks_len - 1], mod, b->version) != 0)
                {
                    return 9;
                }
            }
            else
            {
                /* error: we get wrong insert position */
                return 1;
            }
            return 0;
        }
        case ModificationDelete:
        {
            ssize_t pos = 0, size = 0;
            for (ssize_t i = 0; i < b->blocks_len; ++i, pos += size)
            {
                size = 0;
                if (textblock_get_size(b, b->blocks[i], &size, b->version))
                {
                    return 9;
                }
                /* is block fully deleted? */
                if (mod->pos <= pos && pos + size <= mod->pos + mod->len)
                {
                    /* delete entire block */
                    if (buffer_delete_block(b, i) != 0)
                    {
                        return 9;
                    }
                    /* update this loop counter */
                    --i;
                    continue;
                }
                /* is deleted some prefix of block? */
                else if (mod->pos <= pos && mod->pos + mod->len > pos)
                {
                    struct modification new_mod = *mod;
                    new_mod.pos = 0;
                    new_mod.len = mod->pos + mod->len - pos;
                    if (textblock_modificate(b, b->blocks[i], &new_mod, b->version) != 0)
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
                    if (textblock_modificate(b, b->blocks[i], &new_mod, b->version) != 0)
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
                    if (textblock_modificate(b, b->blocks[i], &new_mod, b->version) != 0)
                    {
                        return 9;
                    }
                }
                else
                {
                    /* this branch mean that this modification doen't modificates this block */
                    assert(mod->pos + mod->len < pos || mod->pos >= pos + size);
                }
            }
            return 0;
        }
        default:
            /* wrong modification type */
            return 3;
    }
}

int buffer_get_size(struct buffer *b, ssize_t *length)
{
    ssize_t len = 0;
    for (ssize_t i = 0; i < b->blocks_len; ++i)
    {
        ssize_t size = 0;
        textblock_get_size(b, b->blocks[i], &size, b->version);
        len += size;
    }
    *length = len;
}

int buffer_read(struct buffer *b, ssize_t from, ssize_t length, char *buffer)
{
    assert(0 <= from);
    assert(0 <= length); // TODO: fix bugs and make it 0 -> 1

    if (length == 0)
    {
        return 0;
    }
    
    /* find first block to read from */
    ssize_t pos = 0, size = 0;
    ssize_t total_read = 0;
    for (ssize_t i = 0; i < b->blocks_len; ++i, pos += size)
    {
        size = 0;
        textblock_get_size(b, b->blocks[i], &size, b->version);
        printf("SPAN: %zd-%zd READ %zd-%zd\n", pos, size, from, length);
        /* if we read any part of this block */
        if (pos <= from && from < pos + size)
        {
            /* find this part's size and position */
            ssize_t start = (from > pos ? from : pos);
            ssize_t end = (from + length > pos + size ? pos + size : from + length);
            textblock_read(b, b->blocks[i], start, end - start, buffer, b->version);
            buffer += end - start;
            total_read += end - start;
        }
    }
    #ifdef BAN_READ_WRONG_PIECES
        assert(total_read == length);
    #endif
    return 0;
}


int buffer_version_set(struct buffer *b, ssize_t version)
{
    assert(0 <= version && version < b->version_tree_len);
    
    b->version = version;
    return 0;
}


int buffer_version_get(struct buffer *b, ssize_t *version)
{
    *version = b->version;
    return 0;
}


int buffer_version_before(struct buffer *b, ssize_t version, ssize_t steps, ssize_t *result)
{
    assert(0 <= version && version < b->version_tree_len);

    for (ssize_t i = 0; i < steps; ++i)
    {
        version = b->version_tree[version];
        assert(0 <= version && version < b->version_tree_len);
    }
    
    *result = version;
    return 0;
}


static ssize_t up_variant(struct buffer *b, ssize_t x, ssize_t add)
{
    while (b->version_tree[x] != x && add > 0)
    {
        ssize_t d = b->version_depth[x] - b->version_depth[b->version_skiplist[x]];
        if (d <= add)
        {
            x = b->version_skiplist[x];
            add -= d;
        }
        else
        {
            x = b->version_tree[x];
            add--;
        }
    }

    return x;
}


int buffer_version_lca(struct buffer *b, ssize_t version_a, ssize_t version_b, ssize_t *result)
{
    if (b->version_depth[version_a] > b->version_depth[version_b])
    {
        version_a = up_variant(b, version_a, b->version_depth[version_a] - b->version_depth[version_b]);
    }
    if (b->version_depth[version_a] < b->version_depth[version_b])
    {
        version_b = up_variant(b, version_b, b->version_depth[version_b] - b->version_depth[version_a]);
    }

    assert(b->version_depth[version_a] == b->version_depth[version_b]);

    ssize_t jump = version_a;

    while (version_a != version_b)
    {
        ssize_t tmp_a = up_variant(b, version_a, jump);
        ssize_t tmp_b = up_variant(b, version_b, jump);
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
