#ifndef EXPORT_API
#define EXPORT_API


// #define BAN_READ_WRONG_PIECES


#ifdef _WIN32
#define ROPE_EXPORT __declspec(dllexport)
#endif


#include "stddef.h"

#include "structure.h"


#ifdef __cplusplus
extern "C"
{
#endif

/* creation and delection */

ROPE_EXPORT struct project *project_create();

ROPE_EXPORT void project_destroy(struct project *project);

ROPE_EXPORT struct state *project_new_state(struct project *project);

ROPE_EXPORT struct state *state_create_dup(struct project *project, struct state *state);

/* modifications */

ROPE_EXPORT void state_moditify(struct project *project, struct state *state, int64_t position, int64_t type, int64_t length, char *buffer);

ROPE_EXPORT void state_commit(struct project *project, struct state *state);

/* reading */

ROPE_EXPORT int64_t state_get_size(struct state *state);

ROPE_EXPORT void state_read(struct state *state, int64_t position, int64_t length, char *buffer);

/* versioning */

ROPE_EXPORT struct state *state_version_before(struct state *state, int64_t steps);

ROPE_EXPORT void project_get_states_len(struct project *project, int64_t *states_count, int64_t *links_count);

struct link
{
	struct state *parent;
	struct state *child;
};
ROPE_EXPORT void project_get_states(struct project *project, int64_t states_count, struct state **result, int64_t links_count, struct link *links);

ROPE_EXPORT void state_set_cursors(struct state *state, int64_t count, struct cursor *cursors);

ROPE_EXPORT int64_t state_get_cursors_count(struct state *state);

ROPE_EXPORT void state_get_cursors(struct state *state, int64_t count, struct cursor *result);

ROPE_EXPORT void state_get_offsets(struct state *state, int64_t position, int64_t *result_line, int64_t *result_column);



#ifdef __cplusplus
}
#endif


#undef ROPE_EXPORT


#endif


