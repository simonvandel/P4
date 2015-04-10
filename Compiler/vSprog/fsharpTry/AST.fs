﻿namespace vSprog

open Hime.CentralDogma
open Hime.Redist

module AST =
    type Primitive =
        | Int
        | Char
        | Real
        | Bool
        | Void

    type PrimitiveValue =
        | Int of int
        | Real of float
        | Bool of bool
        | Char of char
        | List of PrimitiveValue list

    type PrimitiveType =
        | SimplePrimitive of Primitive
        | ListPrimitive of PrimitiveType
        | ArrowPrimitive of PrimitiveType list
        | UserType of string
        | HasNoType

    and TypeDeclaration = string * PrimitiveType // name, type. Example: fieldName:int

    and AST = 
        | Program of AST list
        | Block of AST list
        | Body of AST list
        | Assignment of bool * string * AST // mutability, varId * value
        | Reassignment of Identifier * AST // varId, rhs
        | Initialisation of LValue * AST // lvalue, rhs
        | Constant of PrimitiveType * PrimitiveValue // type, value
        | Actor of AST * AST // name, body FIXME: Add more fields?
        | Struct of string * TypeDeclaration list // name, fields FIXME: Add more fields?
        | If of AST * AST // conditional, body
        | Send of string * string // actorName, msgName
        | Spawn of LValue * AST * AST // lvalue, actorName, initMsg
        | Receive of string * PrimitiveType * AST // msgName, msgType, body
        | ForIn of string * AST * AST // counterName, list, body
        | ListRange of AST list // content
        | Operation of AST * Operator * AST // lhs, op, rhs
        | Identifier of Identifier
        | Function of string * string list * PrimitiveType * AST// funcName, arguments, types, body
        | StructLiteral of (AST * AST) list // (fieldName, fieldValue) list
        | Invocation of string * string list // functionName, parameters
        //| Error // Only for making it compile temporarily

    and Identifier =
        | SimpleIdentifier of string // x
        | IdentifierAccessor of string list // x.y == ["x"; "y"]


    and Operator =
        | Plus
        | Minus
        | Modulo
        | Equals
        | Multiply

    and LValue = {
        identity:Identifier
        isMutable:bool
        primitiveType:PrimitiveType
    }

    and AssignmentStruct = {
        identity:string 
        isMutable:bool 
        declType:PrimitiveType 
        rhs:AST
        }

    let rec toPrimitiveType (input:ASTNode) : PrimitiveType =
        match input.Symbol.Value with
            | "int" -> SimplePrimitive Primitive.Int
            | "char" -> SimplePrimitive Primitive.Char
            | "real" -> SimplePrimitive Primitive.Real
            | "bool" -> SimplePrimitive Primitive.Bool
            | "void" -> SimplePrimitive Primitive.Void
            | "Types" -> 
                match input.Children.Count with
                | 1 -> toPrimitiveType (input.Children.Item 0)
                | n -> 
                    seq { for c in input.Children do
                          yield toPrimitiveType c
                        }
                    |> List.ofSeq
                    |> ArrowPrimitive
                
            | "ListType" -> ListPrimitive (toPrimitiveType (input.Children.Item 0))
            | "PrimitiveType" -> toPrimitiveType (input.Children.Item 0)
            | "Identifier" -> UserType (input.Children.Item 0).Symbol.Value
            | str -> UserType str

    let toMutability (input:ASTNode) : bool =
        match input.Symbol.Value with
                            | "let" -> false
                            | "var" -> true
                            | err -> failwith (sprintf "Mutability can never be: %s" err)

    let toLValue (mutability:ASTNode) (name:AST) (typeName:ASTNode) : LValue = 
        let isMutable = toMutability mutability
        {identity = (match name with
                    | Identifier id -> id)
                    ; 
        isMutable = isMutable;
        primitiveType = toPrimitiveType typeName}

    let toOperator (operator:string) : Operator =
        match operator with
        | "+" -> Plus
        | "-" -> Minus
        | "%" -> Modulo
        | "=" -> Equals
        | "*" -> Multiply
        | err -> failwith (sprintf "Not implemented yet %s" err)

    let astNodeAccess (childIds:int list) (startNode:ASTNode) : ASTNode =
        childIds
        |> List.fold (fun node n -> node.Children.Item n) startNode

    let rec toAST (root:ASTNode) : AST =
        match root.Symbol.Value with
        | "Program" ->
            let t = traverseChildren root
            Program t
        | "Body" ->
            let t = traverseChildren root
            Body t
        | "Block" -> 
            let t =traverseChildren root
            Block t
        | "Initialisation" ->
            let name = toAST (astNodeAccess [1;0] root)
            let typeName = (astNodeAccess [1;1] root)
            let lhs = toLValue (root.Children.Item 0) name typeName
            let rhs = toAST (root.Children.Item 2)
            Initialisation (lhs, rhs)
        | "Assignment" ->
            let mutability = toMutability (root.Children.Item 0)
            let name = match toAST (astNodeAccess [1;0] root) with
                       | Identifier (SimpleIdentifier str) -> str
                       | err -> failwith (sprintf "This should never be reached: %A" err)
            let rhs = toAST (root.Children.Item 2)
            Assignment (mutability, name, rhs)
        | "Reassignment" ->
            let assignables = match toAST (root.Children.Item 0) with
                              | Identifier id -> id
                              | err -> failwith (sprintf "This should never be reached: %A" err)
                                (*seq { for c in (root.Children.Item 0).Children do

                                          yield (c.Children.Item 0).Symbol.Value
                                 }
                              |> List.ofSeq *)
            let body = toAST (root.Children.Item 1)
            Reassignment (assignables, body)
        | "Integer" ->
            let value = Int (int ((root.Children.Item 0).Symbol.Value))
            Constant (SimplePrimitive (Primitive.Int), value) // FIXME: lav en int type. Lige nu bliver værdien af int konstanten ikke gemt
        | "Real" ->
            let value = Real ( float ((root.Children.Item 0).Symbol.Value))
            Constant (SimplePrimitive (Primitive.Real), value) // FIXME: lav en real type. Lige nu bliver værdien af real konstanten ikke gemt
        | "Actor" ->
            let name = toAST (root.Children.Item 0)
            let block = toAST (root.Children.Item 1)
            Actor (name, block)
        | "If" ->
            let conditional = toAST (root.Children.Item 0)
            let body = toAST (root.Children.Item 1)
            If (conditional, body)
        | "Boolean" ->
            let value = match (root.Children.Item 0).Symbol.Value with
                        | "true" -> true
                        | "false" -> false
                        | _ -> failwith "Something terribly went wrong in toAST boolean. This should never be reached."
            Constant (SimplePrimitive Primitive.Bool, Bool value)

        | "Struct" ->
            let name = ((root.Children.Item 0).Children.Item 0).Symbol.Value
            if root.Children.Count = 1 then
                Struct (name, []) // there might only be a name available for the struct; empty block
            else
                match (root.Children.Item 1).Symbol.Value with
                | "TypeDecl" -> 
                    let fieldName = (astNodeAccess [1;0;0] root).Symbol.Value
                    let typeName = astNodeAccess [1;1;0;0] root
                    Struct (name, [(fieldName, toPrimitiveType typeName)])
                | "TypeDecls" ->
                    let blocks = seq { for c in (root.Children.Item 1).Children do
                                          let fieldName = (astNodeAccess [0;0] c).Symbol.Value
                                          let typeName = astNodeAccess [1;0;0] c
                                          yield (fieldName, toPrimitiveType typeName)                        
                                 }
                                 |> List.ofSeq
                    Struct (name, blocks)
                | err -> failwith (sprintf "This should never be reached: %s" err)
        | "Send" ->
            let actorHandle = (astNodeAccess [0;0] root).Symbol.Value
            let msg = (astNodeAccess [1;0;0] root).Symbol.Value
            Send (actorHandle, msg)
        | "Spawn" ->
            let mutability = (root.Children.Item 0)
            let name = toAST (astNodeAccess [1;0] root)
            let typeName = (astNodeAccess [1;1;0;0] root)
            let lhs = toLValue mutability name typeName
            let actorName = toAST (root.Children.Item 2)
            let initMsg = toAST (astNodeAccess [3;0] root)
            Spawn (lhs, actorName, initMsg)
        | "Receive" ->
            let msgName = (astNodeAccess [0;0;0] root).Symbol.Value
            let msgType = toPrimitiveType (astNodeAccess [0;1;0;0] root)
            let body = toAST (root.Children.Item 1)
            Receive (msgName, msgType, body)
        | "ForIn" ->
            let counterName = (astNodeAccess [0;0;0] root).Symbol.Value
            let list = toAST (root.Children.Item 1)
            let body = toAST (root.Children.Item 2)
            ForIn (counterName, list, body)
        | "ListRange" ->
            let start = int (astNodeAccess [0;0;0;0] root).Symbol.Value
            let end' = int (astNodeAccess [0;1;0;0] root).Symbol.Value
            ListRange ([start..end'] |> List.map (fun n -> Constant (SimplePrimitive Primitive.Int, Int n)))
        | ("Factor" | "Term" | "Operation") ->
            match (root.Children.Count) with
            | 3 -> 
                let operation = toAST (root.Children.Item 0)
                let operator = toOperator (root.Children.Item 1).Symbol.Value
                let operand = toAST (root.Children.Item 2)
                Operation (operation, operator, operand)
            | 1 -> 
                toAST (root.Children.Item 0)
            | err -> failwith (sprintf "This should never be reached: %A" err)
        | "Identifier" ->
            match root.Children.Count with
            | 1 -> Identifier (SimpleIdentifier (root.Children.Item 0).Symbol.Value)
            | 2 -> 
                let ids = Seq.unfold (fun (node:ASTNode) -> 
                                        match node.Children.Count with
                                        | 0 -> None
                                        | _ -> Some ((node.Children.Item 0).Symbol.Value, (node.Children.Item 1))) 
                                        root
                          |> List.ofSeq
                Identifier (IdentifierAccessor ids)
        | "Function" ->
            let funcName = (astNodeAccess [0;0] root).Symbol.Value
            if root.Children.Count = 3 then // count is 3 when there is no arguments. fx f()
                let args = []
                let types = seq { for c in (root.Children.Item 1).Children do   
                                    yield (c.Children.Item 0)                    
                                }
                            |> List.ofSeq
                            |> List.map toPrimitiveType
                            |> fun xs -> if xs.Length = 1 then xs.Head else ArrowPrimitive xs


                let body = toAST (root.Children.Item 2)
                Function (funcName, args, types, body)
            else
                let args = seq { for c in (root.Children.Item 1).Children do   
                                   yield (c.Children.Item 0).Symbol.Value                    
                               }
                           |> List.ofSeq
                let types = seq { for c in (root.Children.Item 2).Children do   
                                    yield (c.Children.Item 0)                    
                                }
                            |> List.ofSeq
                            |> List.map toPrimitiveType
                            |> fun xs -> if xs.Length = 1 then xs.Head else ArrowPrimitive xs
                let body = toAST (root.Children.Item 3)
                Function (funcName, args, types, body)
        | "Invocation" ->
            let funcName = (astNodeAccess [0;0] root).Symbol.Value
            if root.Children.Count = 1 then // no parameters                
                Invocation (funcName, [])
            else
                let parameters = seq { for c in (root.Children.Item 1).Children do   
                                        yield (c.Children.Item 0).Symbol.Value                 
                                     }
                                 |> List.ofSeq
                Invocation (funcName, parameters)
        | "StructLiteral" ->
            let fields = seq { for c in root.Children do
                                let fieldName1 = toAST (c.Children.Item 0)
                                let fieldValue1 = toAST (c.Children.Item 1)
                                yield (fieldName1, fieldValue1)               
                             }
                         |> List.ofSeq
            StructLiteral fields
        | "String" ->

            let (chars:PrimitiveValue list) = (root.Children.Item 0).Symbol.Value
                                              |> fun str -> 
                                                str.Substring (1, (str.Length - 2))
                                              |> Seq.map PrimitiveValue.Char
                                              |> List.ofSeq
            Constant (ListPrimitive (SimplePrimitive Primitive.Char), PrimitiveValue.List chars)
        | sym -> 
            printfn "ERROR: No match case for: %A" sym
            failwith "not all cases matched in toAST"

    and traverseChildren (root:ASTNode) : AST list =
        List.ofSeq (seq { for i in root.Children -> i})
            |> List.map toAST
