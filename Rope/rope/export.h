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
    
ROPE_EXPORT int buffer_create(struct buffer **buf);

ROPE_EXPORT int buffer_moditify_star(struct buffer *buf, uint64_t type, int64_t pos, int64_t len, char *text);



/* general functinos */

ROPE_EXPORT int buffer_init(struct buffer *buf);

ROPE_EXPORT int buffer_destroy(struct buffer *buf);

ROPE_EXPORT int buffer_moditify(struct buffer *buf, struct modification *mod);

ROPE_EXPORT int buffer_get_size(struct buffer *buf, int64_t version, int64_t *length);

ROPE_EXPORT int buffer_read(struct buffer *buf, int64_t version, int64_t from, int64_t length, char *buffer);

ROPE_EXPORT int buffer_version_set(struct buffer *buf, int64_t version);

ROPE_EXPORT int buffer_version_get(struct buffer *buf, int64_t *version);

ROPE_EXPORT int buffer_version_lca(struct buffer *buf, int64_t version_a, int64_t version_b, int64_t *result);

ROPE_EXPORT int buffer_version_before(struct buffer *buf, int64_t version, int64_t steps, int64_t *result);

ROPE_EXPORT int buffer_read_versions_count(struct buffer *buf, int64_t *result);

ROPE_EXPORT int buffer_read_versions(struct buffer *buf, int64_t count, int64_t *result);

ROPE_EXPORT int buffer_set_version_cursors(struct buffer *buf, int64_t version, int64_t count, struct cursor_t *cursors);

ROPE_EXPORT int buffer_get_version_cursors_count(struct buffer *buf, int64_t version, int64_t *count);

ROPE_EXPORT int buffer_get_version_cursors(struct buffer *buf, int64_t version, int64_t count, struct cursor_t *cursors);

ROPE_EXPORT int buffer_get_offsets(struct buffer *buf, int64_t version, int64_t position, int64_t *result_line, int64_t *result_column);



#ifdef __cplusplus
}
#endif


#undef ROPE_EXPORT


#endif


