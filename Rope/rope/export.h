#ifndef EXPORT
#define EXPORT


// #define BAN_READ_WRONG_PIECES


#include "stddef.h"

#include "modification.h"
#include "buffer.h"




int buffer_init(struct buffer *b);

int buffer_destroy(struct buffer *b);

int buffer_moditify(struct buffer *b, struct modification *mod);

int buffer_get_size(struct buffer *b, ssize_t *length);

int buffer_read(struct buffer *b, ssize_t from, ssize_t length, char *buffer);


#endif
