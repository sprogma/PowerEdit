#include "assert.h"

#include "structure.h"


#define MAX_NODES 1000000
struct segment glb_nodes[MAX_NODES];
int64_t glb_next_node = 1;


#define _len(n) (n ? glb_nodes[n].total_length : 0)
#define _hgt(n) (n ? glb_nodes[n].height : 0)
#define _cnt(n) (n ? glb_nodes[n].total_newlines : 0)


static void update(int64_t node) 
{
    if (!node) return;
    // printf("Updating of %lld [total %lld nodes]\n", node, glb_next_node);
    // printf("total_length = %lld + %lld + %lld\n", _len(glb_nodes[node].left), _len(glb_nodes[node].right), glb_nodes[node].length);
    glb_nodes[node].total_length = _len(glb_nodes[node].left) + _len(glb_nodes[node].right) + glb_nodes[node].length;
    glb_nodes[node].total_newlines = _cnt(glb_nodes[node].left) + _cnt(glb_nodes[node].right) + glb_nodes[node].newlines;
    int hl = _hgt(glb_nodes[node].left);
    int hr = _hgt(glb_nodes[node].right);
    glb_nodes[node].height = (hl > hr ? hl : hr) + 1;
}

static int64_t _copy_to_version(int64_t node, int64_t this_version) 
{
    if (!node || glb_nodes[node].version_id == this_version) return node;

    int64_t new_node = glb_next_node++;
    // printf("A: allocated node %lld [copy from %lld]\n", new_node, node);
    memcpy(&glb_nodes[new_node], &glb_nodes[node], sizeof(struct segment));
    glb_nodes[new_node].version_id = this_version;
    update(new_node);
    
    assert(glb_nodes[new_node].buffer->buffer == glb_nodes[node].buffer->buffer);
    
    return new_node;
}


static int64_t rotate_right(int64_t y, int64_t ver) 
{
    y = _copy_to_version(y, ver);
    int64_t x = _copy_to_version(glb_nodes[y].left, ver);
    
    glb_nodes[y].left = glb_nodes[x].right;
    
    glb_nodes[x].right = y;
    
    update(y); 
    update(x);
    return x;
}

static int64_t rotate_left(int64_t x, int64_t ver) 
{
    x = _copy_to_version(x, ver);
    int64_t y = _copy_to_version(glb_nodes[x].right, ver);
    
    glb_nodes[x].right = glb_nodes[y].left;
    
    glb_nodes[y].left = x;
    
    update(x); 
    update(y);
    return y;
}



static int64_t balance(int64_t idx, int64_t ver) 
{
    update(idx);
    int balance_factor = _hgt(glb_nodes[idx].left) - _hgt(glb_nodes[idx].right);
    if (balance_factor > 1) 
    {
        if (_hgt(glb_nodes[glb_nodes[idx].left].left) < _hgt(glb_nodes[glb_nodes[idx].left].right))
        {
            int64_t tmp = rotate_left(glb_nodes[idx].left, ver);
            glb_nodes[idx].left = tmp;
            update(idx);
        }
        return rotate_right(idx, ver);
    }
    if (balance_factor < -1) 
    {
        if (_hgt(glb_nodes[glb_nodes[idx].right].right) < _hgt(glb_nodes[glb_nodes[idx].right].left))
        {
            int64_t tmp = rotate_right(glb_nodes[idx].right, ver);
            glb_nodes[idx].right = tmp;
            update(idx);
        }
        return rotate_left(idx, ver);
    }
    return idx;
}

static int64_t get_leftmost_child(int64_t idx) 
{
    while (glb_nodes[idx].left) idx = glb_nodes[idx].left;
    return idx;
}

