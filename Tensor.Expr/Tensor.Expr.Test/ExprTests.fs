﻿module ExprTests

open Xunit
open FsUnit.Xunit

open DeepNet.Utils
open Tensor.Utils
open Tensor
open Tensor.Expr


let dumpExpr (expr: Expr) =
    printfn "Expr: %s" (expr.ToString())
    printfn "==== DataType:           %A" expr.DataType
    printfn "==== Shape:              %A" expr.Shape
    printfn "==== CanEvalAllSymSizes: %A" expr.CanEvalAllSymSizes
    printfn "==== Vars:               %A" expr.Vars
    printfn ""


[<Fact>]
let ``Expr: a + b`` () =
    let a = Var.make<float32> ("a", [SizeSpec.fix 10L; SizeSpec.fix 20L])
    let b = Var.make<float32> ("b", [SizeSpec.fix 10L; SizeSpec.fix 20L])
    let expr = Expr a + Expr b
    printfn "a+b:"
    dumpExpr expr
    
    assert (expr.DataType = typeof<float32>)
    assert (expr.Shape = [SizeSpec.fix 10L; SizeSpec.fix 20L])
    assert (expr.CanEvalAllSymSizes = true)
    assert (expr.Vars = Set [a; b])




