#r "nuget: Polars.FSharp, 1.0.1"
#r "nuget: Polars.NET.Native.linux-x64, 1.0.1"
#r "nuget: Polars.NET.Native.win-x64, 1.0.1"
#r "nuget: Polars.NET.Native.osx-arm64, 1.0.1"
#load "TpchData.fsx"

open Polars.FSharp
open Polars.FSharp.Query
open System.Linq
open TpchData

#time "on" 

let lineitemQuery = TpchData.LineItem
let partQuery = TpchData.Part

let queryQ17 (brand: string) (container: string) =
    let filteredParts =
        query {
            for p in partQuery do
            where (p.p_brand = brand && p.p_container = container)
        }

    query {
        for l in lineitemQuery do
        where (
            l.l_quantity < 0.2m * lineitemQuery
                                    .Where(fun l2 -> l2.l_partkey = l.l_partkey)
                                    .Average(fun l2 -> l2.l_quantity)
        )
        join p in filteredParts on (l.l_partkey = p.p_partkey)
        groupValBy l 1 into g
        select
            {|
                avg_yearly = double (g.Sum(fun x -> x.l_extendedprice)) / 7.0
            |}
    }

// 1. Standard TPC-H Spec Parameters: Brand#23, MED BOX (Matches 0 parts in SF=0.01)
let standardQuery = queryQ17 "Brand#23" "MED BOX"

standardQuery.ToDataFrame().Show()

// 2. Active Dataset Case on SF=0.01: Brand#23, MED DRUM (Has 11 matching lineitems)
let activeQuery = queryQ17 "Brand#23" "MED DRUM"

activeQuery |> (Query.toDataFrame >> DataFrame.show)

activeQuery.Explain() |> printfn "%s"

(*
* TPC-H Query 17: Small-Quantity-Order Revenue Query
*
* SELECT
*     sum(l_extendedprice) / 7.0 AS avg_yearly
* FROM
*     lineitem,
*     part
* WHERE
*     p_partkey = l_partkey
*     AND p_brand = 'Brand#23'
*     AND p_container = 'MED BOX'
*     AND l_quantity < (
*         SELECT
*             0.2 * avg(l_quantity)
*         FROM
*             lineitem
*         WHERE
*             l_partkey = p_partkey
*     );
*)