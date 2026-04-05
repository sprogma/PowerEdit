#include "inttypes.h"
#include "assert.h"

#include "text_api.h"
#include "structure.h"


/* creation and delection */


void _reserve_states(struct project *project, int64_t total_size)
{
	if (project->states_alloc < total_size)
	{
		int64_t oldSize = project->states_alloc;
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
		memset(project->states + oldSize, 0, sizeof(*project->states) * (project->states_alloc - oldSize));
	}
}

void _reserve_buffers(struct project *project, int64_t total_size)
{
	if (project->buffers_alloc < total_size)
	{
		int64_t oldSize = project->buffers_alloc;
		while (project->buffers_alloc < total_size)
		{
			project->buffers_alloc *= 2;
			project->buffers_alloc |= 1;
		}
		void *oldPtr = project->buffers;
		project->buffers = realloc(oldPtr, sizeof(*project->buffers) * project->buffers_alloc);
		if (project->buffers == NULL)
		{
			exit(1);
		}
		memset(project->buffers + oldSize, 0, sizeof(*project->buffers) * (project->buffers_alloc - oldSize));
	}
}


void _project_add_buffer(struct project* project, struct mapped_buffer *buffer)
{
	_reserve_buffers(project, project->buffers_len + 1);
	project->buffers[project->buffers_len++] = buffer;
}


struct project *project_create()
{
	struct project *project = malloc(sizeof(*project));
	InitializeSRWLock(&project->lock);
	project->states_len = 0;
	project->states_alloc = 0;
	project->states = NULL;
	project->buffers_len = 0;
	project->buffers_alloc = 0;
	project->buffers = NULL;

	project->current_buffer = allocate_buffer(1024 * 1024);
	_project_add_buffer(project, project->current_buffer);
#ifdef _WIN32
	project->StopEvent = CreateEvent(NULL, TRUE, FALSE, NULL);
#endif
	_reserve_states(project, 1024);


	project->HashWorker = StartNewThread(HashEvaluationWorker, project);
	project->StatesMerger = StartNewThread(StatesMergeWorker, project);

	return project;
}

void project_destroy(struct project *project)
{
	SetEvent(project->StopEvent);
	WaitForSingleObject(project->HashWorker, INFINITE);
	WaitForSingleObject(project->StatesMerger, INFINITE);
	for (int64_t i = 0; i < project->states_len; ++i)
	{
		state_release(project->states[i]);
	}
	for (int64_t i = 0; i < project->buffers_len; ++i)
	{
		delete_buffer(project->buffers[i]);
	}
	free(project->states);
	free(project);
}

struct state *project_new_state(struct project *project)
{
	return state_create_empty(project);
}

/* versioning */

struct state *state_version_before(struct state *state, int64_t steps)
{
	while (state->merged_to) state = state->merged_to;
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
	lockShared(&project->lock);
	int64_t count = 0, len = 0;
	for (int64_t i = 0; i < project->states_len; ++i)
	{
		if (!project->states[i]->merged_to)
		{
			len++;
			for (int64_t j = 0; j < project->states[i]->next_versions_len; ++j)
			{
				if (!project->states[j]->merged_to)
				{
					count++;
				}
			}
		}
	}
	freeShared(&project->lock);
	*states_count = len;
	*links_count = count;
}


void project_get_states(struct project *project, int64_t states_count, struct state **result, int64_t links_count, struct link *links)
{
	lockShared(&project->lock);
	struct link *links_end = links + links_count, *links_begin = links;
	for (int64_t i = 0; i < project->states_len; ++i)
	{
		if (!project->states[i]->merged_to)
		{
			struct state *st = project->states[i];
			*result++ = st;
			for (int64_t j = 0; j < st->next_versions_len && links < links_end; ++j)
			{
				if (!project->states[j]->merged_to)
				{
					*links++ = (struct link){ st, state_resolve(st->next_versions[j]) };
				}
			}
		}
	}
	freeShared(&project->lock);
}

struct state *state_resolve(struct state *state)
{
	while (state->merged_to) state = state->merged_to;
	return state;
}