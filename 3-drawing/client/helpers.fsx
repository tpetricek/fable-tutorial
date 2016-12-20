module Helpers
#nowarn "40"
#r "node_modules/fable-core/Fable.Core.dll"

open System
open Fable.Core
open Fable.Import.Browser

// -------------------------------------------------------------------------------------------------
// Additional JavaScript functions and bindings
// -------------------------------------------------------------------------------------------------

[<Emit("JSON.stringify($0)")>]
let jsonStringify json : string = failwith "JS Only"

[<Emit("JSON.parse($0)")>]
let jsonParse<'R> (str:string) : 'R = failwith "JS Only"

type ClientReplyChannel<'R> = 
  internal { mutable Trigger : 'R -> unit }
  member x.Reply(r) = 
    x.Trigger(r)
  
type MailboxProcessor<'T> with
  member x.PostAndClientReply(msg:ClientReplyChannel<'C> -> 'T) : Async<'C> = 
    Async.FromContinuations(fun (cont, _, _) ->
      let ch = { Trigger = cont }
      x.Post(msg ch))
  
module Async =
  /// Await the first occurrence of the specified event on the
  /// given element and return the event object casted to 'T
  let AwaitDomEvent<'T>(el:HTMLElement, event) =
    Async.FromContinuations(fun (cont, _, _) ->
      let rec listener = EventListener(fun e ->
        el.removeEventListener(event, U2.Case1 listener)
        cont(unbox<'T> e) )
      el.addEventListener(event, U2.Case1 listener) )

module Http =
  /// Send HTTP request asynchronously
  /// (does not handle errors properly)
  let Request(meth, url, data) =
    Async.FromContinuations(fun (cont, _, _) ->
      let xhr = XMLHttpRequest.Create()
      xhr.``open``(meth, url)
      xhr.onreadystatechange <- fun _ ->
        if xhr.readyState > 3. && xhr.status = 200. then
          cont(xhr.responseText)
        obj()
      xhr.send(defaultArg data "") )
