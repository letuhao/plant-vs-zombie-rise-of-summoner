# Goal: Build Human-Readable Mechanism User Guides

## Objective

Read `docs/guide/mechanisms/README.md` as the mechanism index, understand which mechanisms require user-facing documentation, then autonomously create and improve human-readable user guides.

The agent must operate as both:

1. **Documentation planner** — determine what the user needs to understand and how the guide should be structured.
2. **User blind-spot reviewer** — deliberately assume the reader does not have the author's internal context, then identify missing explanations, unexplained terminology, implicit assumptions, and confusing presentation.

The final result must be useful to a **human user**, not merely sufficient for another AI agent to reconstruct the implementation.

The workflow should produce both:

* Markdown documentation (`.md`)
* Corresponding HTML content where appropriate

The agent decides the exact implementation based on the repository structure and existing documentation conventions.

---

# Core Principle

## Write for the Human, Not the Agent

A mechanism guide is successful when a human can understand:

* What this mechanism is
* Why it exists
* When they should care about it
* What it does
* How it behaves
* How to use it
* What they can expect to happen
* What common mistakes or misunderstandings exist
* How it interacts with other relevant mechanisms

The guide must **not** assume that the reader knows:

* the source code
* internal architecture
* implementation terminology
* historical design decisions
* undocumented project conventions
* the author's mental model
* information that exists only in other files unless it is explicitly connected

Technical implementation details may be included when they help the reader understand or operate the mechanism, but implementation details are not the objective.

---

# Documentation Workflow Loop

The agent must execute this cycle:

```text
READ INDEX
    ↓
UNDERSTAND MECHANISM
    ↓
MAP USER KNOWLEDGE REQUIREMENTS
    ↓
PLAY USER BLIND-SPOT ROLE
    ↓
IDENTIFY DOCUMENTATION GAPS
    ↓
PLAN GUIDE
    ↓
IMPLEMENT GUIDE
    ↓
REVIEW AS HUMAN
    ↓
FIX GAPS
    ↓
VALIDATE
    ↓
REPEAT IF NECESSARY
```

This is a loop, not a one-shot generation task.

---

# Phase 1 — Read the Mechanism Index

Start by reading:

`docs/guide/mechanisms/README.md`

Treat this file as the authoritative mechanism index.

Determine:

* What mechanisms exist
* Which mechanisms already have documentation
* Which mechanisms appear incomplete
* How mechanisms are categorized
* How mechanisms relate to each other
* What naming and linking conventions already exist

Do not immediately start writing documentation.

First establish enough repository context to understand what the mechanism actually represents.

---

# Phase 2 — Understand the Mechanism

For each mechanism selected for documentation, investigate the repository as necessary.

The agent decides which files are relevant.

Potential sources include:

* source code
* configuration
* existing guides
* examples
* tests
* UI implementation
* schemas
* data structures
* related mechanisms
* existing HTML
* README files
* comments
* terminology used elsewhere in the project

Do not blindly read the entire repository.

Follow the mechanism's dependency and usage trail.

The goal is to build a **working mental model**, not to copy implementation details into the guide.

---

# Phase 3 — Map the Human Knowledge Requirements

Before writing, explicitly determine what a new human reader needs to know.

For example:

```text
Reader already knows:
- basic product concepts

Reader probably does not know:
- internal mechanism terminology
- why this mechanism exists
- hidden prerequisites
- interaction with mechanism X
- what happens when condition Y occurs

Reader needs to learn:
1. Concept
2. Purpose
3. Basic behavior
4. Usage
5. Examples
6. Edge cases
7. Relationships with other mechanisms
```

The exact structure is determined by the mechanism.

Do not force every mechanism into the same template if that makes the documentation unnatural.

---

# Phase 4 — Play the User Blind-Spot Role

Before implementation, deliberately switch perspective.

Pretend you are a human user who:

* has never read the source code
* does not know the author's assumptions
* encounters the mechanism for the first time
* only has the documentation and normal product knowledge available

Ask:

### Context

* Do I know what this thing is?
* Do I know why it exists?
* Do I understand where it fits?

### Terminology

* Is every important term introduced?
* Is jargon explained before being used?
* Are internal names presented as if they were obvious concepts?

### Behavior

* Can I predict what will happen?
* Are cause-and-effect relationships explained?
* Are important rules explicit?

### Usage

* Do I know when to use it?
* Do I know how to use it?
* Can I follow a concrete example?

### Boundaries

* Do I know what this mechanism does NOT do?
* Are limitations and edge cases clear?

### Relationships

* If another mechanism affects this one, is that relationship explained?
* Am I expected to jump between documents to understand basic behavior?

### Cognitive Load

* Is this a wall of text?
* Can I scan the document?
* Are important concepts visually separated?
* Are examples easier to understand than the implementation?

