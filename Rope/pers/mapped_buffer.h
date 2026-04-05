#ifndef MAPPED_BUFFER_H
#define MAPPED_BUFFER_H


#include "inttypes.h"

struct state;
struct project;

struct mapped_buffer
{
    char *buffer;
    int64_t length;
    int64_t allocated;
};

int create_buffer_from_save(struct project *project, struct state *state, const char *filename, struct state **result_state, struct mapped_buffer **result_buffer);
struct mapped_buffer *allocate_buffer_from_file(const char *filename);
struct mapped_buffer *allocate_buffer(int64_t size);
void delete_buffer(struct mapped_buffer *);
void acquire_buffer(struct mapped_buffer *);
void release_buffer(struct mapped_buffer *);

#endif
