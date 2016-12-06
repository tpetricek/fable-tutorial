#nowarn "40"
#r "node_modules/fable-core/Fable.Core.dll"
#load "helpers.fsx"
open System
open Helpers
open Fable.Core
open Fable.Import.Browser

module FsOption = Microsoft.FSharp.Core.Option

// -------------------------------------------------------------------------------------------------
// Managing state with async & agents
// -------------------------------------------------------------------------------------------------

// TODO #1: Implement synchronization using the server. The HTTP communication
// is already partly done (in `addRectangle`, we send the new rectangle to the
// server and in `refreshing` we read the list), but you need to integrate this
// with the state manager - add a message `ReplaceShapes` that carries `Shape list`
// and replaces all shapes with new shapes. Trigger the message when you receive
// new list of shapes from the server in `refreshing`.

// TODO #2: The canvas gets messy with a lot of shapes. Modify the server-side
// (app.fsx) to keep at most 20 recent shapes and drop the earlier ones. To do
// this, you need to truncate the list in the agent on the server (`List.truncate`
// is a nice function for that :-))

// TODO #3: Random colors! Change the `Shape` type to also store color of the
// rectangle. You can store it as RGB tuple or as a string in the 
// "rgb(r,g,b,a)" format. Next, we want to have current color for drawing and
// the "Random color" button should change the current color. To do this, 
// add current color as a parameter to the `loop` function in the (client-side) 
// agent below and add a message to generate random color. Send the message
// to the agent when the button is clicked. For generating random colors, you
// can use the .NET `Random()` type.

// BONUS: The "Reset" button should reset the state on the server. To do this,
// we need to modify the JSON communication protocol. You can send 
// `{"cmd":"reset"}` to reset and `{"cmd":"shape","shape":{...}}` to add
// a new shape (see the `ShapeCommand` type in `app.fsx` for example). Then
// you need to modify sending messages here & receiving messages on the 
// server-side.

// BONUS: If you have even more time, then you can implement the button
// to switch between drawing of circles & drawing of rectangles! :-)


// -------------------------------------------------------------------------------------------------
// Domain model - simple type for shapes & events modeling operations on state 
// -------------------------------------------------------------------------------------------------

type Shape =
  { x1:float; y1:float; x2:float; y2:float; }

// BONUS: If you're implementing the reset button, use the 
// following type for communication with the server-side:
//
//   type ShapeCommand = 
//     { cmd:string; shape:Shape option }
  
type DrawingMessage = 
  // Set the shape currently being drawn
  | SetSelection of Shape
  // Add the currently being drawn shape to the list
  | AddShape  
  // Get the optional current shape & list of drawn shapes
  | GetState of ClientReplyChannel<Shape option * Shape list>


// -------------------------------------------------------------------------------------------------
// State management - we keep the state isolated inside an agent
// The agent receives messages that specify how to update the state
// -------------------------------------------------------------------------------------------------
  
let agent = MailboxProcessor.Start(fun inbox ->
  let rec loop sel shapes = async {
    let! msg = inbox.Receive()
    match msg with
    | SetSelection(sel) -> 
        return! loop (Some sel) shapes
    | GetState ch ->
        ch.Reply(sel, shapes)
        return! loop sel shapes
    | AddShape -> 
        match sel with 
        | None -> return! loop None shapes
        | Some sel -> return! loop None (sel::shapes) }
  loop None [])
  
// -------------------------------------------------------------------------------------------------
// The drawing - this is where we implement drawing on canvas
// -------------------------------------------------------------------------------------------------
  
// Initialize the canvas & cancel selection (when drawing starts)
let canvas =  document.getElementsByTagName_canvas().[0]
canvas.onselectstart <- fun _ -> box false
let ctx = canvas.getContext_2d()

/// Add drawn rectangle and report it to the server too
let addRectangle rect =
  Http.Request("POST", "/addrect", Some(jsonStringify rect))
  |> Async.Ignore |> Async.StartImmediate
  agent.Post(AddShape)

/// Fill rectangle with the given colour
let fillRectangle r color =
  ctx.fillStyle <- U3.Case1 color
  ctx.fillRect (r.x1, r.y1, r.x2 - r.x1, r.y2 - r.y1)

/// Draw the current stated - erase, draw all rectangles & selection
/// This is asynchronous too & it first gets the state from the agent
let drawRectangles () = async {
  let! selection, shapes = agent.PostAndClientReply(GetState)
  ctx.fillStyle <- U3.Case1 "rgb(0,0,0)"
  ctx.fillRect (0., 0., 1000., 1000.)
  shapes |> Seq.iter (fun rect ->
    fillRectangle rect "rgba(255,230,120,0.5)")
  selection |> FsOption.iter (fun rect ->
    fillRectangle rect "rgba(128,128,128,0.2)") }


// -------------------------------------------------------------------------------------------------
// User interaction - drawing rectangles & polling for refresh
// -------------------------------------------------------------------------------------------------

/// We are waiting for mouse down to start drawing
let rec waiting () = async {
  do! drawRectangles ()
  let! e = Async.AwaitDomEvent<MouseEvent>(canvas, "mousedown")
  let startPos = e.x - canvas.offsetLeft, e.y - canvas.offsetTop
  return! drawing startPos }

/// We are waiting for mouse move/up to continue or finish drawing
and drawing (x1, y1) = async {
  let! e = Async.AwaitDomEvent<MouseEvent>(canvas, "mousemove")
  let x2, y2 = e.x - canvas.offsetLeft, e.y - canvas.offsetTop
  let rect = { x1=x1; y1=y1; x2=x2; y2=y2 }
  if e.buttons > 0. then
    agent.Post(SetSelection rect)
    do! drawRectangles ()
    return! drawing (x1, y1)
  else
    addRectangle rect
    return! waiting() }

/// Infinite loop that refreshes rectangles from the server repeatedly
let refreshing () = async {
  while true do
    let! res = Http.Request("GET", "/getrects", None) 
    let shapes = jsonParse<Shape[]>(res) |> List.ofArray
    // TODO: Replace the shapes in the agent 
    // with the newly loaded ones (see TASK #1)
    do! drawRectangles ()
    do! Async.Sleep(250) }

refreshing () |> Async.StartImmediate
waiting () |> Async.StartImmediate

// -------------------------------------------------------------------------------------------------
// Handlers for buttons that implement other functionality
// -------------------------------------------------------------------------------------------------

let resetBtn = document.getElementById("reset") :?> HTMLButtonElement
let rndBtn = document.getElementById("rnd") :?> HTMLButtonElement
let circBtn = document.getElementById("circ") :?> HTMLButtonElement
let rectBtn = document.getElementById("rect") :?> HTMLButtonElement

resetBtn.onclick <- fun _ -> window.alert("Reset!"); null
rndBtn.onclick <- fun _ -> window.alert("Random color!"); null
circBtn.onclick <- fun _ -> window.alert("Draw circles!"); null
rectBtn.onclick <- fun _ -> window.alert("Draw rectangles!"); null
