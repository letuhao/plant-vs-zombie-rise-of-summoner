# Design Task: Hardcore RPG Derived Stats HUD

You are designing the **Derived Stats / Combat Analysis HUD** for *PvZ: Rise of Summoner*, a deep single-player RPG with complex character/plant/zombie progression.

This is NOT a casual RPG character sheet.

The target user is a **hardcore player who wants to understand exactly why their character has its current stats**, identify sources of power, compare builds, discover optimization opportunities, and trace calculations.

The current UI is too boring and generic. Do NOT produce a simple collection of cards, progress bars, or plain text stat rows.

## Core design goal

Create a **modern, premium, information-rich RPG statistics interface** that feels like a real game system rather than an admin dashboard.

The player should immediately feel:

> "This game has a deep underlying system, and this screen lets me inspect it."

The UI should communicate:

* complexity
* progression
* power
* causality
* relationships between stats
* sources and modifiers
* discoverability
* player experimentation

## IMPORTANT: Design from the data model, not from generic UI patterns

Every important derived stat should be explainable.

For example:

ATTACK POWER
= Base Attack

* Equipment
* Level Scaling
* Talent
* Buffs
* Passive
  × Multipliers
* Conditional Bonuses

The player must be able to inspect this chain.

A derived stat should never appear as an unexplained number.

---

# 1. Main HUD

Design a strong visual hierarchy.

The screen should contain:

### Header

* Character identity
* level / rank
* combat power
* relevant build context
* tabs/categories
* search
* filter
* grouping controls

### Primary stats area

Show the most important combat statistics with visually distinctive components.

Examples:

* HP
* ATK
* DEF
* Crit Rate
* Crit Damage
* Attack Speed
* Skill Power
* Resistance
* Penetration
* Lifesteal
* Accuracy
* Evasion

Do NOT render these as identical rectangular cards.

Use different visual treatments where appropriate:

* radial visualization
* segmented meters
* compact stat modules
* comparison indicators
* sparkline/trend visualization
* iconography
* numerical emphasis
* subtle VFX
* layered backgrounds

The interface should feel visually alive without becoming noisy.

---

# 2. Derived Stat Inspector

This is the most important feature.

When the player selects a stat, open a detailed inspector.

Example:

## ATTACK POWER

### 2,847

+312 from Equipment
+185 from Talent
+420 from Level
+96 from Passive
+18% multiplier
+245 conditional bonus

Then allow the player to drill down.

For example:

ATTACK POWER
↓
Equipment Contribution
↓
Weapon
↓
Weapon Base ATK
↓
Enhancement
↓
Affixes
↓
Set Bonus

The player should be able to understand the complete source chain.

Represent this visually as a **calculation graph / contribution tree / layered breakdown**, not merely a vertical list.

Use:

* connectors
* nodes
* expandable groups
* contribution magnitude
* positive/negative indicators
* multiplier indicators
* conditional badges
* hover/focus states

---

# 3. Contribution Visualization

For important stats, visualize where the power comes from.

Example:

ATK 2,847

Base          █████████
Equipment     ███████
Talents       ████
Passives      ███
Buffs         ██

But do NOT simply make generic progress bars.

Consider:

* stacked contribution bars
* radial contribution
* waterfall visualization
* branching source graph
* layered rings
* animated contribution flow

The visualization should help answer:

**"What is actually making me strong?"**

---

# 4. Comparison Mode

Hardcore players need comparison.

Support:

CURRENT vs PREVIOUS

CURRENT vs EQUIPPED

CURRENT vs ALTERNATIVE BUILD

CURRENT vs BASELINE

Example:

ATK
2,847 → 3,104
+257 (+9.0%)

Crit Rate
42.5% → 51.8%
+9.3%

Display improvements and losses clearly.

The player should be able to inspect **why the difference happened**, not just see the final numbers.

---

# 5. Filters and Grouping

This screen must support high information density.

Include controls such as:

Filter:

* Offensive
* Defensive
* Utility
* Elemental
* Critical
* Speed
* Survival
* Skill
* Hidden / Advanced

Source:

* Base
* Level
* Equipment
* Talent
* Passive
* Buff
* Debuff
* Set Bonus
* Temporary
* Conditional

