#load "App.fsx"

namespace Fractal.Sample

open System.IO
open FunScript

module Generator =
    let generate path =
        let code = Compiler.compileWithoutReturn(<@@ App.app() @@>)
        let code' = "var React = require('react');\n" +
                    "window.postal = require('postal');\n" +
                    code
        File.WriteAllText(path, code')
