﻿module Fantomas.FormatConfig

open System
open System.IO
open System.Text.RegularExpressions
open System.CodeDom.Compiler

type Num = int

type FormatConfig = 
    { /// Number of spaces for each identation
      IndentSpaceNum : Num;
      /// Length of long identifers to consider breaking to multiple lines
      LongIdentLength : Num;
      SemicolonAtEndOfLine : bool;
      SpaceBeforeArgument : bool;
      SpaceBeforeColon : bool;
      SpaceAfterComma : bool;
      SpaceAfterSemicolon : bool;
      IndentOnTryWith : bool }
    static member Default = 
        { IndentSpaceNum = 4; LongIdentLength = 10;
          SemicolonAtEndOfLine = true; SpaceBeforeArgument = false; SpaceBeforeColon = true;
          SpaceAfterComma = true; SpaceAfterSemicolon = true; IndentOnTryWith = false }

/// Wrapping IndentedTextWriter with a current column position
type ColumnIndentedTextWriter(tw : TextWriter) =
    let indentWriter = new IndentedTextWriter(tw, " ")
    let mutable col = 0
    let mutable cursor = 0
    member __.Write(s : string) =
        match s.LastIndexOf('\n') with
        | -1 -> col <- col + s.Length
        | i -> col <- col + s.Length - i - 1
        indentWriter.Write(s)
    member __.WriteLine(s : string) =
        col <- 0
        indentWriter.WriteLine(s)
    /// Current column of the page
    member __.Column = col
    member __.Indent 
        with get() = indentWriter.Indent
        and set i = indentWriter.Indent <- i
    member __.InnerWriter = indentWriter.InnerWriter
    interface IDisposable with
        member __.Dispose() =
            indentWriter.Dispose()    

type Context = 
    { Config : FormatConfig; 
      Writer: ColumnIndentedTextWriter;
      /// The original source string to query as a last resort 
      Content : string; 
      /// Positions of new lines in the original source string
      Positions : int [] }
    /// Initialize with a string writer and use space as delimiter
    static member Default = { Config = FormatConfig.Default; 
                              Writer = new ColumnIndentedTextWriter(new StringWriter());
                              Content = ""; Positions = [||] }
    static member createContext config (content : string) =
        let positions = 
            content.Split([|'\n'|], StringSplitOptions.None)
            |> Seq.map (fun s -> String.length s + 1)
            |> Seq.scan (+) 0
            |> Seq.toArray
        { Context.Default with Config = config; Content = content; Positions = positions }

let dump (ctx: Context) = ctx.Writer.InnerWriter.ToString()

// A few utility functions from https://github.com/fsharp/powerpack/blob/master/src/FSharp.Compiler.CodeDom/generator.fs

/// Indent one more level based on configuration
let indent (ctx : Context) = 
    ctx.Writer.Indent <- ctx.Writer.Indent + ctx.Config.IndentSpaceNum
    ctx

/// Unindent one more level based on configuration
let unindent (ctx : Context) = 
    ctx.Writer.Indent <- max 0 (ctx.Writer.Indent - ctx.Config.IndentSpaceNum)
    ctx

/// Increase indent by i spaces
let incrIndent i (ctx : Context) = 
    ctx.Writer.Indent <- ctx.Writer.Indent + i
    ctx

/// Decrease indent by i spaces
let decrIndent i (ctx : Context) = 
    ctx.Writer.Indent <- max 0 (ctx.Writer.Indent - i)
    ctx

/// Apply function f at an absolute indent level (use with care)
let atIndentLevel level (f : Context -> Context) ctx =
    if level < 0 then
        invalidArg "level" "The indent level cannot be negative."
    let oldLevel = ctx.Writer.Indent
    ctx.Writer.Indent <- level
    let result = f ctx
    ctx.Writer.Indent <- oldLevel
    result

/// Write everything at current column indentation
let atCurrentColumn (f : _ -> Context) (ctx : Context) =
    match ctx.Writer.Column with
    | 0 -> f ctx
    | c -> atIndentLevel (ctx.Writer.Indent + c) f ctx

