module rec FsAutoComplete.Tests.DependentFileChecking

open Expecto
open Utils.ServerTests
open Helpers
open System.IO
open Utils.Tests
open Utils.Server
open Utils.Server.Document
open System
open FSharp.Control.Reactive
open System.Reactive.Linq
open FSharpx.Control

let tests state =
  let root = Path.Combine(__SOURCE_DIRECTORY__, "TestCases", "DependentFileChecking", "SameProject")
  let aFile, bFile = "A.fs", "B.fs"
  testSequenced <| serverTestList (nameof DependentFileChecking) state defaultConfigDto (Some root) (fun server -> [
    testCaseAsync "When A is modified B is re-checked" (async {
      // open the files as they are on-disk and verify things are good
      let! (aDoc, aDiags) = Server.openDocument aFile server
      let! (bDoc, bDiags) = Server.openDocument bFile server
      use aDoc = aDoc
      use bDoc = bDoc
      Expect.isEmpty aDiags $"There should be no diagnostics in {aFile}"
      Expect.isEmpty bDiags $"There should be no diagnostics in {bFile}"

      let bDiagsStream = bDoc.CompilerDiagnostics
      // make a change to A (that is clearly incorrect)
      let! startAChange = Document.changeTextTo "farts" aDoc |> Async.StartChild
      // start listening for new diagnostics for B
      let! diagnosticsForBWaiter = bDiagsStream |> Observable.timeoutSpan (TimeSpan.FromSeconds 100.) |> Async.AwaitObservable |> Async.StartChild
      let! aDiags = startAChange
      Expect.isNonEmpty aDiags $"Should have had some compilation errors for {aFile} after erroneous changes"
      // observe that compilation errors are reported for B
      let! bDiags = diagnosticsForBWaiter
      Expect.isNonEmpty bDiags $"Should have some compilation errors for {bFile} after erroneous change to {aFile}"
    })

    testCaseAsync "When A is modified repeatedly, B is re-checked after each modification" (async {
      // open the files as they are on-disk and verify things are good
      let! (aDoc, aDiags) = Server.openDocument aFile server
      let! (bDoc, bDiags) = Server.openDocument bFile server
      use aDoc = aDoc
      use bDoc = bDoc
      Expect.isEmpty aDiags $"There should be no diagnostics in {aFile}"
      Expect.isEmpty bDiags $"There should be no diagnostics in {bFile}"
      let bDiagsStream = bDoc.CompilerDiagnostics
      for i in 0..10 do
        // make a change to A (that is clearly incorrect)
        let! startAChange = Document.changeTextTo "farts" aDoc |> Async.StartChild
        // start listening for new diagnostics for B
        let! diagnosticsForBWaiter = bDiagsStream |> Observable.skip (1 + i) |> Observable.timeoutSpan (TimeSpan.FromSeconds 100.) |> Async.AwaitObservable |> Async.StartChild
        let! aDiags = startAChange
        Expect.isNonEmpty aDiags $"Should have had some compilation errors for {aFile} after erroneous change #%d{i}"
        // observe that compilation errors are reported for B
        let! bDiags = diagnosticsForBWaiter
        Expect.isNonEmpty bDiags $"Should have some compilation errors for {bFile} after erroneous change #%d{i} to {aFile}"
    })

  ])
