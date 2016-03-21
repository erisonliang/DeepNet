﻿namespace SymTensor.Compiler

open System.Runtime.InteropServices
open ManagedCuda
open Basics
open ArrayNDNS
open SymTensor


[<AutoOpen>]
module ArrayNDManikinTypes = 
    open ArrayND

    /// represents a memory allocation execlusively for this expression (used for temporary results)
    type MemAllocManikinT = {
        Id:             int; 
        TypeName:       TypeNameT; 
        Elements:       int;
    }

    /// Represents memory. 
    /// Memory can either be internal to this expression or external (passed in variable at runtime).
    /// Memory can either be on the host or the accelerator.
    type MemManikinT =
        | MemAlloc of MemAllocManikinT
        | MemExternal of UVarSpecT

    /// represents an n-dimensional array that will be allocated or accessed during execution 
    type ArrayNDManikinT (layout: ArrayNDLayoutT, storage: MemManikinT) = 
        inherit ArrayNDT<int> (layout)  // generic type does not matter since we do not store data

        /// storage manikin
        member this.Storage = storage

        /// typename of the data stored in this array
        member this.TypeName = 
            match storage with
            | MemAlloc {TypeName=tn} -> tn
            | MemExternal vs -> vs.TypeName

        override this.Item
            with get pos = failwith "ArrayNDManikin does not store data"
            and set pos value = failwith "ArrayNDManikin does not store data"

        override this.NewOfSameType (layout: ArrayNDLayoutT) = 
            failwith "ArrayNDManikin cannot allocate memory on its own"

        override this.NewView (layout: ArrayNDLayoutT) = 
            ArrayNDManikinT(layout, storage) :> ArrayNDT<int>

        override this.DataType =
            TypeName.getType this.TypeName

        override this.Location = ArrayLoc "Manikin"

        /// C++ type name for ArrayND with static shape and dynamic offset/strides
        member this.DynamicCPPType =
            let dims = ArrayNDLayout.nDims layout
            let shp = ArrayNDLayout.shape layout
            let cppDataType = Util.cppType this.DataType
            let shapeStr = 
                if dims = 0 then "" 
                else "<" + (shp |> Util.intToStrSeq |> String.concat ",") + ">"
            sprintf "ArrayND%dD<%s, ShapeStatic%dD%s, StrideDynamic%dD>" 
                dims cppDataType dims shapeStr dims        




module ArrayNDManikin =
    open ArrayND

    /// creates a new MemoryManikinT and a new ArrayNDManikinT with contiguous layout
    let inline newContiguous memAllocator typ shape = 
        let layout = ArrayNDLayout.newContiguous shape
        ArrayNDManikinT (layout, 
                         (memAllocator typ (ArrayNDLayout.nElems layout)))

    /// creates a new MemoryManikinT and a new ArrayNDManikinT with Fortran layout
    let inline newColumnMajor memAllocator typ shape = 
        let layout = ArrayNDLayout.newColumnMajor shape
        ArrayNDManikinT (layout, 
                         (memAllocator typ (ArrayNDLayout.nElems layout)))

    /// creates a new ArrayNDManikinT with contiguous layout using the specified storage
    let inline externalContiguous storage shape =
        let layout = ArrayNDLayout.newContiguous shape
        ArrayNDManikinT (layout, storage) 

    /// storage
    let inline storage (ary: ArrayNDManikinT) =
        ary.Storage

    /// used data type name
    let inline typeName (ary: ArrayNDManikinT) =
        ary.TypeName

    /// size of the used data type 
    let inline typeSize ary =
        ary |> typeName |> TypeName.getType |> Marshal.SizeOf

    /// offset in bytes
    let inline offsetInBytes ary =
        (typeSize ary) * (ArrayND.offset ary)

    /// size in bytes 
    let inline sizeInBytes ary =
        (typeSize ary) * (ArrayND.nElems ary)

        
