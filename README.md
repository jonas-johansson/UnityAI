# UnityAI

Quick and dirty implementation of a behavior tree system in C# and Unity using a custom scripting language to craft your AI behaviors.
* Easy to use
* Extensible
* Simple yet powerful
* Breakpoints
* Examples

## Motivation

Unity has built-in support for physics, rendering, input, sound, etc., but there's nothing for artificial intelligence (AI).

## Existing Solutions

There are a couple of existing solutions that seemed very competent but just too overkill, too expensive, and too visual for me.
I just wanted something small that I could fiddle around with.

If you're looking for a complete package with visual tools, these seem popular:
* Behave 2
* Behavior Designer
* RAIN
* Behaviour Machine
* NodeCanvas 2
* React

## My Solution

I wanted to be able to program nodes in a behavior tree, and describe their relationship in some way.

#### Attempt 1: Visual Designer
My first attempt was to go visual, but I realized that I didn't care too much about that - I really just wanted to get going and write some behaviors.

#### Attempt 2: XML
I then looked at some way to describe the relationship and the data configuration of the nodes. I started out with XML, which is a great format for schema validation etc., but the syntax felt too instrusive.

#### Attempt 3: JSON
I looked at representing the behavior tree in JSON, but the hierarchy became very deep and the vertical span was far too high.

#### Attempt 4: Custom scripting language
I settled for a simple scripting language where tabs represent the nodes' hierarchical relationship, and I steered away from characters like [](){}<>; etc to make it as airy as possible.