### Human Test

Ask:

> "If I gave this document to a competent user who has never seen the source code, what questions would they ask me after reading it?"

Those questions represent documentation gaps.

---

# Phase 5 — Build the Documentation Plan

Create a plan based on the identified blind spots.

The plan should determine:

* document structure
* sections
* concepts requiring explanation
* examples
* diagrams or tables if useful
* cross-links
* terminology explanations
* HTML presentation requirements
* information that should remain implementation-only

Do not optimize for maximum information density.

Optimize for **minimum reader confusion**.

Prefer:

```text
concept → explanation → example → consequence
```

over:

```text
implementation detail → implementation detail → implementation detail
```

---

# Phase 6 — Implement

Execute the plan.

Create or update the appropriate:

* Markdown guide
* HTML content
* indexes
* navigation
* cross-links
* supporting assets

Follow existing repository conventions where they exist.

Do not invent a new documentation architecture unnecessarily.

The agent is responsible for determining the correct location and format from repository context.

---

# Phase 7 — Human Readability Review

After implementation, read the resulting documentation as if you were the intended human reader.

Do NOT review it primarily as an author.

Look for:

* unexplained concepts
* unexplained terminology
* missing context
* implicit assumptions
* unnecessary implementation details
* excessive text density
* long paragraphs
* weak section hierarchy
* examples that do not actually teach the mechanism
* statements that are technically correct but practically confusing
* references to concepts that the reader has not encountered
* missing "why"
* missing "when"
* missing "what happens if..."
* documentation that requires source-code knowledge

If the guide fails the human-reader test, modify it.

---

# Phase 8 — Anti-Text-Wall Pass

Explicitly check whether the document has become a text wall.

Prefer appropriate combinations of:

* headings
* short paragraphs
* bullet lists
* numbered procedures
* tables
* examples
* callouts
* diagrams
* before/after comparisons
* small code/configuration snippets when useful

Do not mechanically convert everything into bullets.

Formatting exists to reduce cognitive load, not to make the document look structured.

A document can still be a text wall made entirely of short paragraphs.

---

# Phase 9 — Accuracy Validation

Verify that the guide is consistent with the actual implementation.

Check important claims against the repository.

Pay particular attention to:

* behavior
* defaults
* constraints
* prerequisites
* terminology
* examples
* interactions with other mechanisms
* configuration
* edge cases

Never invent behavior merely because it would make the guide easier to explain.

If implementation behavior is unclear, investigate further before documenting it.

---

# Phase 10 — Final User Test

Perform one final simulation:

> "I am a competent human user encountering this mechanism for the first time. Can I understand and use it without asking the author to explain the missing context?"

If **no**, return to the appropriate phase and improve the documentation.

If **yes**, finalize the guide.

---

# Important Constraints

## Do Not Generate AI-Only Documentation

The guide must not primarily optimize for:

* source-code retrieval
* embedding/search performance
* LLM context reconstruction
* exhaustive internal terminology
* implementation completeness

Those may be useful secondary properties.

The primary audience is a human.

---

## Do Not Dump Repository Knowledge

More information does not automatically produce better documentation.

Do not copy every discovered implementation detail into the guide.

For every piece of information, ask:

> "Does this help the human understand or use the mechanism?"

If not, leave it out.

---

## Do Not Assume Context

Never rely on:

> "The user will understand this because it is obvious from the code."

The reader does not have the code.

Never rely on:

> "This was explained somewhere else."

If another document is genuinely required, provide a clear link and enough local context to explain why the reader should follow it.

---

## Do Not Force a Universal Template

The workflow is mandatory.

The document structure is not.

Different mechanisms may require different presentation styles.

For example:

* A configuration mechanism may need a reference table.
* A gameplay mechanism may need examples and diagrams.
* A workflow may need a step-by-step tutorial.
* A complex interaction may need a sequence diagram.
* A conceptual mechanism may need an explanation-first structure.

The agent decides what format best teaches the mechanism.

---

# Definition of Done

A mechanism guide is complete only when:

* The mechanism is accurately understood.
* The guide explains what it is.
* The guide explains why it matters.
* The reader can understand its behavior.
* Important terminology is explained.
* Important assumptions are explicit.
* At least useful examples exist where applicable.
* Important interactions are explained.
* The document is scannable.
* The document does not unnecessarily expose implementation complexity.
* Markdown and HTML representations are consistent.
* Links/navigation work according to repository conventions.
* The agent has performed a deliberate blind-spot review.
* The guide has passed the final human-reader simulation.

---

# Operating Rule

**Never stop at "the documentation is technically correct."**

The workflow is successful only when the documentation is also **understandable without the author's hidden context**.

When uncertain, prioritize:

**clarity → context → usability → accuracy → completeness**

rather than:

**completeness → implementation detail → information density**.
