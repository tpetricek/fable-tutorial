// --------------------------------------------------------------------------------------
// A simple FAKE build script that:
//  1) Hosts Suave server locally & reloads web part that is defined in 'app.fsx'
//  2) Deploys the web application to Azure web sites when called with 'build deploy'
// --------------------------------------------------------------------------------------

#r "packages/FSharp.Compiler.Service/lib/net45/FSharp.Compiler.Service.dll"
#r "packages/Suave/lib/net40/Suave.dll"
#r "packages/FAKE/tools/FakeLib.dll"
open Fake

open System
open System.IO
open Suave
open Suave.Web
open Microsoft.FSharp.Compiler.Interactive.Shell

// --------------------------------------------------------------------------------------
// The following uses FileSystemWatcher to look for changes in 'app.fsx'. When
// the file changes, we run `#load "app.fsx"` using the F# Interactive service
// and then get the `App.app` value (top-level value defined using `let app = ...`).
// The loaded WebPart is then hosted at localhost:8083.
// --------------------------------------------------------------------------------------

let sbOut = new Text.StringBuilder()
let sbErr = new Text.StringBuilder()

let fsiSession =
  let inStream = new StringReader("")
  let outStream = new StringWriter(sbOut)
  let errStream = new StringWriter(sbErr)
  let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration()
  let argv = Array.append [|"/fake/fsi.exe"; "--quiet"; "--noninteractive"; "-d:DO_NOT_START_SERVER"|] [||]
  FsiEvaluationSession.Create(fsiConfig, argv, inStream, outStream, errStream)

let reportFsiError (e:exn) =
  traceError "Reloading app.fsx script failed."
  traceError (sprintf "Message: %s\nError: %s" e.Message (sbErr.ToString().Trim()))
  sbErr.Clear() |> ignore

let reloadScript () =
  try
    traceImportant "Reloading app.fsx script..."
    let appFsx = __SOURCE_DIRECTORY__ @@ "app.fsx"
    fsiSession.EvalInteraction(sprintf "#load @\"%s\"" appFsx)
    fsiSession.EvalInteraction("open App")
    match fsiSession.EvalExpression("app") with
    | Some app -> Some(app.ReflectionValue :?> WebPart)
    | None -> failwith "Couldn't get 'app' value"
  with e -> reportFsiError e; None

// --------------------------------------------------------------------------------------
// Suave server that redirects all request to currently loaded version
// --------------------------------------------------------------------------------------

let currentApp = ref (fun _ -> async { return None })

let getLocalServerConfig port =
  { defaultConfig with
      homeFolder = Some __SOURCE_DIRECTORY__
      logger = Logging.Loggers.saneDefaultsFor Logging.LogLevel.Debug
      bindings = [ HttpBinding.mkSimple HTTP  "127.0.0.1" port ] }

let reloadAppServer (changedFiles: string seq) =
  traceImportant <| sprintf "Changes in %s" (String.Join(",",changedFiles))
  reloadScript() |> Option.iter (fun app ->
    currentApp.Value <- app
    traceImportant "Refreshed app." )

Target "run" (fun _ ->
  let app ctx = currentApp.Value ctx
  let port = 8083
  let _, server = startWebServerAsync (getLocalServerConfig port) app

  // Start Suave to host it on localhost
  reloadAppServer ["app.fsx"]
  Async.Start(server)
  // Open web browser with the loaded file
  System.Diagnostics.Process.Start(sprintf "http://localhost:%d" port) |> ignore

  // Watch for changes & reload when app.fsx changes
  let sources =
    { BaseDirectory = __SOURCE_DIRECTORY__
      Includes = [ "*.fsx" ];
      Excludes = [] }

  use watcher = sources |> WatchChanges (Seq.map (fun x -> x.FullPath) >> reloadAppServer)
  traceImportant "Waiting for app.fsx edits. Press any key to stop."

  System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite)
)

// --------------------------------------------------------------------------------------
// NPM helpers
// --------------------------------------------------------------------------------------

let npm command args workingDir =
  let args = sprintf "%s %s" command (String.concat " " args)
  let cmd, args = if EnvironmentHelper.isUnix then "npm", args else "cmd", ("/C npm " + args)
  let ok =
    execProcess (fun info ->
      info.FileName <- cmd
      info.WorkingDirectory <- workingDir
      info.Arguments <- args) TimeSpan.MaxValue
  if not ok then failwith (sprintf "'%s %s' task failed" cmd args)

let node command args workingDir =
  let args = sprintf "%s %s" command (String.concat " " args)
  let cmd, args = if EnvironmentHelper.isUnix then "node", args else "cmd", ("/C node " + args)
  async { 
    execProcess (fun info ->
      info.FileName <- cmd
      info.WorkingDirectory <- workingDir
      info.Arguments <- args) TimeSpan.MaxValue |> ignore } |> Async.Start

Target "fable" (fun _ ->
  __SOURCE_DIRECTORY__ </> "client" |> npm "install" []
  __SOURCE_DIRECTORY__ </> "client" |> node "node_modules/fable-compiler" ["-w"]
)

"fable" ==> "run"

RunTargetOrDefault "run"
