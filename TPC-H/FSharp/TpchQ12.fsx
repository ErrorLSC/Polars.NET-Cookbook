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

let startDate = DateOnly(1994, 1, 1)
let endDate = DateOnly(1995, 1, 1)

let query =
    query {
        for l in TpchData.LineItem do
        where (
            (l.l_shipmode = "MAIL" || l.l_shipmode = "SHIP")
            && l.l_commitdate < l.l_receiptdate
            && l.l_shipdate < l.l_commitdate
            && l.l_receiptdate >= startDate
            && l.l_receiptdate < endDate
        )
        join o in TpchData.Orders on (l.l_orderkey = o.o_orderkey)
        groupValBy o l.l_shipmode into g
        sortBy g.Key
        select
            {|
                l_shipmode = g.Key
                high_line_count = g.Count(fun x -> x.o_orderpriority = "1-URGENT" || x.o_orderpriority = "2-HIGH")
                low_line_count = g.Count(fun x -> x.o_orderpriority <> "1-URGENT" && x.o_orderpriority <> "2-HIGH")
            |}
    }

query |> (Query.toDataFrame >> DataFrame.show)

printfn "%s" (query.Explain())

(*
* TPC-H Query 12: Shipping Modes and Order Priority Query
*
* SELECT
*     l_shipmode,
*     sum(
*         CASE WHEN o_orderpriority = '1-URGENT'
*             OR o_orderpriority = '2-HIGH' THEN
*             1
*         ELSE
*             0
*         END) AS high_line_count,
*     sum(
*         CASE WHEN o_orderpriority <> '1-URGENT'
*             AND o_orderpriority <> '2-HIGH' THEN
*             1
*         ELSE
*             0
*         END) AS low_line_count
* FROM
*     orders,
*     lineitem
* WHERE
*     o_orderkey = l_orderkey
*     AND l_shipmode IN ('MAIL', 'SHIP')
*     AND l_commitdate < l_receiptdate
*     AND l_shipdate < l_commitdate
*     AND l_receiptdate >= CAST('1994-01-01' AS date)
*     AND l_receiptdate < CAST('1995-01-01' AS date)
* GROUP BY
*     l_shipmode
* ORDER BY
*     l_shipmode;
*)