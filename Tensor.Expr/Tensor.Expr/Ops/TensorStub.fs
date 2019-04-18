﻿namespace Tensor.Expr.Ops

open DeepNet.Utils
open Tensor
open Tensor.Backend
open Tensor.Expr


type AllocReq = {
    TypeName:   TypeName
    Dev:        ITensorDevice
    Size:       int64
}


[<ReferenceEquality>]
type AllocStub = {
    Req:        AllocReq
} with
    member this.TypeName = this.Req.TypeName
    member this.Dev = this.Req.Dev
    member this.Size = this.Req.Size

type AllocFn = AllocReq -> AllocStub

    
[<RequireQualifiedAccess>]
type StorageStub =
    /// Storage will be known at run-time.
    | Dynamic
    /// Allocated for internal results in the expression tree.
    | Allocated of AllocStub
    /// Storage of a variable.
    | VarStorage of VarName
    /// Fixed storage (already allocated before compilation).
    | Fixed of ITensorStorage


   
type TensorStub = {
    /// Shape (always known).
    Shape:          int64 list
    /// Data type (always known).
    TypeName:       TypeName
    /// Storage device (always known).
    Dev:            ITensorDevice
    /// Offset and strides (may be unknown at compile-time).
    OffsetStride:   (int64 * int64 list) option
    /// Storage (may be unknown at compile-time).
    Storage:        StorageStub
} with

    member this.DataType = this.TypeName.Type
    static member dataType (ts: TensorStub) = ts.DataType

    member this.Layout =
        match this.OffsetStride with
        | Some (offset, stride) ->
            Some {
                Shape = this.Shape
                Offset = offset
                Stride = stride
            }
        | None -> None
    static member layout (ts: TensorStub) = ts.Layout

    /// True, if layout (offset and strides) are known.
    member this.HasLayout = this.OffsetStride.IsSome

    /// Apply a new layout to the tensor stub.
    static member relayout (layout: TensorLayout) (ts: TensorStub) =
        {ts with
            Shape = layout.Shape
            OffsetStride = Some (layout.Offset, layout.Stride)
        }

    static member tryMapLayout (fn: TensorLayout -> TensorLayout) (ts: TensorStub) =
        match ts.Layout with
        | Some layout -> Some (ts |> TensorStub.relayout (fn layout))
        | None -> None     

    static member mapLayout (fn: TensorLayout -> TensorLayout) (ts: TensorStub) =
        match ts |> TensorStub.tryMapLayout fn with
        | Some res -> res
        | None -> failwithf "Cannot apply operation to layout-less to TensorStub %A." ts

    static member make (layout: TensorLayout, alloc: AllocStub) = {
        Shape = layout.Shape
        TypeName = alloc.TypeName
        Dev = alloc.Dev
        OffsetStride = Some (layout.Offset, layout.Stride)
        Storage = StorageStub.Allocated alloc
    }    

    static member alloc (allocFn: AllocReq -> AllocStub, dataType: TypeName, shape: int64 list, 
                         dev: ITensorDevice, ?order: TensorOrder) =
        let order = defaultArg order RowMajor
        let layout = 
            match order with
            | RowMajor -> TensorLayout.newRowMajor shape
            | ColumnMajor -> TensorLayout.newColumnMajor shape
            | CustomOrder perm -> TensorLayout.newOrdered shape perm
        let alloc = allocFn {TypeName=dataType; Dev=dev; Size=layout.NElems}
        TensorStub.make (layout, alloc)

    /// Transpose.
    member this.T = 
        this |> TensorStub.mapLayout TensorLayout.transpose

    /// True, if the layout is known and row major.
    static member isRowMajor (ts: TensorStub) =
        match ts.Layout with
        | Some layout -> TensorLayout.isRowMajor layout
        | None -> false

    //let isColumnMajor (ary: TensorManikin) =
    //    ary |> layout |> TensorLayout.isColumnMajor
        
    /// a view of the specified tensor over the given range 
    static member tryRange (rng: Rng list) ts =
        ts |> TensorStub.tryMapLayout (TensorLayout.view rng)

    /// Returns true, if no aliasing of elements can occur.
    static member isNotAliased ts =
        match TensorStub.layout ts with
        | Some layout -> TensorLayout.isNotAliased layout
        | None -> false

    /// Tries to reshape the tensor stub.
    /// For this to succeed, the stub's layout must be known and in row-major order.
    static member tryReshape shp (ts: TensorStub) =
        match ts.Layout |> Option.map (TensorLayout.tryReshape shp) with
        | Some (Some newLayout) -> Some (TensorStub.relayout newLayout ts)
        | _ -> None

    ///// Tries to reshape the tensor without copying.
    ///// For this to succeed, the tensor must have row-major layout.
    ///// If this a reshape without copying is impossible, an error is raised.
    //static member reshapeView shp a =
    //    match TensorStub.tryReshapeView shp a with
    //    | Some res -> res
    //    | None -> 
    //        let msg =
    //            sprintf "Cannot reshape tensor of shape %A and strides %A without copying."
    //                a.Shape a.Layout.Stride
    //        raise (System.InvalidOperationException msg)

    ///// Returns true if the tensor can be reshaped without copying.
    //static member canReshapeView shp a =
    //    match TensorStub.tryReshapeView shp a with
    //    | Some _ -> true
    //    | None -> false

    /// Permutes the axes as specified.
    static member tryPermuteAxes (permut: int list) (ts: TensorStub) =
        ts |> TensorStub.tryMapLayout (TensorLayout.permuteAxes permut)

    ///// inserts a broadcastable dimension of size one as first dimension
    //static member padLeft a =
    //    a |> TensorStub.relayout (a.Layout |> TensorLayout.padLeft)

    ///// appends a broadcastable dimension of size one as last dimension
    //static member padRight a =
    //    a |> TensorStub.relayout (a.Layout |> TensorLayout.padRight)

    ///// Inserts an axis of size 1 before the specified position.
    //static member insertAxis ax a =
    //    a |> TensorStub.relayout (a.Layout |> TensorLayout.insertAxis ax)

    ///// removes the first dimension from the tensor
    //static member cutLeft a =
    //    a |> TensorStub.relayout (a.Layout |> TensorLayout.cutLeft)
      
    ///// removes the last dimension from the tensor
    //static member cutRight a =
    //    a |> TensorStub.relayout (a.Layout |> TensorLayout.cutRight)

    ///// transpose
    //static member transpose (a: TensorStub) =
    //    a.T

    ///// C++ type string
    ////let cppType (a: TensorManikin) = 
    ////    a.CPPType

    /// Reverses the elements in the specified dimension.
    static member tryReverseAxis ax (ts: TensorStub) =
        ts |> TensorStub.tryMapLayout (TensorLayout.reverseAxis ax)      

    ///// Returns a view of the diagonal along the given axes.
    ///// The diagonal replaces the first axis and the second axis is removed.
    //static member diagAxis ax1 ax2 a =
    //    a |> TensorStub.relayout (a.Layout |> TensorLayout.diagAxis ax1 ax2)

    /// broadcasts the tensor to the given shape
    static member tryBroadcastTo shp (ts: TensorStub) =
        ts |> TensorStub.tryMapLayout (TensorLayout.broadcastToShape shp)

    ///// returns true if at least one dimension is broadcasted
    //static member isBroadcasted (a: TensorStub) =
    //    a.Layout |> TensorLayout.isBroadcasted 

    ///// size of the used data type 
    //let typeSize ary =
    //    ary |> typeName |> TypeName.size

    ///// size of the used data type as int64
    //let typeSize64 ary =
    //    ary |> typeName |> TypeName.size64

    ///// offset in bytes
    //let offsetInBytes (ary: TensorManikin) =
    //    typeSize64 ary * ary.Layout.Offset

    ///// address of given element in bytes (relative to start of array)
    //let addrInBytes idx (ary: TensorManikin) =
    //    typeSize64 ary * (ary.Layout |> TensorLayout.addr idx)

    ///// size in bytes 
    //let sizeInBytes (ary: TensorManikin) =
    //    typeSize64 ary * TensorLayout.nElems ary.Layout

    ///// True if array can be target of BLAS operation.
    //let canBeBlasTarget (ary: TensorManikin) =
    //    let nd = ary.NDims
    //    if nd >= 2 then
    //        let st = ary.Layout.Stride
    //        let shp = ary.Shape
    //        match st.[nd-2 ..] with
    //        | [1L; ld] when ld >= 1L && ld >= shp.[nd-2] -> true
    //        | _ -> false
    //    else false

    ///// true if a and b may overlap
    //let maybeOverlapping a b =    
    //    storage a = storage b
        
