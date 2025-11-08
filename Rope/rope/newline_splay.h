#ifndef NEWLINE_SPLAY
#define NEWLINE_SPLAY


#include "inttypes.h"


struct newline_tree;


/*
    this is array
*/
struct newline_tree *nltree_create();
void nltree_free(struct newline_tree *tree);
void nltree_insert(struct newline_tree *tree, int64_t pos, int64_t value);
void nltree_remove(struct newline_tree *tree, int64_t pos);
int64_t nltree_lowerbound(struct newline_tree *tree, int64_t pos);
void nltree_update(struct newline_tree *tree, int64_t l, int64_t r, int64_t value);


#endif