static int64_t remove_internal(int64_t idx, int64_t pos, int64_t ver) {
    if (!idx) return 0;
    idx = _copy_to_version(idx, ver);
    int64_t left_len = _len(glb_nodes[idx].left);

    if (pos < left_len) 
    {
        int64_t tmp = remove_internal(glb_nodes[idx].left, pos, ver);
        glb_nodes[idx].left = tmp;
    } 
    else if (pos >= left_len + glb_nodes[idx].length) 
    {
        int64_t tmp = remove_internal(glb_nodes[idx].right, pos - left_len - glb_nodes[idx].length, ver);
        glb_nodes[idx].right = tmp;
    } 
    else 
    {
        if (!glb_nodes[idx].left || !glb_nodes[idx].right) 
        {
            int64_t tmp = glb_nodes[idx].left ? glb_nodes[idx].left : glb_nodes[idx].right;
            return tmp;
        } 
        else 
        {
            int64_t temp_node = get_leftmost_child(glb_nodes[idx].right);
            memcpy(&glb_nodes[idx], &glb_nodes[temp_node], sizeof(struct segment_info));
            int64_t tmp = remove_internal(glb_nodes[idx].right, 0, ver);
            glb_nodes[idx].right = tmp;
        }
    }
    return balance(idx, ver);
}


int64_t _update_newlines(struct segment *node)
{
    int64_t count = 0;
    char *pos = node->buffer->buffer + node->offset;
    for (int64_t i = 0; i < node->length; ++i)
    {
        count += *pos++ == '\n';
    }
    return count;
}


static int64_t insert_at_pos(int64_t root_idx, int64_t pos, struct segment_info info, int64_t ver) {
    // printf("Insert: length %lld at %lld\n", info.length, pos);
    if (root_idx == 0) 
    {
        int64_t new_node = glb_next_node++;
        // printf("B: allocated node %lld\n", new_node);
        memset(&glb_nodes[new_node], 0, sizeof(glb_nodes[new_node]));
        memcpy(&glb_nodes[new_node], &info, sizeof(info));
        glb_nodes[new_node].offset = info.offset;
        glb_nodes[new_node].newlines = _update_newlines(&glb_nodes[new_node]);
        update(new_node);
        return new_node;
    }

    int64_t current = _copy_to_version(root_idx, ver);
    int64_t left_idx = glb_nodes[current].left;
    int64_t left_size = _len(left_idx);

    if (pos <= left_size) 
    {
        int64_t tmp = insert_at_pos(left_idx, pos, info, ver);
        glb_nodes[current].left = tmp;
    }
    else 
    {
        int64_t tmp = insert_at_pos(glb_nodes[current].right, 
                                    pos - left_size - glb_nodes[current].length, 
                                    info, ver);
        glb_nodes[current].right = tmp;
    }
    return balance(current, ver);
}



/*
    insert segment into tree, creating new version, if node version isn't this_version
*/
struct segment *InsertSegment(struct segment *tree, struct segment_info info, int64_t position, int64_t this_version)
{    
    int64_t root_idx = (tree ? tree - glb_nodes : 0);
    int64_t new_root = insert_at_pos(root_idx, position, info, this_version);
    return &glb_nodes[new_root];
}

/*
    remove segment from tree, creating new version, if node version isn't this_version
*/
struct segment *RemoveSegment(struct segment *tree, int64_t position, int64_t this_version)
{
    int64_t root_idx = tree - glb_nodes;
    
    int64_t new_root = remove_internal(root_idx, position, this_version);
    return new_root ? &glb_nodes[new_root] : NULL;
}

/*
    get segment by position
*/
struct segment *GetSegment(struct segment *tree, int64_t position, int64_t *segment_start_pos)
{
    int64_t treeent_offset = 0;
    while (tree)
    {
        int64_t left_size = _len(tree->left);
        if (position < left_size)
        {
            tree = &glb_nodes[tree->left];
        }
        else if (position < left_size + tree->length)
        {
            if (segment_start_pos) *segment_start_pos = treeent_offset + left_size;
            return tree;
        }
        else
        {
            treeent_offset += left_size + tree->length;
            position -= (left_size + tree->length);
            tree = &glb_nodes[tree->right];
        }
    }
    return NULL;
}

