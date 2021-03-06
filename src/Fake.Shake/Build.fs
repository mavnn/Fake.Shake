[<AutoOpen>]
module Fake.Shake.Build
open System.Collections.Concurrent
open Fake
open Fake.Shake.Core
open Fake.Shake.Control

type [<NoComparison>] Cache =
    {
        Dependencies : ConcurrentDictionary<Key, Key list>
        Results : Map<Key, obj>
    }

type FakeShakeConfig =
    {
        CacheFile : string
        PostBuildCheck : bool
    }
    static member Default =
        {
            CacheFile = ".fake" @@ "shake.cache"
            PostBuildCheck = true
        }

let build { CacheFile = cacheFile; PostBuildCheck = postBuildCheck } rules key =
    let (old : Cache) =
        try
            System.IO.File.ReadAllBytes cacheFile
            |> binary.UnPickle
        with
        | _ ->
            { Dependencies = ConcurrentDictionary(); Results = Map.empty }
    let state =
        {
            Rules = rules
            Results = ConcurrentDictionary()
            OldResults = old.Results
            Current = None
            Dependencies = old.Dependencies
            Stack = []
        }
    let finalState, result =
       (require key state).Force() |> Async.RunSynchronously
    let mergedResults =
        finalState.Results.ToArray()
        |> Seq.map (fun kv -> kv.Key, kv.Value.Force() |> Async.RunSynchronously)
        |> Map.ofSeq
        |> Map.fold (fun old k o -> Map.add k o old) old.Results
    let cache = { Dependencies = finalState.Dependencies; Results = mergedResults }
    System.IO.Directory.CreateDirectory(".fake") |> ignore
    System.IO.File.WriteAllBytes(cacheFile, binary.Pickle cache)
    if postBuildCheck && (not <| skip key { state with OldResults = cache.Results }) then
        traceImportant <| sprintf "Build complete, but %A does not have a valid stored result." key
    result

let readLines fileName =
  action {
       do! need (Key fileName)
       return System.IO.File.ReadAllLines fileName |> Array.toList
    }
