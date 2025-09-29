#ifndef EXPORT
#define EXPORT


#include "stddef.h"
#include "modification.h"



struct buffer;



int buffer_create(struct buffer **b);

int buffer_insert(struct buffer *b, size_t pos, size_t length, char *text);

int buffer_delete(struct buffer *b, size_t pos, size_t length);

int buffer_read(struct buffer *b, size_t pos, size_t length, char *buffer);



#endif