Grouping:

* By Stat
* By Source
* By System
* By Equipment
* By Ability
* By Calculation Layer

Sorting:

* Highest contribution
* Biggest percentage
* Alphabetical
* Recently changed

Search should allow the player to quickly locate a specific stat.

---

# 6. Advanced / Hardcore Layer

Provide information that casual players do not normally need.

Examples:

Base Value
Flat Bonus
Percentage Bonus
Additive Multiplier
Multiplicative Multiplier
Final Value
Effective Value
Conditional Modifier
Soft Cap
Hard Cap
Conversion
Scaling Source

Use progressive disclosure.

Do not dump every number onto the first screen.

The interface should feel approachable initially but reveal enormous depth when inspected.

---

# 7. Visual Language

The design should resemble a **modern premium strategy/RPG interface**.

Think:

* sophisticated sci-fi fantasy
* tactical game UI
* holographic information panels
* layered glass/material surfaces
* luminous accents
* strong typography
* dense but organized information
* subtle gradients
* animated data
* elegant graphs
* meaningful iconography
* depth
* hierarchy

Avoid:

* Bootstrap-looking cards
* generic dashboard UI
* giant empty spaces
* identical cards repeated 20 times
* excessive rounded rectangles
* plain HTML tables
* basic progress bars everywhere
* default CSS colors
* generic purple neon AI UI
* childish PvZ UI
* mobile-app-looking layouts

This is a **desktop hardcore-game interface**, not a SaaS dashboard.

---

# 8. VFX / Motion

Use motion to communicate information, not decoration.

Examples:

* stat values animate when changed
* contribution sources visually flow into the derived value
* selecting a stat highlights its dependency chain
* increasing a value creates a subtle energy pulse
* positive changes have a short visual confirmation
* conditional modifiers can subtly animate when active
* graph nodes illuminate along the calculation path
* hover states expose deeper information
* transitions should feel fast and responsive

Avoid excessive particle effects.

VFX should reinforce the concept of **power flowing through the calculation system**.

---

# 9. Information Architecture

Design the UI around these questions:

1. What is my current value?
2. Is it good or bad?
3. What contributes to it?
4. Why did it change?
5. Which system gave me this power?
6. What happens if I change something?
7. Which source contributes the most?
8. What is currently active?
9. What is conditional?
10. Can I compare this with another build?
11. Are there diminishing returns or caps?
12. What should I optimize next?

If the design cannot answer these questions, it is incomplete.

---

# 10. Create a REAL prototype

Do not only produce a visual mockup.

Build a functional HTML/CSS/JS prototype.

Use realistic fake RPG data.

The prototype should demonstrate:

* stat selection
* filtering
* grouping
* expanding/collapsing sources
* derived-stat calculation breakdown
* comparison mode
* hover information
* visual contribution graphs
* state changes
* animations
* responsive desktop layout

The prototype should contain enough fake data to demonstrate the intended information density.

---

# 11. Visual quality bar

Before considering the design complete, ask:

### Does this look like a premium RPG system?

If it looks like:

* a Bootstrap dashboard
* a CRUD application
* a spreadsheet
* a generic character sheet
* a collection of cards

then the design has failed.

The UI must have a **strong visual identity**.

---

# 12. Design process

Do NOT immediately implement the first idea.

First:

1. Define the information architecture.
2. Identify the major visual components.
3. Decide how derived-stat relationships are represented.
4. Decide how contribution/source relationships are visualized.
5. Create the HTML prototype.
6. Review the visual hierarchy.
7. Improve the design.
8. Only then treat the prototype as the implementation specification.

The HTML prototype is the **source of truth for visual implementation**.

Do not later reinterpret the design into a simpler UI.

If a visual element exists in the approved prototype, preserve its:

* hierarchy
* position
* proportions
* spacing
* typography
* visual treatment
* interaction
* animation intent

Do not replace complex visual components with generic cards merely because they are easier to implement.

---

# Final requirement

The result should feel like a **combat scientist's analysis console inside a sophisticated RPG**.

The player should want to spend time inside this screen experimenting with builds.

This is not merely a screen that displays stats.

It is a **tool for understanding the game's combat system**.
