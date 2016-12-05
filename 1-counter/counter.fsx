#r "node_modules/fable-core/Fable.Core.dll"
#r "node_modules/fable-arch/Fable.Arch.dll"
open Fable.Core
open Fable.Import
open Fable.Import.Browser
open Fable.Arch
open Fable.Arch.App
open Fable.Arch.Html

// ------------------------------------------------------------------------------------------------
// Introducing Elm-style architecture with Fable
// ------------------------------------------------------------------------------------------------

// TODO #1: Look at the sample below. Check what kind of events are there
// (this is the `Update` type), what do we store in the `Model` and look
// at the type signatures of `update` and `render` to understand things!

// TODO #2: Add "Reset" button - to do this:
//  1) Add a new case `Reset` to the `Update` discriminated union
//  2) Implement the handling of `Reset` in `update` function
//  3) Create the "Reset" button in the `render` function

// ------------------------------------------------------------------------------------------------
// Domain model - update events and application state
// ------------------------------------------------------------------------------------------------

type Update =
  | Increment
  | Decrement

type Model =
  { Count : int }

// ------------------------------------------------------------------------------------------------
// Given an old state and update event, produce a new state
// ------------------------------------------------------------------------------------------------

let update state = function
  | Increment -> { Count = state.Count + 1 }
  | Decrement -> { Count = state.Count - 1 }

// ------------------------------------------------------------------------------------------------
// Render page based on the current state
// ------------------------------------------------------------------------------------------------

let render state =
  div [] [
    p [] [ text (string state.Count) ]
    button
      [ onMouseClick (fun _ -> Increment) ]
      [ text "+1" ]
    button
      [ onMouseClick (fun _ -> Decrement) ]
      [ text "-1" ]
  ]

// ------------------------------------------------------------------------------------------------
// Start the application with initial state
// ------------------------------------------------------------------------------------------------

let initial =
  { Count = 0 }

createSimpleApp initial render update (Virtualdom.createRender)
|> withStartNodeSelector "#counter"
|> start
