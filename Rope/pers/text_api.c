#include "inttypes.h"
#include "assert.h"

#include "text_api.h"
#include "structure.h"


/* creation and delection */


void _reserve_state(struct project *project, int64_t total_size)
{
	if (project->states_alloc < total_size)
	{
		while (project->states_alloc < total_size)
		{
			project->states_alloc *= 2;
			project->states_alloc |= 1;
		}
		void *oldPtr = project->states;
		project->states = realloc(oldPtr, sizeof(*project->states) * project->states_alloc);
		if (project->states == NULL)
		{
			exit(1);
		}
	}
}


struct project *project_create()
{
	struct project *project = malloc(sizeof(*project));
	InitializeSRWLock(&project->lock);
	project->states_len = 0;
	project->states_alloc = 0;
	project->states = NULL;
	project->current_buffer = allocate_buffer(1024 * 1024);
	_reserve_state(project, 1024);
	return project;
}

void project_destroy(struct project *project)
{
	for (int64_t i = 0; i < project->states_len; ++i)
	{
		state_release(project->states[i]);
	}
}

struct state *project_new_state(struct project *project)
{
	return state_create_empty(project);
}

/* versioning */

struct state *state_version_before(struct state *state, int64_t steps)
{
	for (int64_t i = 0; i < steps; ++i)
	{
		if (state->previous_versions_len > 0)
		{
			state = state->previous_versions[0];
		}
	}
	return state;
}

void project_get_states_len(struct project *project, int64_t *states_count, int64_t *links_count)
{
	*states_count = project->states_len;
	lockShared(&project->lock);
	int64_t count = 0;
	for (int64_t i = 0; i < project->states_len; ++i)
	{
		count += project->states[i]->next_versions_len;
	}
	printf("found %lld links\n", count);
	freeShared(&project->lock);
	*links_count = count;
}


void project_get_states(struct project *project, int64_t states_count, struct state **result, int64_t links_count, struct link *links)
{
	lockShared(&project->lock);
	memcpy(result, project->states, sizeof(*project->states) * states_count);
	struct link *links_end = links + links_count, *links_begin = links;
	for (int64_t i = 0; i < project->states_len; ++i)
	{
		struct state *st = project->states[i];
		for (int64_t j = 0; j < st->next_versions_len && links < links_end; ++j)
		{
			assert(st->next_versions[j]);
			*links++ = (struct link){ st, st->next_versions[j] };
		}
	}
	printf("exported %lld links\n", links_end - links_begin);
	freeShared(&project->lock);
}
