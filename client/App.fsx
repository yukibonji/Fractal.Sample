#load "Model.fsx"

namespace Fractal.Sample

open FunScript.TypeScript.Mui
open FunScript.TypeScript.React
open FunScript.TypeScript
open FunScript
open Fractal
open System.Collections.Generic
open System
open Model

[<ReflectedDefinition>]
module App =
    type TodoItemState = {editingText : string}
    type TodoItemProps = {todo : Todo; editing : bool}
    type todoItem =  FractalComponent<TodoItemProps, TodoItemState>

    let TodoItem props =
        let onToggle (c : todoItem) (e : FormEvent) =
            Message.publish "todo.toggle" c.props.todo.id

        let onDestroy (c : todoItem) (e : React.MouseEvent) =
            Message.publish "todo.remove" c.props.todo.id

        let handleSubmit (c : todoItem) (e : React.FocusEvent) =
            let v = c.state.editingText.Trim()
            if String.IsNullOrEmpty v |> not then
                c.setState {editingText = v}
                Message.publish "todo.view.editDone" ()
                Message.publish "tood.save" (c.props.todo.id, c.state.editingText)
            else
                Message.publish "todo.remove" c.props.todo.id

        let handleEdit (c : todoItem) (e : React.MouseEvent) =
            Message.publish "todo.view.edit" c.props.todo.id
            c.setState {editingText = c.props.todo.title}

        let handleKeyDown (c : todoItem) (e : React.KeyboardEvent) =
            if e.which = 27. then
                c.setState {editingText = c.props.todo.title}
                Message.publish "todo.view.editDone" ()
            elif e.which = 13. then
                handleSubmit c (e |> unbox<React.FocusEvent>)

        let handleChange (c : todoItem) (e : FormEvent) =
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
            DOM.li ((if c.props.todo.completed then [|ClassName "completed"|]
                     elif c.props.editing then [|ClassName "editing"|]
                     else [| |]),
                DOM.div ([|ClassName "view"|],
                    DOM.input( [| ClassName "toggle"; Attr.Type "checkbox"; Checked c.props.todo.completed; OnChange (onToggle c)|]),
                    DOM.label([| OnDoubleClick (handleEdit c) |], c.props.todo.title),
                    DOM.button([| ClassName "destroy"; OnClick (onDestroy c) |])),
                DOM.input([| Ref "editField"; ClassName "edit"; Value c.state.editingText;
                             OnBlur (handleSubmit c); OnChange ( handleChange c); OnKeyDown (handleKeyDown c) |])
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

    type TodoFooterProps = {
        count : int; completeCount : int; canUndo : bool;
        canRedo : bool; nowShowing : FilterTodo}
    type todoFooter = FractalComponent<TodoFooterProps, Nothing>

    let TodoFooter props =
        let onClearCompleted (e : React.MouseEvent) =
            Message.publish "todo.repository.clearSelected" ()

        let onUndo (e : React.MouseEvent) =
            Message.publish "todo.repository.undo" ()

        let onRedo (e : React.MouseEvent) =
            Message.publish "todo.repository.redo" ()

        let render (c : todoFooter) =
            let clearButton =
                DOM.button([| ClassName "clear-completed"; OnClick (onClearCompleted); Disabled (c.props.completeCount > 0) |], "Clear completed")

            let undoButton =
                DOM.button([| ClassName "clear-completed"; OnClick (onUndo); Disabled (c.props.canUndo) |], "Undo")

            let redoButton =
                DOM.button([| ClassName "clear-completed"; OnClick (onRedo); Disabled (c.props.canRedo) |], "Redo") 

            DOM.footer( [| ClassName "footer" |],
                DOM.span( [| ClassName "todo-count" |],
                    DOM.strong([| |], c.props.count),
                    " todo(s) left"
                ),
                DOM.ul( [| ClassName "filters" |],
                    DOM.li( [||],
                        DOM.a( [| Href "#/"; ClassName (if c.props.nowShowing = FilterTodo.All then "selected" else "" ) |]  , "All"),
                        " ",
                        DOM.a( [| Href "#/active"; ClassName (if c.props.nowShowing = FilterTodo.Active then "selected" else "" ) |]  , "Active"),
                        " ",
                        DOM.a( [|Href "#/completed"; ClassName (if c.props.nowShowing = FilterTodo.Completed then "selected" else "" ) |]  , "Completed")
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

        let handleKeyDown (c : todoApp) (e : React.KeyboardEvent) =
            if e.which = 13. then
                e.preventDefault()
                let v = Globals.findDOMNode(c.refs.["newField"]).value.Trim()
                if String.IsNullOrEmpty v |> not then
                    {id = Guid.NewGuid(); title = v; completed = false }
                    |> Message.publish "todo.new"
                    Globals.findDOMNode(c.refs.["newField"]).value <- ""

        let toggleAll (c : todoApp) (e : FormEvent) =
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
                DOM.section( [| ClassName "main" |],
                    DOM.input( [| ClassName "toggle-all"; Attr.Type "checkbox"; OnChange (toggleAll c); Checked (activeCount = 0) |]),
                    DOM.ul( [| ClassName "todo-list" |], todoItems)
                )

            DOM.div( [||],
                DOM.header( [|ClassName "header" |],
                    DOM.h1( [||], "todos" ),
                    DOM.input( [| Ref "newField"; ClassName "new-todo"; Placeholder "What needs to be done?"; OnKeyDown (handleKeyDown c); AutoFocus true |])
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
