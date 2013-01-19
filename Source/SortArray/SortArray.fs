﻿// Copyright (c) 2013 Semyon Grigorev <rsdpisuy@gmail.com>
// All rights reserved.
// 
// The contents of this file are made available under the terms of the
// Eclipse Public License v1.0 (the "License") which accompanies this
// distribution, and is available at the following URL:
// http://www.opensource.org/licenses/eclipse-1.0.php
// 
// Software distributed under the License is distributed on an "AS IS" basis,
// WITHOUT WARRANTY OF ANY KIND, either expressed or implied. See the License for
// the specific language governing rights and limitations under the License.
// 
// By using this software in any fashion, you are agreeing to be bound by the
// terms of the License.

module SortArray

open Brahma.Samples
open OpenCL.Net
open Brahma.OpenCL
open Brahma.FSharp.OpenCL.Wrapper
open Microsoft.FSharp.Quotations
open Brahma.FSharp.OpenCL.Extensions

let random = new System.Random()

let platformName = "*"
let deviceType = Cl.DeviceType.Default
let provider =
        try  ComputeProvider.Create(platformName, deviceType)
        with 
        | ex -> failwith ex.Message
let commandQueue = new CommandQueue(provider, provider.Devices |> Seq.head)

let length = 100000000

let baseArr = Array.init length (fun _ -> random.Next(10))

let cpuArr = Array.copy baseArr
let gpuArr = Array.copy baseArr
let b = Array.zeroCreate (min (length/2+1) ((length + 1)/2))

let gpuSum (arr:array<_>) = 
    let command = 
        <@
            fun (rng:_1D) l (a:array<_>) (b:array<_>) ->
                let r = rng.GlobalID0
                let x = r * 2
                if l = x
                then b.[r] <- a.[x]
                else b.[r] <- a.[x] + a.[x + 1]
        @>    
    let length = arr.Length    
    let kernel, kernelPrepare, kernelRun = provider.Compile command
    let mutable bufLen = min (length/2+1) ((length + 1)/2)
    let mutable curL = length
    let mutable flip = false    
    while curL > 1 do
        let d =(new _1D(bufLen,1))
        if flip
        then kernelPrepare d curL b arr
        else kernelPrepare d curL arr b
        flip <- not flip
        let _ = commandQueue.Add(kernelRun())
        curL <- bufLen
        bufLen <- bufLen/2
    let _ = commandQueue.Add(kernelRun()).Finish()
    let _ = commandQueue.Add((if flip then b else arr).ToHost(kernel)).Finish()
    let _ = commandQueue.Add(arr.ToHost(kernel)).Finish()
    let sum = (if flip then b else arr).[0]
    commandQueue.Dispose()
    sum

let gpuiter (arr:array<_>) = 
    let command = 
        <@
            fun (rng:_1D) (a:array<_>) ->
                let r = rng.GlobalID0
                let x = a.[r]
                a.[r] <- x*x*x*x*x*x*x*x                
        @>
    let kernel, kernelPrepare, kernelRun = provider.Compile command    
    let d =(new _1D(arr.Length,1))    
    kernelPrepare d arr        
    let _ = commandQueue.Add(kernelRun()).Finish()    
    let _ = commandQueue.Add(arr.ToHost(kernel)).Finish()    
    ()

let gpuSort (arr:array<_>) =
    let command = 
        <@
            fun (rng:_1D) l frame (a:array<_>) ->
                let r = rng.GlobalID0
                let first = r * frame
                let mutable mid = first + frame
                let mutable left = first
                let mutable right = mid+1
                let mutable last = first + frame
                if l - 1 - last < frame then last <- l - 1
                        
                while (left <= mid && right <= last) do
                   // Select from left:  no change, just advance left
                    if a.[left] <= a.[right]
                    then left <- left + 1
                    // Select from right:  rotate [left..right] and correct
                    else
                       let tmp = a.[right]     // Will move to [left]                       
                       for j in [right - 1  .. left] do a.[j + 1] <- a.[j]     
                       a.[left] <- tmp
                       // EVERYTHING has moved up by one
                       left <- left + 1
                       mid <- mid + 1
                       right <- right + 1
        @>
    let s = string command
    command |> printfn "%A"
    let mutable localWorkSize = 1
    let kernel, kernelPrepare, kernelRun = provider.Compile command
    let d =(new _1D(length, 1))
    kernelPrepare d arr.Length 1 arr
    let _ = commandQueue.Add(kernelRun()).Finish()
    arr.ToHost(kernel)
//    for i in 0 .. iterations - 1 do
//        Timer<string>.Global.Start()
//        let _ = commandQueue.Add(kernelRun()).Finish()  

let time f = 
    let start = System.DateTime.Now
    f()
    System.DateTime.Now - start

//let cpuSum = ref 0
//(fun () -> cpuSum := Array.sum cpuArr )
//|> time
//|> printfn "cpu time: %A"  
//
//
//let _gpuSum = ref 0
//(fun () -> _gpuSum := gpuSum gpuArr )
//|> time
//|> printfn "gpu time: %A"  
//
//printfn "%A" cpuSum
//printfn "%A" _gpuSum


(fun () -> Array.iteri (fun i x -> cpuArr.[i] <- x*x*x*x*x*x*x*x) cpuArr )
|> time
|> printfn "cpu iter time: %A"  


(fun () -> gpuiter cpuArr )
|> time
|> printfn "cpu iter time: %A"  


//gpuSort [|1;2|] |> printfn "%A"


//_gpuSum = cpuSum |> printfn "%A"