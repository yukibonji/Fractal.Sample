#r "packages/FAKE/tools/FakeLib.dll"
#r "packages/Suave/lib/net40/Suave.dll"
#r "packages/FSharp.Compiler.Service/lib/net45/FSharp.Compiler.Service.dll"

#r "packages/FunScript/lib/net40/FunScript.dll"
#r "packages/FunScript/lib/net40/FunScript.Interop.dll"
#r "packages/FunScript.TypeScript.Binding.lib/lib/net40/FunScript.TypeScript.Binding.lib.dll"
#r "packages/FunScript.TypeScript.Binding.jquery/lib/net40/FunScript.TypeScript.Binding.jquery.dll"
#r "lib/Fractal.dll"

#load "client/App.fsx"


open Fake
open Fake.AssemblyInfoFile
open System
open System.IO
open FunScript
open Suave
open Suave.Web
open Suave.Http
open Suave.Http.Applicatives
open Suave.Http.Files
open Suave.Logging
open Microsoft.FSharp.Compiler.Interactive.Shell

let appPath = combinePaths __SOURCE_DIRECTORY__ "app"
let clientJs = combinePaths appPath "client.js"

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
    fsiSession.EvalInteraction(sprintf "#load @\"%s\"" "client/App.fsx")
    match fsiSession.EvalExpression("<@@ Fractal.Sample.App.app() @@>") with
    | Some app -> Some(app.ReflectionValue :?> Quotations.Expr)
    | None -> failwith "Couldn't get 'app' value"
  with e -> reportFsiError e; None

let generate expr =
    let code = Compiler.compileWithoutReturn(expr)
    let code' = "var React = require('react');\n" +
                "window.postal = require('postal');\n" +
                "window.mui = require('material-ui');\n" +
                "var ThemeManager = new window.mui.Styles.ThemeManager();\n" +
                "var Router5 = require('router5').Router5;\n" +
                "var injectTapEventPlugin = require('react-tap-event-plugin');\n" +
                "injectTapEventPlugin();\n" +
                code
    File.WriteAllText(clientJs, code')

let npmInstall () =
    let npm = __SOURCE_DIRECTORY__ @@ "packages/Npm.js/tools/npm.cmd"
    let result = ExecProcess (fun info ->
            info.FileName <- npm
            info.WorkingDirectory <- appPath
            info.Arguments <- "install") System.TimeSpan.MaxValue
    if result <> 0 then failwithf "Error during running npm "

let browserify () =
    let browserify = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) @@ "npm" @@ "browserify.cmd"


    let result = ExecProcess (fun info ->
            info.FileName <- browserify
            info.WorkingDirectory <- appPath
            info.Arguments <- "client.js > app.js -r material-ui") System.TimeSpan.MaxValue
    if result <> 0 then failwithf "Error during running browserify "

Target "Default" DoNothing

Target "BuildClient" (fun _ -> reloadScript() |> Option.iter generate)

Target "NpmInstall" npmInstall

Target "Browserify" browserify

Target "RunServer" (fun _ ->
    let index = combinePaths appPath "index.html"
    let app = choose [ GET >>= choose [ path "/" >>= file index
                                        pathRegex "(.*)\.css" >>= browseHome
                                        pathRegex "(.*)\.js" >>= browseHome
                                      ] ]
    let config = { defaultConfig with homeFolder = Some appPath; logger = Loggers.ConsoleWindowLogger LogLevel.Debug }
    let _, server = startWebServerAsync config app
    Async.Start(server)

    use watcher = !! (__SOURCE_DIRECTORY__ @@ "client\*.fsx") |> WatchChanges (fun _ ->
        reloadScript() |> Option.iter generate
        npmInstall ()
        browserify ()
        printfn "Regenerate Completed"
        )



    System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite)
)


"BuildClient"
  ==> "NpmInstall"
  ==> "Browserify"
  ==> "Default"

RunTargetOrDefault "Default"
