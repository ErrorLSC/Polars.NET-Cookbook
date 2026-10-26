#r "nuget: Polars.FSharp, 1.0.1"
#r "nuget: Polars.NET.Native.linux-x64, 1.0.1"
#r "nuget: Polars.NET.Native.win-x64, 1.0.1"
#r "nuget: Polars.NET.Native.osx-arm64, 1.0.1"
#load "TpchData.fsx"

open Polars.FSharp
open Polars.FSharp.Query
open System.Linq
open TpchData
open System

#time "on" 

let lineitemQuery = TpchData.LineItem
let partQuery = TpchData.Part

let startDate = DateOnly(1995, 9, 1)
let endDate = DateOnly(1995, 10, 1)

let query =
    query {
        for l in lineitemQuery do
        where (l.l_shipdate >= startDate && l.l_shipdate < endDate)
        join p in partQuery on (l.l_partkey = p.p_partkey)
        groupValBy {| l = l; p = p |} 1 into g
        select
            {|
                promo_revenue =
                    100.0 * double (g.Sum(fun x -> if x.p.p_type.StartsWith "PROMO" then x.l.l_extendedprice * (1.0m - x.l.l_discount) else 0.0m))
                    / double (g.Sum(fun x -> x.l.l_extendedprice * (1.0m - x.l.l_discount)))
            |}
    }

query |> (Query.toDataFrame >> DataFrame.show)

printfn "%s" (query.Explain())

(*
* TPC-H Query 14: Promotion Effect Query
*
* SELECT
*     100.00 * sum(
*         CASE WHEN p_type LIKE 'PROMO%' THEN
*             l_extendedprice * (1 - l_discount)
*         ELSE
*             0
*         END) / sum(l_extendedprice * (1 - l_discount)) AS promo_revenue
* FROM
*     lineitem,
*     part
* WHERE
*     l_partkey = p_partkey
*     AND l_shipdate >= date '1995-09-01'
*     AND l_shipdate < CAST('1995-10-01' AS date);
*)