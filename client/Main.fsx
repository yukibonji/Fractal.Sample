#load "App.fsx"

namespace Fractal.Sample

open System.IO
open FunScript

module Generator =
    let generate path =
        let code = Compiler.compileWithoutReturn(<@@ App.app() @@>)
        let code' = "var React = require('react');\n" +
                    "window.postal = require('postal');\n" +
                    "window.mui = require('material-ui');\n" +
                    "var ThemeManager = new window.mui.Styles.ThemeManager();\n" +
                    "var Router5 = require('router5').Router5;\n" +
                    code
        File.WriteAllText(path, code')
