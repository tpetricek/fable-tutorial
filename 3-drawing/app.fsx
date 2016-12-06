#I "packages/Suave/lib/net40"
#r "packages/Suave/lib/net40/Suave.dll"
#r "packages/FSharp.Data/lib/net40/FSharp.Data.dll"
open System
open System.IO
open Suave
open Suave.Filters
open Suave.Operators
open FSharp.Data

// -------------------------------------------------------------------------------------------------
// Agent for keeping the state of the drawing
// -------------------------------------------------------------------------------------------------

// Using JSON provider to get a type for rectangles with easy serialization
type Shape = JsonProvider<"""{"x1":0.0,"y1":0.0,"x2":10.0,"y2":10.0}""">

// BONUS: If you want to implement the reset functionality, you can use
// the following type provider for the communication instead of `Shape`.
(*
type ShapeCommand = JsonProvider<"""
  [ { "cmd":"reset" },
    { "cmd":"shape", 
      "shape":{"x1":0.0,"y1":0.0,"x2":10.0,"y2":10.0} } ]""", SampleIsList=true>
*)


// We can add new shapes or request a list of all shapes
type Message =
  | AddShape of Shape.Root
  | GetShapes of AsyncReplyChannel<list<Shape.Root>>

// Agent that keeps the state and handles 'Message' requests
let agent = MailboxProcessor.Start(fun inbox ->
  let rec loop shapes = async {
    let! msg = inbox.Receive()
    match msg with
    | AddShape(s) -> return! loop (s::shapes)
    | GetShapes(repl) ->
        repl.Reply(shapes)
        return! loop shapes }
  loop [] )

// -------------------------------------------------------------------------------------------------
// The web server - REST api and static file hosting
// -------------------------------------------------------------------------------------------------

let webRoot = Path.Combine(__SOURCE_DIRECTORY__, "web")
let clientRoot = Path.Combine(__SOURCE_DIRECTORY__, "client")

let noCache =
  Writers.setHeader "Cache-Control" "no-cache, no-store, must-revalidate"
  >=> Writers.setHeader "Pragma" "no-cache"
  >=> Writers.setHeader "Expires" "0"

let getRectangles ctx = async {
  let! shapes = agent.PostAndAsyncReply(GetShapes)
  let json = JsonValue.Array [| for r in shapes -> r.JsonValue |]
  return! ctx |> Successful.OK(json.ToString()) }

let addRectangle ctx = async {
  use ms = new StreamReader(new MemoryStream(ctx.request.rawForm))
  agent.Post(AddShape(Shape.Parse(ms.ReadToEnd())))
  return! ctx |> Successful.OK "added" }

let app =
  choose [
    // REST API for adding/getting rectangles
    GET >=> path "/getrects" >=> getRectangles
    POST >=> path "/addrect" >=> addRectangle

    // Serving the generated JS and source maps
    path "/out/bundle.js" >=> noCache >=> Files.browseFile clientRoot (Path.Combine("out", "bundle.js"))
    path "/out/bundle.js.map" >=> noCache >=> Files.browseFile clientRoot (Path.Combine("out", "bundle.js.map"))

    // Serving index and other static files
    path "/" >=> Files.browseFile webRoot "index.html"
    Files.browse webRoot
  ]











//