/// Function composition operator
let (+>) (ctx : Context -> Context) (f : _ -> Context) x =
    f (ctx x)

/// Break-line and append specified string
let (++) (ctx : Context -> Context) (str : string) x =
    let c = ctx x
    c.Writer.WriteLine("")
    c.Writer.Write(str)
    c

/// Append specified string without line-break
let (--) (ctx : Context -> Context) (str : string) x =
    let c = ctx x
    c.Writer.Write(str)
    c

let (!-) (str : string) = id -- str 
let (!+) (str : string) = id ++ str 

/// Call function, but give it context as an argument      
let withCtxt f x =
    (f x) x

/// Print object converted to string
let str (o : 'T) (ctx : Context) =
    ctx.Writer.Write(o.ToString())
    ctx

/// Process collection - keeps context through the whole processing
/// calls f for every element in sequence and f' between every two elements 
/// as a separator. This is a variant that works on typed collections.
let col f' (c : seq<'T>) f (ctx : Context) =
    let mutable tryPick = true in
    let mutable st = ctx
    let e = c.GetEnumerator()   
    while (e.MoveNext()) do
        if tryPick then tryPick <- false else st <- f' st
        st <- f (e.Current) st
    st

/// Similar to col, apply one more function f2 at the end if the input sequence is not empty
let colPost f2 f1 (c : seq<'T>) f (ctx : Context) =
    if Seq.isEmpty c then ctx
    else f2 (col f1 c f ctx)

/// Similar to col, apply one more function f2 at the beginning if the input sequence is not empty
let colPre f2 f1 (c : seq<'T>) f (ctx : Context) =
    if Seq.isEmpty c then ctx
    else col f1 c f (f2 ctx)

/// If there is a value, apply f and f' accordingly, otherwise do nothing
let opt (f' : Context -> _) o f (ctx : Context) =
    match o with
    | Some x -> f' (f x ctx)
    | None -> ctx

/// Similar to opt, but apply f2 at the beginning if there is a value
let optPre (f2 : _ -> Context) (f1 : Context -> _) o f (ctx : Context) =
    match o with
    | Some x -> f1 (f x (f2 ctx))
    | None -> ctx

/// b is true, apply f1 otherwise apply f2
let ifElse b (f1 : Context -> Context) f2 (ctx : Context) =
    if b then f1 ctx else f2 ctx

/// Repeat application of a function n times
let rep n (f : Context -> Context) (ctx : Context) =
    [1..n] |> List.fold (fun c _ -> f c) ctx

let wordAnd = !- " and "
let wordOr = !- " or "
let wordOf = !- " of "   

// Separator functions        
let sepDot = !- "."
let sepSpace = !- " "      
let sepNln = !+ ""
let sepStar = !- " * "
let sepEq = !- " = "
let sepArrow = !- " -> "
let sepWild = !- "_"
let sepNone = id
let sepBar = !- "| "

let sepOpenL = !- "["
let sepCloseL = !- "]"
let sepOpenA = !- "[|"
let sepCloseA = !- "|]"
let sepOpenS = !- "{ "
let sepCloseS = !- " }"

let inline sepColon(ctx : Context) = 
    if ctx.Config.SpaceBeforeColon then str " : " ctx else str ": " ctx

let inline sepComma(ctx : Context) = 
    if ctx.Config.SpaceAfterComma then str ", " ctx else str "," ctx

let inline sepSemi(ctx : Context) = 
    if ctx.Config.SpaceAfterSemicolon then str "; " ctx else str ";" ctx

let sepSemiNln =
    /// !+ part is essential to indentation
    withCtxt(fun ctx -> if ctx.Config.SemicolonAtEndOfLine then !- ";" ++ "" else !+ "")

let inline sepBeforeArg(ctx : Context) = 
    if ctx.Config.SpaceBeforeArgument then str " " ctx else str "" ctx

/// Conditional indentation on with keyword
let inline indentWith(ctx : Context) =
    if ctx.Config.IndentOnTryWith then indent ctx else ctx

/// Conditional unindentation on with keyword
let inline unindentWith(ctx : Context) =
    if ctx.Config.IndentOnTryWith then unindent ctx else ctx

