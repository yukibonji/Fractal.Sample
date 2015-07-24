#load "Fractal.fsx"

namespace Fractal.Sample

open FunScript.TypeScript.Mui
open FunScript.TypeScript.React
open FunScript.TypeScript
open FunScript
open Fractal
open System.Collections.Generic
open System

[<ReflectedDefinition>]
module Model =
    type Todo = {id : Guid; title : string; completed: bool}


    type TodoRepository () =
        let mutable todos : Todo list = List.empty
        let mutable history : Todo list list = List.empty
        let mutable redoList : Todo list list = List.empty

        let setState lst =
            todos <- lst
            history <- lst :: history
            redoList <- List.empty
            Message.publish "todo.repository.changed" ()

        let undo () =
            if history.Length <= 1 then ()
            else
                let h = history.Head
                history <- history.Tail
                redoList <- h::redoList
                todos <- h
                Message.publish "todo.repository.changed" ()

        let redo () =
            if redoList.Length = 0 then ()
            else
                let h = redoList.Head
                redoList <- redoList.Tail
                history <- h::history
                todos <- h
                Message.publish "todo.repository.changed" ()

        do Message.subscribe "todo.new" (fun n -> n :: todos |> setState ) |> ignore

        do Message.subscribe "todo.toggle" (fun n ->
            todos |> List.map (fun t -> if t.id = n then {t with completed = t.completed |> not} else t )
            |> setState ) |> ignore

        do Message.subscribe "todo.remove" (fun n -> todos |> List.filter (fun t -> t.id <> n) |> setState) |> ignore

        do Message.subscribe "todo.save" (fun (n,m) ->
            todos |> List.map (fun t -> if t.id = n then {t with title = m} else t )
            |> setState ) |> ignore

        do Message.subscribe "todo.repository.undo" (fun n -> undo () ) |> ignore

        do Message.subscribe "todo.repository.redo" (fun n -> redo () ) |> ignore

        do Message.subscribe "todo.repository.toggleAll" (fun n ->
            todos |> List.map (fun t -> {t with completed = n}) |> setState) |> ignore

        do Message.subscribe "todo.repository.clearSelected" (fun n ->
            todos |> List.filter (fun t -> t.completed |> not) |> setState) |> ignore

        member x.getState () =
            todos

        member x.canUndo () =
            history.Length > 1

        member x.canRedo () =
            redoList.Length > 0
