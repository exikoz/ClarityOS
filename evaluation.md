# AI Output Evaluation

This document evaluates the quality and limitations of the AI-generated task rescheduling proposals produced by the Gemini API integration in ClarityOS.

## Quality Criteria

A "good" AI response meets these criteria:

| Criterion | Definition |
|-----------|-----------|
| Relevance | The proposal directly addresses the user's rescheduling instruction |
| Correctness | taskIds match input exactly, dates are valid and in the future |
| Format compliance | Output is valid JSON array with all required fields |
| Conciseness | Titles under 100 chars, descriptions under 300 chars |
| No hallucination | No invented tasks, no fabricated context |
| Language consistency | Response language matches the user prompt language |

## Known Limitations

- **Prompt sensitivity**: Small changes in wording can produce very different scheduling logic. "Move to next week" vs "postpone by 7 days" may yield different results.
- **Bias toward weekdays**: The model tends to schedule tasks on Monday-Friday even when weekends are valid.
- **Garbage in, garbage out**: Vague prompts like "fix my schedule" produce generic responses with little actionable value.
- **Hallucination risk**: When given few tasks, the model occasionally invents additional context in the description field.
- **Date reasoning**: The model sometimes miscalculates relative dates (e.g., "next Thursday" when today is Wednesday may land on the wrong week).
- **No domain knowledge**: The model has no awareness of holidays, personal calendars, or workload capacity.

## Test Prompts and Results

### Test 1: Clear instruction with specific date

**Prompt**: "Move all tasks to next Monday"  
**Tasks**: 3 tasks with various due dates  
**Result**:
- All 3 tasks received proposedDueDate of the correct next Monday
- taskIds matched exactly
- Titles were preserved, descriptions updated with "rescheduled to Monday"
- Valid JSON, no code fences

**Verdict**: Good. Clear instructions produce reliable output.

### Test 2: Vague instruction

**Prompt**: "Make my schedule better"  
**Tasks**: 3 tasks (2 overdue, 1 upcoming)  
**Result**:
- The model spread tasks across the next 5 days
- Descriptions contained generic text like "optimized for better workflow"
- One task got a new title that changed the meaning ("Buy groceries" became "Grocery shopping optimization")
- Valid JSON format

**Verdict**: Mixed. Format was correct but the model hallucinated meaning into the titles. The vague prompt gave the model too much freedom.

**Change made**: Added rule 5 to the system prompt: "If you are unsure about a scheduling decision, state your uncertainty in the proposedDescription field." This reduced creative title changes in subsequent tests.

### Test 3: Conflicting instruction

**Prompt**: "Schedule everything for yesterday"  
**Tasks**: 2 tasks  
**Result**:
- The model correctly refused to set dates in the past (rule enforcement worked)
- It set dates to today instead and noted in the description: "Cannot schedule in the past, moved to earliest valid date"
- Valid JSON, correct taskIds

**Verdict**: Good. The system prompt rule about future dates was respected. The model handled the conflict gracefully.

## What Was Changed and Why

1. **Added explicit field length limits** (title < 100 chars, description < 300 chars) to prevent verbose AI output that breaks UI layouts.
2. **Added uncertainty rule** ("if unsure, say so") to reduce hallucinated confidence in vague scenarios.
3. **Added few-shot example** in the system prompt to anchor the expected output format. This reduced code-fence wrapping from ~30% of responses to near zero.
4. **Lowered temperature to 0.1** to prioritize consistency over creativity for a scheduling task.

## Conclusion

The AI service is suitable for:
- Bulk rescheduling with clear, specific instructions (dates, relative offsets)
- Generating draft proposals that a human reviews before accepting

The AI service is NOT suitable for:
- Autonomous scheduling without human review
- Complex constraint satisfaction (dependencies between tasks, resource conflicts)
- Vague or ambiguous instructions where the "right answer" is subjective

The accept/reject workflow in ClarityOS mitigates the main risks by keeping a human in the loop. No proposal is applied automatically.
