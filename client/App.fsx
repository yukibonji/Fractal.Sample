#load "Model.fsx"

namespace Fractal.Sample


open Fractal
open System.Collections.Generic
open System
open Model

[<ReflectedDefinition>]
module App =
    type TodoItemState = {editingText : string}
    type TodoItemProps = {todo : Todo; editing : bool}
    type todoItem = FractalComponent<TodoItemProps, TodoItemState>

    let mutable router : Router option = None


    module TodoItemHandlers =
        let onToggle (c : todoItem) (e : FormEvent) =
            Message.publish "todo.toggle" c. props.todo.id

        let onDestroy (c : todoItem) (e : MouseEvent) =
            Message.publish "todo.remove" c.props.todo.id

        let handleSubmit (c : todoItem) (e : FocusEvent) =
            let v = c.state.editingText.Trim()
            if String.IsNullOrEmpty v |> not then
                c.setState {editingText = v}
                Message.publish "todo.view.editDone" ()
                Message.publish "todo.save" (c.props.todo.id, c.state.editingText)
            else
                Message.publish "todo.remove" c.props.todo.id

        let handleEdit (c : todoItem) (e : MouseEvent) =
            Message.publish "todo.view.edit" c.props.todo.id
            c.setState {editingText = c.props.todo.title}

        let handleKeyDown (c : todoItem) (e : KeyboardEvent) =
            if e.which = 27. then
                c.setState {editingText = c.props.todo.title}
                Message.publish "todo.view.editDone" ()
            elif e.which = 13. then
                handleSubmit c (e |> unbox<FocusEvent>)

        let handleChange (c : todoItem) (e : FormEvent) =
            c.setState {editingText = e.target.value }

    let TodoItem =
        Fractal.createComponent [|
            GetInitialState (fun (c : todoItem) -> {editingText = c.props.todo.title })

            ShouldComponentUpdate ( fun nextProps nextState (c : todoItem) ->
                nextProps.todo <> c.props.todo ||
                nextProps.editing <> c.props.editing ||
                nextState.editingText <> c.state.editingText)

            ComponentDidUpdate (fun prevProps prevState (c : todoItem) ->
                if prevProps.editing |> not && c.props.editing then
                    let node = Fractal.findDOMNode(c.refs.["editField"])
                    node?focus() |> ignore)

            Render ( fun (c : todoItem) ->
                DOM.li ((if c.props.todo.completed then [|ClassName "completed"|]
                         elif c.props.editing then [|ClassName "editing"|]
                         else [| |]),
                    DOM.div ([|ClassName "view"|],
                        DOM.input( [| ClassName "toggle"; Attr.Type "checkbox"; Checked c.props.todo.completed; OnChange (TodoItemHandlers.onToggle c)|]),
                        DOM.label([| OnDoubleClick (TodoItemHandlers.handleEdit c) |], c.props.todo.title),
                        DOM.button([| ClassName "destroy"; OnClick (TodoItemHandlers.onDestroy c) |])),
                    DOM.input([| Ref "editField"; ClassName "edit"; Value c.state.editingText;
                                 OnBlur (TodoItemHandlers.handleSubmit c); OnChange ( TodoItemHandlers.handleChange c); OnKeyDown (TodoItemHandlers.handleKeyDown c) |])
                ))
         |]

    type FilterTodo =
        | All
        | Completed
        | Active

    type TodoFooterProps = {
        count : int; completeCount : int; canUndo : bool;
        canRedo : bool; nowShowing : FilterTodo}
    type todoFooter = FractalComponent<TodoFooterProps, Nothing>

    module TodoFooterHandlers =
        let onClearCompleted (e : MouseEvent) =
            Message.publish "todo.repository.clearSelected" ()

        let onUndo (e : MouseEvent) =
            Message.publish "todo.repository.undo" ()

        let onRedo (e : MouseEvent) =
            Message.publish "todo.repository.redo" ()

    let TodoFooter =
        Fractal.createComponent [|
            Render (fun (c : todoFooter) ->
                let clearButton =
                    DOM.button([| ClassName "clear-completed"; OnClick (TodoFooterHandlers.onClearCompleted); Disabled (c.props.completeCount = 0 ) |], "Clear completed")

                let undoButton =
                    DOM.button([| ClassName "clear-completed"; OnClick (TodoFooterHandlers.onUndo); Disabled (c.props.canUndo |> not) |], "Undo")

                let redoButton =
                    DOM.button([| ClassName "clear-completed"; OnClick (TodoFooterHandlers.onRedo); Disabled (c.props.canRedo |> not) |], "Redo")

                DOM.footer( [| ClassName "footer" |],
                    DOM.span( [| ClassName "todo-count" |],
                        DOM.strong([| |], c.props.count),
                        " todo(s) left"
                    ),
                    DOM.ul( [| ClassName "filters" |],
                        DOM.li( [||],
                            DOM.button( [| OnClick (fun _ -> router |> Option.iter (fun r -> r.navigate "all" |> ignore )); ClassName (if c.props.nowShowing = FilterTodo.All then "selected" else "" ) |]  , "All"),
                            " ",
                            DOM.button( [| OnClick (fun _ -> router |> Option.iter (fun r -> r.navigate "active" |> ignore )); ClassName (if c.props.nowShowing = FilterTodo.Active then "selected" else "" ) |]  , "Active"),
                            " ",
                            DOM.button( [| OnClick (fun _ -> router |> Option.iter (fun r -> r.navigate "completed" |> ignore )); ClassName (if c.props.nowShowing = FilterTodo.Completed then "selected" else "" ) |]  , "Completed")
                        )
                    ),
                    clearButton,
                    undoButton,
                    redoButton)
                )|]

    type TodoAppProps = {model : TodoRepository}
    type TodoAppState = {nowShowing : FilterTodo; editing : Guid option; todos: Todo array }
    type todoApp = FractalComponent<TodoAppProps, TodoAppState>

    module TodoAppHandlers =
        let handleKeyDown (c : todoApp) (e : KeyboardEvent) =
            if e. which = 13. then
                e.preventDefault()
                let v = Fractal.findDOMNode(c.refs.["newField"]).value.Trim()
                if String.IsNullOrEmpty v |> not then
                    {id = Guid.NewGuid(); title = v; completed = false }
                    |> Message.publish "todo.new"
                    Fractal.findDOMNode(c.refs.["newField"]).value <- ""

        let toggleAll (c : todoApp) (e : FormEvent) =
            e.target.check
            |> Message.publish "todo.repository.toggleAll"

    let TodoApp =
        Fractal.createComponent [|
            GetInitialState (fun (c : todoApp) -> {nowShowing = FilterTodo.All; editing = None; todos = [||]})

            ComponentDidMount (fun (c : todoApp) ->
                Message.subscribe "todo.view.editDone" (fun n ->
                    c.setState({c.state with editing = None})
                ) |> ignore
                Message.subscribe "todo.view.edit" (fun n ->
                    c.setState({c.state with editing = Some n})
                ) |> ignore

                Message.subscribe "todo.repository.changed" (fun n ->
                    c.setState {c.state with todos = c.props.model.getState () |> List.toArray}
                ) |> ignore

                router <- Router.Create([| {name = "all"; path = "/"; children = [||]}
                                           {name = "active"; path = "/active"; children = [||]}
                                           {name = "completed"; path = "/completed"; children = [||]} |])
                                .addRouteListener("all", fun _ -> c.setState {c.state with nowShowing = FilterTodo.All})
                                .addRouteListener("active", fun _ -> c.setState {c.state with nowShowing = FilterTodo.Active})
                                .addRouteListener("completed", fun _ -> c.setState {c.state with nowShowing = FilterTodo.Completed})
                                .start()
                                |> Some
            )

            Render (fun (c : todoApp) ->
                let todos = c.state.todos
                let todoItems = todos |> Array.filter (fun t ->
                                            match c.state.nowShowing with
                                            | All -> true
                                            | Active -> t.completed |> not
                                            | Completed -> t.completed)
                                      |> Array.map(fun t ->
                                            let ed = if c.state.editing.IsSome then c.state.editing.Value = t.id else false
                                            TodoItem {todo = t; editing = ed }  )
                let activeCount = todos |> Array.fold (fun acc t ->
                    if t.completed then acc else acc + 1 ) 0
                let completedCount = todos.Length - activeCount

                let footer =
                    let props = {
                        count = activeCount
                        completeCount = completedCount
                        canUndo = c.props.model.canUndo ()
                        canRedo = c.props.model.canRedo ()
                        nowShowing = c.state.nowShowing }
                    TodoFooter props

                let main =
                    DOM.section( [| ClassName "main" |],
                        DOM.input( [| ClassName "toggle-all"; Attr.Type "checkbox"; OnChange (TodoAppHandlers.toggleAll c); Checked (activeCount = 0) |]),
                        DOM.ul( [| ClassName "todo-list" |], todoItems)
                    )

                DOM.div( [||],
                    DOM.header( [|ClassName "header" |],
                        DOM.h1( [||], "todos" ),
                        DOM.input( [| Ref "newField"; ClassName "new-todo"; Placeholder "What needs to be done?"; OnKeyDown (TodoAppHandlers.handleKeyDown c); AutoFocus true |])
                    ),
                    (if todos.Length > 0 then main else null |> unbox<FractalElement<obj>>),
                    footer))
        |]

    let app () =
        { model = TodoRepository () }
        |> TodoApp
        |> Fractal.render "todoapp"
        ()
