﻿namespace SymTensor.Compiler.Cuda

open System
open System.Reflection

open Tensor.Utils
open Tensor
open SymTensor
open UExprTypes
open SymTensor.Compiler


module CudaEval =

    /// CUDA GPU expression evaluator
    let cudaEvaluator (compileEnv: CompileEnvT) (uexprs: UExprT list)  =
        let channels = uexprs |> List.mapi (fun i _ -> sprintf "EXPR%d" i)
        let channelExprs = List.zip channels uexprs |> Map.ofList

        // build recipe and create workspace
        let rcpt, diagnostics = 
            {CompileEnv=compileEnv; UExprs=channelExprs; OwnerUExpr=None}
            |> CudaRecipe.buildFromDesc 
        let workspace = new CudaExprWorkspace (rcpt)

        // evaluator
        let evalFn = fun (evalEnv: EvalEnvT) ->           
            // create arrays for results and add them to VarEnv
            let resArrays = rcpt.ChannelAllocators |> Map.map (fun _ alloc -> alloc ())
            let varEnv =
                (evalEnv.VarEnv, channels)
                ||> List.fold (fun varEnv ch -> 
                                    match rcpt.ChannelVars.[ch] with
                                    | Some vs -> varEnv |> VarEnv.addVarSpec vs resArrays.[ch]
                                    | None -> varEnv)

            // evaluate
            workspace.Eval varEnv
            channels |> List.map (fun ch -> resArrays.[ch])

        evalFn, Some diagnostics


[<AutoOpen>]
module CudaEvalTypes =

    type private HelperT =
        static member Allocator<'T when 'T: (new: unit -> 'T) and 'T: struct and 'T :> ValueType> 
                (shp: NShapeSpecT) : Tensor<'T> =
            Tensor<'T> (shp, CudaTensor.Dev)

        static member ToDev<'T when 'T: (new: unit -> 'T) and 'T: struct and 'T :> ValueType> 
                (ary: Tensor<'T>) : Tensor<'T> =
            CudaTensor.transfer ary

        static member ToHost<'T when 'T: (new: unit -> 'T) and 'T: struct and 'T :> ValueType> 
                (ary: Tensor<'T>) : Tensor<'T> =
            HostTensor.transfer ary

    let private invokeHelperMethod<'T> name args = 
        let gm = typeof<HelperT>.GetMethod (name, Util.allBindingFlags)
        let m = gm.MakeGenericMethod ([|typeof<'T>|])
        m.Invoke(null, args)  


    /// Evaluates the model on a CUDA GPU.
    let DevCuda = { 
        new IDevice with
            
            #if !CUDA_DUMMY
            member this.Allocator shp : Tensor<'T>    = invokeHelperMethod<'T> "Allocator" [|shp|] |> unbox            
            member this.ToDev ary : Tensor<'T1>       = invokeHelperMethod<'T1> "ToDev" [|ary|] |> unbox 
            member this.ToHost ary : Tensor<'T2>      = invokeHelperMethod<'T2> "ToHost" [|ary|] |> unbox 

            #else

            member this.Allocator shp : ArrayNDT<'T> = ArrayNDHost.newContiguous shp
            member this.ToDev ary : ArrayNDT<'T1> = box |> unbox 
            member this.ToHost ary : ArrayNDHostT<'T2>  = box |> unbox 
            #endif
            
            member this.Compiler = { new IUExprCompiler with 
                                        member this.Name = "Cuda"
                                        member this.Compile env exprs = CudaEval.cudaEvaluator env exprs }
            member this.DefaultLoc = CudaTensor.Dev
            member this.DefaultFactory = this.Compiler, {CompileEnv.empty with ResultLoc=this.DefaultLoc}

    }


