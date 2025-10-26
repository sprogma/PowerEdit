#ifndef EXPORT
#define EXPORT


// #define BAN_READ_WRONG_PIECES


#ifdef _WIN32
    #define ROPE_EXPORT __declspec(dllexport)
#endif


#include "stddef.h"

#include "modification.h"
#include "buffer.h"


#ifdef __cplusplus
extern "C"
{
#endif

/* for c#, to not deal with memory management */
    
ROPE_EXPORT int buffer_create(struct buffer **b);

ROPE_EXPORT int buffer_moditify_star(struct buffer *b, uint64_t type, ssize_t pos, ssize_t len, char *text);



/* general functinos */

ROPE_EXPORT int buffer_init(struct buffer *b);

ROPE_EXPORT int buffer_destroy(struct buffer *b);

ROPE_EXPORT int buffer_moditify(struct buffer *b, struct modification *mod);

ROPE_EXPORT int buffer_get_size(struct buffer *b, ssize_t *length);

ROPE_EXPORT int buffer_read(struct buffer *b, ssize_t from, ssize_t length, char *buffer);

ROPE_EXPORT int buffer_version_set(struct buffer *b, ssize_t version);

ROPE_EXPORT int buffer_version_get(struct buffer *b, ssize_t *version);

ROPE_EXPORT int buffer_version_lca(struct buffer *b, ssize_t version_a, ssize_t version_b, ssize_t *result);

ROPE_EXPORT int buffer_version_before(struct buffer *b, ssize_t version, ssize_t steps, ssize_t *result);

ROPE_EXPORT int buffer_read_versions_count(struct buffer *b, ssize_t *result);

ROPE_EXPORT int buffer_read_versions(struct buffer *b, ssize_t count, ssize_t *result);


#ifdef __cplusplus
}
#endif


#undef ROPE_EXPORT


#endif
