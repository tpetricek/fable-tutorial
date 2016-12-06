#r "node_modules/fable-core/Fable.Core.dll"
#r "node_modules/fable-arch/Fable.Arch.dll"
open System
open Fable.Core
open Fable.Core.JsInterop
open Fable.Import
open Fable.Import.Browser
open Fable.Arch
open Fable.Arch.App
open Fable.Arch.Html


// ------------------------------------------------------------------------------------------------
// Implementing TODO list app with Fable
// ------------------------------------------------------------------------------------------------

// TODO #1: The sample code keeps track of the current text in the input
// box - understand how this works! Our model is just `string` and every
// time the input changes, we trigger `Input` event with a new value of the
// input (which we get from the element). This way, the current state is
// always the value in the textbox. We will need this later when adding new
// todo items to the list.

// TODO #2: Add support for adding items & rendering them:
//  * Modify the model so that it contains the current input (string)
//    together with a list of work items (list of strings) and update
//    the `initial` value and the `render` function to render `<li>` nodes
//  * Add a new event (`Create`) and a click handler for the button that
//    triggers it. Modify the `update` function to add the new item.

// TODO #3: Add support for removing items once they are done. To do this,
// we will need to track a unique ID to identify items. Add `NextId` to
// your `Model` record & increment it each time you add an item. Change the
// list of items to be of type `int * string` with ID and text and when
// the `X` button is clicked, trigger a new event `Remove(id)`. Then you
// just need to change the `update` function to remove the item from the list!

// ------------------------------------------------------------------------------------------------
// Domain model - update events and application state
// ------------------------------------------------------------------------------------------------

type Update =
  | Input of string
  | Add
  | Remove of Guid

type Todo =
  { Id : Guid; Text: string }

type Model =
  { Input : string; Todos: Todo list }

// ------------------------------------------------------------------------------------------------
// Given an old state and update event, produce a new state
// ------------------------------------------------------------------------------------------------

let update state = function
  | Input s -> 
      { state with Input = s }
  | Add -> 
      let todo = { Id = Guid.NewGuid(); Text = state.Input }
      { state with Input = ""; Todos = todo::state.Todos }
  | Remove id -> 
      let filtered = state.Todos |> List.filter (fun x -> x.Id <> id)
      { state with Todos = filtered }

// ------------------------------------------------------------------------------------------------
// Render page based on the current state
// ------------------------------------------------------------------------------------------------

let render (state: Model) =
  div [] [
    ul [] [
      for todo in state.Todos ->
        li [] [
          text todo.Text
          a [
            property "href" "#"
            onMouseClick (fun _ -> Remove todo.Id)
          ] [ span [] [ text "X" ] ]
        ]
    ]
    input [
      property "value" state.Input
      onInput (fun d -> Input(unbox d?target?value))
    ]
    button
      [ onMouseClick (fun _ -> Add)]
      [ text "Add" ]
  ]

// ------------------------------------------------------------------------------------------------
// Start the application with initial state
// ------------------------------------------------------------------------------------------------

let initial =
  { Input = ""
    Todos =
    [ { Id = Guid.NewGuid(); Text = "First work item"}
      { Id = Guid.NewGuid(); Text = "Second work item"} ] }

createSimpleApp initial render update (Virtualdom.createRender)
|> withStartNodeSelector "#todo"
|> start
