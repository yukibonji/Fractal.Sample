#r "packages/FAKE/tools/FakeLib.dll"

open Fake
open Fake.AssemblyInfoFile
open System
open System.IO

let buildDir = "./build"
let applicationOutput = "app"

let clientGeneratorProj = "client" @@ "client.fsproj"
let clientGeneratorExe = buildDir @@ "client.exe"
let clientGeneratorOutput = applicationOutput @@ "client.js"

let browserify = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) @@ "npm" @@ "browserify.cmd"

Target "Default" DoNothing

Target "BuildClient" (fun _ ->
    !! clientGeneratorProj
    |> MSBuildRelease buildDir "Build"
    |> Log "Build-Output: "

    let result = ExecProcess (fun info ->
            info.FileName <- clientGeneratorExe
            info.Arguments <- clientGeneratorOutput) System.TimeSpan.MaxValue
    if result <> 0 then failwithf "Error during running ClientGenerator"
)


Target "Browserify" (fun _ ->
    let result = ExecProcess (fun info ->
            info.FileName <- browserify
            info.WorkingDirectory <- "app"
            info.Arguments <- "client.js > app.js -r material-ui") System.TimeSpan.MaxValue
    if result <> 0 then failwithf "Error during running browserify "
)


"BuildClient"
  ==> "Browserify"
  ==> "Default"

RunTargetOrDefault "Default"
