#include "structure.h"
#include "stdlib.h"
#include "threading.h"
#include "text_api.h"


static void CalculateHash(struct state *state)
{
	int64_t h_hi = 0xCBF29CE484222325, h_lo = 0xCBF29CE484222325, buffer[2], i = 0, len = SegmentLength(state->value);
	/* calculate hash */
	for (; i + sizeof(buffer) < len; i += sizeof(buffer))
	{
		state_read(state, i, sizeof(buffer), (char *)buffer);
		h_hi ^= buffer[0];
		h_lo ^= buffer[1];
		h_hi *= 0x100000001B3;
		h_lo *= 0x100000001B3;
	}
	if (i < len) 
	{
		memset(buffer, 0, sizeof(buffer));
		state_read(state, i, len - i, (char *)buffer);
		h_hi ^= buffer[0];
		h_lo ^= buffer[1];
		h_hi *= 0x100000001B3;
		h_lo *= 0x100000001B3;
	}
	h_hi += len;
	h_lo -= len;
	state->hash.total_hash[0] = h_lo;
	state->hash.total_hash[1] = h_hi;
	state->hash.calculated = 1;
	printf("hash of state %p is %llx%llx\n", state, h_hi, h_lo);
}


int HashEvaluationWorker(void *param)
{
	struct project *project = param;

	while (WaitForSingleObject(project->StopEvent, 1) == WAIT_TIMEOUT)
	{
		/* iterate throug states, if there is any without hash - calculate this hash */
		struct state *state = NULL;
		lockShared(&project->lock);
		for (int64_t i = 0; i < project->states_len; ++i)
		{
			if (!project->states[i]->hash.calculated && project->states[i]->committed && !project->states[i]->merged_to)
			{
				state = project->states[i];
				break;
			}
		}
		freeShared(&project->lock);
		if (state != NULL)
		{
			CalculateHash(state);
		}
		else
		{
			msleep(50);
		}
	}
	return 0;
}