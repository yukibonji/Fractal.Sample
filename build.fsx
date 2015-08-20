#r "packages/FAKE/tools/FakeLib.dll"
#r "packages/FunScript/lib/net40/FunScript.dll"
#r "packages/FunScript/lib/net40/FunScript.Interop.dll"
#r "packages/FunScript.TypeScript.Binding.lib/lib/net40/FunScript.TypeScript.Binding.lib.dll"
#r "packages/FunScript.TypeScript.Binding.jquery/lib/net40/FunScript.TypeScript.Binding.jquery.dll"
#r "lib/Fractal.dll"
#r "packages/Suave/lib/net40/Suave.dll"

#load "client/App.fsx"
#load "client/Main.fsx"


open Fake
open Fake.AssemblyInfoFile
open System
open System.IO

let appPath = combinePaths __SOURCE_DIRECTORY__ "app"

Target "Default" DoNothing

Target "BuildClient" (fun _ -> Fractal.Sample.Generator.generate "app/client.js")

Target "NpmInstall" (fun _ ->
    let result = ExecProcess (fun info ->
            info.FileName <- "./npm.cmd"
            info.Arguments <- "install") System.TimeSpan.MaxValue
    if result <> 0 then failwithf "Error during running npm "
) //TODO: FIX IT

Target "Browserify" (fun _ ->
    let browserify = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) @@ "npm" @@ "browserify.cmd"


    let result = ExecProcess (fun info ->
            info.FileName <- browserify
            info.WorkingDirectory <- "app"
            info.Arguments <- "client.js > app.js -r material-ui") System.TimeSpan.MaxValue
    if result <> 0 then failwithf "Error during running browserify "
)

open Suave
open Suave.Web
open Suave.Http
open Suave.Http.Applicatives
open Suave.Http.Files
open Suave.Logging

Target "RunServer" (fun _ ->
    let index = combinePaths appPath "index.html"
    let app = choose [ GET >>= choose [ path "/" >>= file index
                                        pathRegex "(.*)\.css" >>= browseHome
                                        pathRegex "(.*)\.js" >>= browseHome
                                      ] ]
    let config = { defaultConfig with homeFolder = Some appPath; logger = Loggers.ConsoleWindowLogger LogLevel.Verbose }

    startWebServer config app
)


"BuildClient"
//==> "NpmInstall"
  ==> "Browserify"
  ==> "Default"

RunTargetOrDefault "Default"
