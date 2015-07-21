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
    type todoItem =  FractalComponent<TodoItemProps, TodoItemState>

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

        let shouldComponentUpdate nextProps nextState (c : todoItem)=
            nextProps.todo <> c.props.todo ||
            nextProps.editing <> c.props.editing ||
            nextState.editingText <> c.state.editingText

        let componentDidUpdate prevProps (prevState : obj) (c : todoItem)  =
            if prevProps.editing |> not && c.props.editing then
                let node = Globals.findDOMNode(c.refs.["editField"]) |> unbox<JQuery>
                node.focus() |> ignore

        let render (c : todoItem) =
            DOM.li ((if c.props.todo.completed then obj ["className" ==> "completed"]
                     elif c.props.editing then obj ["className" ==> "editing"]
                     else null),
                DOM.div (obj ["className" ==> "view"],
                    DOM.input( obj ["className" ==> "toggle"
                                    "type" ==> "checkbox"
                                    "checked" ==> c.props.todo.completed
                                    "onChange" ==> onToggle c]),
                    DOM.label(obj ["onDoubleClick" ==> handleEdit c], c.props.todo.title),
                    DOM.button(obj ["className" ==> "destroy"
                                    "onClick" ==> onDestroy c ])
                ),
                DOM.input(obj [ "ref" ==> "editField"
                                "className" ==> "edit"
                                "value" ==> c.state.editingText
                                "onBlur" ==> handleSubmit c
                                "onChange" ==> handleChange c
                                "onKeyDown" ==> handleKeyDown c])
            )
        Fractal.defineComponent render
        |> Fractal.getInitialState getInitialState
        |> Fractal.shouldComponentUpdate shouldComponentUpdate
        |> Fractal.componentDidUpdate componentDidUpdate
        |> Fractal.createComponent
        |> Fractal.createElement props

    type FilterTodo =
        | All
        | Completed
        | Active

    type TodoFooterProps = {count : int; completeCount : int; canUndo : bool; canRedo : bool; nowShowing : FilterTodo}
    type todoFooter = FractalComponent<TodoFooterProps, Nothing>

    let TodoFooter props =
        let onClearCompleted () =
            Message.publish "todo.repository.clearSelected" ()

        let onUndo () =
            Message.publish "todo.repository.undo" ()

        let onRedo () =
            Message.publish "todo.repository.redo" ()

        let render (c : todoFooter) =
            let clearButton =
                DOM.button( obj ["className" ==> "clear-completed"
                                 "onClick" ==> onClearCompleted
                                 (if c.props.completeCount > 0 then
                                     "" ==> null
                                  else "disabled" ==> "disabled")], "Clear completed")

            let undoButton =
                DOM.button( obj ["className" ==> "clear-completed"
                                 "onClick" ==> onUndo
                                 (if c.props.canUndo then
                                      "" ==> null
                                  else "disabled" ==> "disabled")], "Undo")

            let redoButton  =
                DOM.button( obj ["className" ==> "clear-completed"
                                 "onClick" ==> onRedo
                                 (if c.props.canRedo then
                                     "" ==> null
                                  else "disabled" ==> "disabled")], "Redo")

            DOM.footer( obj ["className" ==> "footer"],
                DOM.span( obj ["className" ==> "todo-count"],
                    DOM.strong(null, c.props.count),
                    " todo(s) left"
                ),
                DOM.ul( obj ["className" ==> "filters"],
                    DOM.li( null,
                        DOM.a( obj ["href" ==> "#/"
                                    (if c.props.nowShowing = FilterTodo.All then
                                        "className" ==> "selected"
                                     else "className" ==> "")], "All"),
                        " ",
                        DOM.a (obj ["href" ==> "#/active"
                                    (if c.props.nowShowing = FilterTodo.Active then
                                        "className" ==> "selected"
                                     else "className" ==> "")], "Active"),
                        " ",
                        DOM.a( obj ["href" ==> "#/completed"
                                    (if c.props.nowShowing = FilterTodo.Completed then
                                        "className" ==> "selected"
                                     else "className" ==> "")], "Completed")
                    )
                ),
                clearButton,
                undoButton,
                redoButton
            )
        Fractal.defineComponent render
        |> Fractal.createComponent
        |> Fractal.createElement props

    type TodoAppProps = {model : TodoRepository}
    type TodoAppState = {nowShowing : FilterTodo; editing : Guid option; todos: Todo array }
    type todoApp = FractalComponent<TodoAppProps, TodoAppState>

    let TodoApp props =
        let getInitialState (c : todoApp) =
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
            let todoItems = todos |> Array.map(fun t ->
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
                DOM.section( obj ["className" ==> "main"],
                    DOM.input( obj ["className" ==> "toggle-all"
                                    "type" ==> "checkbox"
                                    "onChange" ==> toggleAll c
                                    "checked" ==> (activeCount = 0)]),
                    DOM.ul( obj ["className" ==> "todo-list"], todoItems)
                )

            DOM.div( (null |> unbox<obj>),
                DOM.header( obj ["className" ==> "header"],
                    DOM.h1( null, "todos" ),
                    DOM.input( obj ["ref" ==> "newField"
                                    "className" ==> "new-todo"
                                    "placeholder" ==> "What needs to be done?"
                                    "onKeyDown" ==> handleKeyDown c
                                    "autoFocus" ==> true])
                ),
                (if todos.Length > 0 then main else null |> unbox<DOMElement<obj>>),
                footer)

        Fractal.defineComponent render
        |> Fractal.getInitialState getInitialState
        |> Fractal.componentDidMount componentDidMount
        |> Fractal.createComponent
        |> Fractal.createElement props

    let app () =
        { model = TodoRepository () }
        |> TodoApp
        |> Fractal.render "todoapp"
        ()
