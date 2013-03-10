﻿#r "../../lib/FSharp.Compiler.dll"

#load "SourceParser.fs"
#load "FormatConfig.fs"
#load "CodePrinter.fs"
#load "CodeFormatter.fs"

open Fantomas.SourceParser
open Fantomas.FormatConfig
open Fantomas.CodePrinter
open Fantomas.CodeFormatter

let config = FormatConfig.Default

let t03 = parse """
    type AParameters = { a : int }
    type X = | A of AParameters | B
    let f (r : X) =
        match r with
        | X.A ({ a = aValue } as t) -> aValue
        | X.B -> 0"""

let t06 = parse """
    [<AttributeUsage(AttributeTargets.Method, AllowMultiple = true)>]
    type TestAttribute([<ParamArray>] parameters: obj[])  =
        inherit Attribute()
        member this.Parameters = parameters"""

let t08 = parse """
    /// This is a foo function
    module Tests
    let foo() = 
        // Line comment
        let msg = "Hello world"
        printfn "%s" msg"""

let t10 = parse """
    exception Error2 of string * int
    with member __.Message = "ErrorMessage"
    """

let t11 = parse """
    namespace Core
    type A = A
    """

//printfn "Result:\n%s" <| format t01 config;;
//printfn "Result:\n%s" <| format t02 config;;
printfn "Result:\n%s" <| format t03 config;;
//printfn "Result:\n%s" <| format t04 config;;
//printfn "Result:\n%s" <| format t05 config;;
printfn "Result:\n%s" <| format t06 config;;
//printfn "Result:\n%s" <| format t07 config;;
printfn "Result:\n%s" <| format t08 config;;
//printfn "Result:\n%s" <| format t09 config;;
printfn "Result:\n%s" <| format t10 config;;
printfn "Result:\n%s" <| format t11 config;;