int64_t SegmentLength(struct segment *tree)
{
    return (tree ? tree->total_length : 0);
}

// 0 indexation
int64_t SegmentNthNewline(int64_t node, int64_t n)
{
    if (!node || n < 0 || n >= _cnt(node)) return -1;

    if (n < _cnt(glb_nodes[node].left))
    {
        return SegmentNthNewline(glb_nodes[node].left, n);
    }
    else if (n < _cnt(glb_nodes[node].left) + glb_nodes[node].newlines)
    {
        int64_t count = 1 + n - _cnt(glb_nodes[node].left);
        const char *data = glb_nodes[node].buffer->buffer + glb_nodes[node].offset;
        for (int64_t i = 0; i < glb_nodes[node].length; i++)
        {
            count -= data[i] == '\n';
            if (count == 0)
            {   
                return _len(glb_nodes[node].left) + i;
            }
        }
    }
    else
    {
        int64_t res = SegmentNthNewline(glb_nodes[node].right, n - _cnt(glb_nodes[node].left) - glb_nodes[node].newlines);
        return res == -1 ? -1 : _len(glb_nodes[node].left) + glb_nodes[node].length + res;
    }
    return -1;
}


int64_t FindNearestLeft(int64_t node_id, int64_t position)
{
    if (!node_id || position < 0) return -1;

    struct segment *node = &glb_nodes[node_id];
    if (node->total_newlines == 0) return -1;

    int64_t left_len = _len(node->left);

    if (position >= left_len + node->length)
    {
        int64_t res = FindNearestLeft(node->right, position - left_len - node->length);
        if (res != -1) return left_len + node->length + res;
        position = left_len + node->length - 1;
    }

    if (position >= left_len && node->newlines > 0)
    {
        int64_t search_start = position - left_len;
        char *data = node->buffer->buffer + node->offset;
        for (int64_t i = search_start; i >= 0; i--)
        {
            if (data[i] == '\n')
            {
                return left_len + i;
            }
        }
        position = left_len - 1;
    }
    return node->left ? FindNearestLeft(node->left, position) : -1;
}


int64_t FindNearestRight(int64_t node_id, int64_t position)
{
    if (!node_id || position < 0) return -1;

    struct segment *node = &glb_nodes[node_id];
    if (node->total_newlines == 0) return -1;

    int64_t left_len = _len(node->left);
    int64_t node_end = left_len + node->length;

    if (position < left_len)
    {
        int64_t res = FindNearestRight(node->left, position);
        if (res != -1) return res;
        position = left_len;
    }

    if (position < node_end && node->newlines > 0)
    {
        int64_t search_start = position - left_len;
        char *data = node->buffer->buffer + node->offset;
        for (int64_t i = search_start; i < node->length; i++)
        {
            if (data[i] == '\n')
            {
                return left_len + i;
            }
        }
    }

    int64_t res = FindNearestRight(node->right, (position > node_end) ? (position - node_end) : 0);
    if (res != -1) return node_end + res;
    return -1;
}

// count newlines on [0 position)
int64_t SegmentGetLineNumber(int64_t root_idx, int64_t position)
{
    int64_t count = 0;
    int64_t cur = root_idx;

    while (cur && position > 0)
    {
        struct segment *node = &glb_nodes[cur];
        int64_t left_len = _len(node->left);

        if (position < left_len)
        {
            cur = node->left;
        }
        else if (position < left_len + node->length)
        {
            count += _cnt(node->left);
            char *data = node->buffer->buffer + node->offset;
            int64_t local_limit = position - left_len;
            for (int64_t i = 0; i < local_limit; i++)
            {
                count += data[i] == '\n';
            }
            break;
        }
        else
        {
            count += _cnt(node->left) + node->newlines;
            position -= (left_len + node->length);
            cur = node->right;
        }
    }
    return count;
}
