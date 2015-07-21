namespace Fractal.Sample

open System.IO
open FunScript

module Generator =
    [<EntryPoint>]
    let main argv =
        let code = Compiler.compileWithoutReturn(<@@ App.app() @@>)
        let code' = "var React = require('react');\n" +
                    "window.postal = require('postal');\n" +
                    code
        File.WriteAllText(argv.[0], code')
        printfn "%A" argv
        0 // return an integer exit code
