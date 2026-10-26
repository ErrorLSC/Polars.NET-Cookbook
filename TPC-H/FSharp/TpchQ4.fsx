#r "nuget: Polars.FSharp, 1.0.1"
#r "nuget: Polars.NET.Native.linux-x64, 1.0.1"
#r "nuget: Polars.NET.Native.win-x64, 1.0.1"
#r "nuget: Polars.NET.Native.osx-arm64, 1.0.1"
#load "TpchData.fsx"

open Polars.FSharp
open Polars.FSharp.Query
open System
open System.Linq
open TpchData

#time "on" 

let query = 
    query {
        for o in TpchData.Orders do
        where (o.o_orderdate >= DateOnly(1993, 7, 1) && o.o_orderdate < DateOnly(1993, 10, 1)
                && query {
                    for l in TpchData.LineItem do
                    exists (l.l_orderkey = o.o_orderkey && l.l_commitdate < l.l_receiptdate)
                })
        groupBy o.o_orderpriority into g
        sortBy g.Key
        select
            {|
                o_orderpriority = g.Key
                order_count = g.Count()
            |}
    }
    
query |> (Query.toDataFrame >> DataFrame.show)

printfn "%s" (query.Explain())

#time "off" 
(*
* TPC-H Query 4: Order Priority Checking Query
*
* SELECT
*     o_orderpriority,
*     count(*) AS order_count
* FROM
*     orders
* WHERE
*     o_orderdate >= CAST('1993-07-01' AS date)
*     AND o_orderdate < CAST('1993-10-01' AS date)
*     AND EXISTS (
*         SELECT
*             *
*         FROM
*             lineitem
*         WHERE
*             l_orderkey = o_orderkey
*             AND l_commitdate < l_receiptdate
*     )
* GROUP BY
*     o_orderpriority
* ORDER BY
*     o_orderpriority;
*)