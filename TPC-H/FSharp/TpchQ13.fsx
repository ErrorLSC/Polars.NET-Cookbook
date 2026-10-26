#r "nuget: Polars.FSharp, 1.0.1"
#r "nuget: Polars.NET.Native.linux-x64, 1.0.1"
#r "nuget: Polars.NET.Native.win-x64, 1.0.1"
#r "nuget: Polars.NET.Native.osx-arm64, 1.0.1"
#load "TpchData.fsx"

open Polars.FSharp
open Polars.FSharp.Query
open System.Linq
open TpchData
open System.Text.RegularExpressions

#time "on" 

let specialRequestsRegex = Regex("special.*requests", RegexOptions.Compiled)

let customerOrders =
    query {
        for c in TpchData.Customer do
        groupJoin o in TpchData.Orders.Where(fun o -> not (specialRequestsRegex.IsMatch o.o_comment))
            on (c.c_custkey = o.o_custkey) into g
        select
            {|
                c_custkey = c.c_custkey
                c_count = g.Count()
            |}
    }

let query =
    query {
        for co in customerOrders do
        groupBy co.c_count into g
        sortByDescending (g.Count())
        thenByDescending g.Key
        select
            {|
                c_count = g.Key
                custdist = g.Count()
            |}
    }

query |> (Query.toDataFrame >> DataFrame.show)

printfn "%s" (query.Explain())

(*
* TPC-H Query 13: Customer Distribution Query
*
* SELECT
*     c_count,
*     count(*) AS custdist
* FROM (
*     SELECT
*         c_custkey,
*         count(o_orderkey) AS c_count
*     FROM
*         customer
*     LEFT OUTER JOIN orders ON c_custkey = o_custkey
*         AND o_comment NOT LIKE '%special%requests%'
*     GROUP BY
*         c_custkey
* ) AS c_orders (c_custkey, c_count)
* GROUP BY
*     c_count
* ORDER BY
*     custdist DESC,
*     c_count DESC;
*)