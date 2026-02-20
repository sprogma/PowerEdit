#include "structure.h"


#define MAX_NODES 1000000
struct segment glb_nodes[MAX_NODES];
int64_t glb_next_node = 1;


#define _len(n) ((glb_nodes[n].buffer) ? glb_nodes[n].total_length : 0)
#define _hgt(n) ((glb_nodes[n].buffer) ? glb_nodes[n].height : 0)



static void _inc_ref(int64_t node) 
{
    if (node) glb_nodes[node].links_count++;
}

static void _dec_ref(int64_t node) 
{
    if (!node) return;
    glb_nodes[node].links_count--;
    if (glb_nodes[node].links_count == 0)
    {
        release_buffer(glb_nodes[node].buffer);
        _dec_ref(glb_nodes[node].left);
        _dec_ref(glb_nodes[node].right);
    }
}

static int64_t _copy_to_version(int64_t node, int64_t this_version) 
{
    if (!node || glb_nodes[node].version_id == this_version) return node;

    int64_t new_node = glb_next_node++;
    memcpy(&glb_nodes[new_node], &glb_nodes[node], sizeof(struct segment));
    glb_nodes[new_node].version_id = this_version;
    glb_nodes[new_node].links_count = 0;
    
    _inc_ref(glb_nodes[new_node].left);
    _inc_ref(glb_nodes[new_node].right);
    
    return new_node;
}

static void update(int64_t node) 
{
    if (!node) return;
    glb_nodes[node].total_length = _len(glb_nodes[node].left) + _len(glb_nodes[node].right) + glb_nodes[node].length;
    int hl = _hgt(glb_nodes[node].left);
    int hr = _hgt(glb_nodes[node].right);
    glb_nodes[node].height = (hl > hr ? hl : hr) + 1;
}


static int64_t rotate_right(int64_t y, int64_t ver) 
{
    y = _copy_to_version(y, ver);
    int64_t x = _copy_to_version(glb_nodes[y].left, ver);
    
    _inc_ref(glb_nodes[x].right);
    _dec_ref(glb_nodes[y].left);
    glb_nodes[y].left = glb_nodes[x].right;
    
    _inc_ref(y);
    _dec_ref(glb_nodes[x].right);
    glb_nodes[x].right = y;
    
    update(y); 
    update(x);
    return x;
}

static int64_t rotate_left(int64_t x, int64_t ver) 
{
    x = _copy_to_version(x, ver);
    int64_t y = _copy_to_version(glb_nodes[x].right, ver);
    
    _inc_ref(glb_nodes[y].left);
    _dec_ref(glb_nodes[x].right);
    glb_nodes[x].right = glb_nodes[y].left;
    
    _inc_ref(x);
    _dec_ref(glb_nodes[y].left);
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
            _inc_ref(tmp);
            _dec_ref(glb_nodes[idx].left);
            glb_nodes[idx].left = tmp;
        }
        return rotate_right(idx, ver);
    }
    if (balance_factor < -1) 
    {
        if (_hgt(glb_nodes[glb_nodes[idx].right].right) < _hgt(glb_nodes[glb_nodes[idx].right].left))
        {
            int64_t tmp = rotate_right(glb_nodes[idx].right, ver);
            _inc_ref(tmp);
            _dec_ref(glb_nodes[idx].right);
            glb_nodes[idx].right = tmp;
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
        _inc_ref(tmp);
        _dec_ref(glb_nodes[idx].left);
        glb_nodes[idx].left = tmp;
    } 
    else if (pos >= left_len + glb_nodes[idx].length) 
    {
        int64_t tmp = remove_internal(glb_nodes[idx].right, pos - left_len - glb_nodes[idx].length, ver);
        _inc_ref(tmp);
        _dec_ref(glb_nodes[idx].right);
        glb_nodes[idx].right = tmp;
    } 
    else 
    {
        if (!glb_nodes[idx].left || !glb_nodes[idx].right) 
        {
            int64_t tmp = glb_nodes[idx].left ? glb_nodes[idx].left : glb_nodes[idx].right;
            _inc_ref(tmp);
            _dec_ref(idx);
            return tmp;
        } 
        else 
        {
            int64_t temp_node = get_leftmost_child(glb_nodes[idx].right);
            memcpy(&glb_nodes[idx], &glb_nodes[temp_node], sizeof(struct segment_info));
            int64_t tmp = remove_internal(glb_nodes[idx].right, 0, ver);
            _inc_ref(tmp);
            _dec_ref(glb_nodes[idx].right);
            glb_nodes[idx].right = tmp;
        }
    }
    return balance(idx, ver);
}


static int64_t insert_at_pos(int64_t root_idx, int64_t pos, struct segment_info info, int64_t ver) {
    if (root_idx == 0) 
    {
        int64_t new_node = glb_next_node++;
        memset(&glb_nodes[new_node], 0, sizeof(glb_nodes[new_node]));
        memcpy(&glb_nodes[new_node], &info, sizeof(info));
        glb_nodes[new_node].offset = info.offset;
        glb_nodes[new_node].length = info.length;
        update(new_node);
        return new_node;
    }

    int64_t current = _copy_to_version(root_idx, ver);
    int64_t left_idx = glb_nodes[current].left;
    int64_t left_size = _len(left_idx);

    if (pos <= left_size) 
    {
        int64_t tmp = insert_at_pos(left_idx, pos, info, ver);
        _inc_ref(tmp);
        _dec_ref(glb_nodes[current].left);
        glb_nodes[current].left = tmp;
    }
    else 
    {
        int64_t tmp = insert_at_pos(glb_nodes[current].right, 
                                    pos - left_size - glb_nodes[current].length, 
                                    info, ver);
        _inc_ref(tmp);
        _dec_ref(glb_nodes[current].right);
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
    _inc_ref(new_root);
    _dec_ref(root_idx);
    return &glb_nodes[new_root];
}

/*
    remove segment from tree, creating new version, if node version isn't this_version
*/
struct segment *RemoveSegment(struct segment *tree, int64_t position, int64_t this_version)
{
    int64_t root_idx = tree - glb_nodes;
    
    int64_t new_root = remove_internal(root_idx, position, this_version);
    _inc_ref(new_root);
    _dec_ref(root_idx);
    return new_root ? &glb_nodes[new_root] : NULL;
}

/*
    get segment by position
*/
struct segment *GetSegment(struct segment *tree, int64_t position) 
{
    if (!tree) return NULL;

    int64_t left_size = _len(tree->left);
    
    if (position < left_size) 
    {
        return GetSegment(&glb_nodes[tree->left], position);
    } 
    else if (position < left_size + tree->length) 
    {
        return tree;
    } 
    else 
    {
        return GetSegment(&glb_nodes[tree->right], position - left_size - tree->length);
    }
}
