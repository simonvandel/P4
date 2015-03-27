namespace vSprog

open vSprog.Parser
open vSprog.Analysis
open vSprog.CommonTypes
open vSprog.AST
open vSprog.ParserUtils
open System.IO

module Main =
    [<EntryPoint>]
    let main argv = 
        let input = File.ReadAllText "../../GoldenCode.bar"

        let lift m = Success m

        let res = parse input
                  >>= (fun parseTree -> 
                                        //printTree parseTree 0
                                        lift (toAST parseTree))
                  >>= analyse

        match res with
        | Success _ -> printfn "%s" "success"
        | Failure errs -> 
            printfn "%s" "Errors:"
            errs |> List.iter (printfn "%s")

        System.Console.ReadLine()
        0 // return an integer exit code
