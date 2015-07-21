namespace Fractal.Sample

open FunScript.TypeScript.Mui
open FunScript.TypeScript.React
open FunScript.TypeScript
open FunScript
open Fractal
open System.Collections.Generic
open System

[<ReflectedDefinition>]
module App =
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

        do Message.subscribe "todo.new" (fun n -> n :: todos |> setState )

        do Message.subscribe "todo.toggle" (fun n ->
            todos |> List.map (fun t -> if t.id = n then {t with completed = t.completed |> not} else t )
            |> setState
        )

        do Message.subscribe "todo.remove" (fun n ->
            todos |> List.filter (fun t -> t.id <> n) |> setState
        )

        do Message.subscribe "todo.save" (fun (n,m) ->
            todos |> List.map (fun t -> if t.id = n then {t with title = m} else t )
            |> setState
        )

        do Message.subscribe "todo.repository.undo" (fun n -> undo () )

        do Message.subscribe "todo.repository.redo" (fun n -> redo () )

        do Message.subscribe "todo.repository.toggleAll" (fun n ->
            todos |> List.map (fun t -> {t with completed = n}) |> setState
        )

        do Message.subscribe "todo.repository.clearSelected" (fun n ->
            todos |> List.filter (fun t -> t.completed |> not) |> setState
        )

        member x.getState () =
            todos

        member x.canUndo () =
            history.Length > 1

        member x.canRedo () =
            redoList.Length > 0

    type TodoItemState = {editingText : string}
    type TodoItemProps = {todo : Todo; editing : bool}
    type todoItem =  ReactComponent<TodoItemProps, TodoItemState>

    let TodoItem props =
        let onToggle (c : todoItem) () =
            Message.publish "todo.toggle" c.props.todo.id

        let onDestroy (c : todoItem) () =
            Message.publish "todo.remove" c.props.todo.id

        let handleSubmit (c : todoItem) (e : obj) =
            let v = c.state.editingText.Trim()
            if String.IsNullOrEmpty v |> not then
                c.setState {editingText = v}
                Message.publish "todo.view.editDone" ()
                Message.publish "tood.save" (c.props.todo.id, c.state.editingText)
            else
                Message.publish "todo.remove" c.props.todo.id

        let handleEdit (c : todoItem) (e : obj) =
            Message.publish "todo.view.edit" c.props.todo.id
            c.setState {editingText = c.props.todo.title}

        let handleKeyDown (c : todoItem) (e : KeyboardEvent) =
            if e.which = 27. then
                c.setState {editingText = c.props.todo.title}
                Message.publish "todo.view.editDone" ()
            elif e.which = 13. then
                handleSubmit c e

        let handleChange (c : todoItem) (e : Event) =
            c.setState {editingText = e.target.value }

        let getInitialState (c : todoItem) =
            {editingText = c.props.todo.title }

        let shouldComponentUpdate (c : todoItem) (nextProps,nextState) =
            nextProps.todo <> c.props.todo ||
            nextProps.editing <> c.props.editing ||
            nextState.editingText <> c.state.editingText

        let componentDidUpdate (c : todoItem) (prevProps) =
            if prevProps.editing |> not && c.props.editing then
                let node = Globals.findDOMNode(c.refs.["editField"]) |> unbox<JQuery>
                node.focus() |> ignore
        ()

        let render (c : todoItem) =
            let liAtr = if c.props.todo.completed then obj ["className" ==> "completed"]
                        elif c.props.editing then obj ["className" ==> "editing"]
                        else null

            Globals.createElement ("li", liAtr,
                Globals.createElement ("div", obj ["className" ==> "view"],
                    Globals.createElement("input", obj ["className" ==> "toggle"
                                                        "type" ==> "checkbox"
                                                        "checked" ==> c.props.todo.completed
                                                        "onChange" ==> onToggle c]),
                    Globals.createElement("label", obj ["onDoubleClick" ==> handleEdit c], c.props.todo.title),
                    Globals.createElement("button", obj ["className" ==> "destroy"
                                                         "onClick" ==> onDestroy c ])
                ),
                Globals.createElement("input", obj ["ref" ==> "editField"
                                                    "className" ==> "edit"
                                                    "value" ==> c.state.editingText
                                                    "onBlur" ==> handleSubmit c
                                                    "onChange" ==> handleChange c
                                                    "onKeyDown" ==> handleKeyDown c])
            )
        React.defineComponent render
        |> fun c ->
            c.``getInitialState <-``(fun _ -> JS.this |> getInitialState)
            c.``shouldComponentUpdate <-``(fun p s _ -> shouldComponentUpdate (JS.this) (p,s) )
            c.``componentDidUpdate <-``(fun p _ _ -> componentDidUpdate (JS.this) p)
            c
        |> React.createComponent
        |> fun n -> Globals.createElement(n, props, null)

    type FilterTodo =
        | All
        | Completed
        | Active

    type TodoFooterProps = {count : int; completeCount : int; canUndo : bool; canRedo : bool; nowShowing : FilterTodo}
    type todoFooter = ReactComponent<TodoFooterProps, Nothing>

    let TodoFooter props =
        let onClearCompleted () =
            Message.publish "todo.repository.clearSelected" ()

        let onUndo () =
            Message.publish "todo.repository.undo" ()

        let onRedo () =
            Message.publish "todo.repository.redo" ()

        let render (c : todoFooter) =
            let clearButton =
                Globals.createElement("button", obj ["className" ==> "clear-completed"
                                                     "onClick" ==> onClearCompleted
                                                     (if c.props.completeCount > 0 then
                                                         "" ==> null
                                                      else "disabled" ==> "disabled")
                ], "Clear completed")

            let undoButton =
                Globals.createElement("button", obj ["className" ==> "clear-completed"
                                                     "onClick" ==> onUndo
                                                     (if c.props.canUndo then
                                                          "" ==> null
                                                      else "disabled" ==> "disabled")
                ], "Undo")

            let redoButton  =
                Globals.createElement("button", obj ["className" ==> "clear-completed"
                                                     "onClick" ==> onRedo
                                                     (if c.props.canRedo then
                                                         "" ==> null
                                                      else "disabled" ==> "disabled")
                ], "Redo")

            Globals.createElement("footer", obj ["className" ==> "footer"],
                Globals.createElement("span", obj ["className" ==> "todo-count"],
                    Globals.createElement("strong", null, c.props.count),
                    " todo(s) left"
                ),
                Globals.createElement("ul", obj ["className" ==> "filters"],
                    Globals.createElement("li", null,
                        Globals.createElement("a", obj ["href" ==> "#/"
                                                        (if c.props.nowShowing = FilterTodo.All then
                                                            "className" ==> "selected"
                                                         else "className" ==> "")], "All"),
                        " ",
                        DOM.a (obj ["href" ==> "#/active"
                                    (if c.props.nowShowing = FilterTodo.Active then
                                        "className" ==> "selected"
                                     else "className" ==> "")], "Active"),
                        " ",
                        Globals.createElement("a", obj ["href" ==> "#/completed"
                                                        (if c.props.nowShowing = FilterTodo.Completed then
                                                            "className" ==> "selected"
                                                         else "className" ==> "")], "Completed")
                    )
                ),
                clearButton,
                undoButton,
                redoButton
            )
        React.defineComponent render
        |> React.createComponent
        |> fun n -> Globals.createElement(n, props, null)

    type TodoAppProps = {model : TodoRepository}
    type TodoAppState = {nowShowing : FilterTodo; editing : Guid option; todos: Todo array }
    type todoApp = ReactComponent<TodoAppProps, TodoAppState>

    let TodoApp props =
        let getInitialState () =
            {nowShowing = FilterTodo.All; editing = None; todos = [||]}

        let componentDidMount (c : todoApp) =
            do Message.subscribe "todo.view.editDone" (fun n ->
                c.setState({c.state with editing = None})
            )

            do Message.subscribe "todo.view.edit" (fun n ->
                c.setState({c.state with editing = Some n})
            )

            do Message.subscribe "todo.repository.changed" (fun n ->
                c.setState {c.state with todos = c.props.model.getState () |> List.toArray}
            )

            //TODO ROUTING
            ()

        let handleKeyDown (c : todoApp) (e : KeyboardEvent) =
            if e.which = 13. then
                e.preventDefault()
                let v = Globals.findDOMNode(c.refs.["newField"]).value.Trim()
                if String.IsNullOrEmpty v |> not then
                    {id = Guid.NewGuid(); title = v; completed = false }
                    |> Message.publish "todo.new"
                    Globals.findDOMNode(c.refs.["newField"]).value <- ""

        let toggleAll (c : todoApp) (e : Event) =
            e.target.check
            |> Message.publish "todo.repository.toggleAll"

        let render (c : todoApp) =
            let todos = c.state.todos
            let todoItems = todos
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
                Globals.createElement("section", obj ["className" ==> "main"],
                    Globals.createElement("input", obj ["className" ==> "toggle-all"
                                                        "type" ==> "checkbox"
                                                        "onChange" ==> toggleAll c
                                                        "checked" ==> (activeCount = 0)]),
                    Globals.createElement("ul", obj ["className" ==> "todo-list"], todoItems)
                )

            Globals.createElement("div", (null |> unbox<obj>),
                Globals.createElement("header", obj ["className" ==> "header"],
                    Globals.createElement("h1", null, "todos" ),
                    Globals.createElement("input", obj ["ref" ==> "newField"
                                                        "className" ==> "new-todo"
                                                        "placeholder" ==> "What needs to be done?"
                                                        "onKeyDown" ==> handleKeyDown c
                                                        "autoFocus" ==> true])
                ),
                (if todos.Length > 0 then main else null |> unbox<DOMElement<obj>>),
                footer)

        React.defineComponent render
        |> fun c ->
            c.``getInitialState <-``(fun _ -> JS.this |> getInitialState)
            c.``componentDidMount <-``(fun _ -> JS.this |> componentDidMount)
            c
        |> React.createComponent
        |> fun n -> Globals.createElement(n, props, null)

    let app () =
        { model = TodoRepository () }
        |> TodoApp
        |> React.render "todoapp"
        ()
