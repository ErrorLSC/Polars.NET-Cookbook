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

let supplierQuery = TpchData.Supplier
let lineitemQuery = TpchData.LineItem
let ordersQuery = TpchData.Orders
let nationQuery = TpchData.Nation

// 1. Orders with more than one supplier
let multiSuppOrders =
    query {
        for l in lineitemQuery do
        groupValBy l.l_suppkey l.l_orderkey into g
        where (g.Distinct().Count() > 1)
        select g.Key
    }

// 2. Orders where exactly one supplier was late
let singleLateSuppOrders =
    query {
        for l in lineitemQuery do
        where (l.l_receiptdate > l.l_commitdate)
        groupValBy l.l_suppkey l.l_orderkey into g
        where (g.Distinct().Count() = 1)
        select
            {|
                l_orderkey = g.Key
                l_suppkey = g.Max()
            |}
    }

let query =
    query {
        for l1 in lineitemQuery do
        where (l1.l_receiptdate > l1.l_commitdate)
        where (multiSuppOrders.Contains l1.l_orderkey)
        join slo in singleLateSuppOrders on (l1.l_orderkey = slo.l_orderkey)
        where (l1.l_suppkey = slo.l_suppkey)
        join o in ordersQuery on (l1.l_orderkey = o.o_orderkey)
        where (o.o_orderstatus = "F")
        join s in supplierQuery on (l1.l_suppkey = s.s_suppkey)
        join n in nationQuery on (s.s_nationkey = n.n_nationkey)
        where (n.n_name = "SAUDI ARABIA")
        groupBy s.s_name into g
        sortByDescending (g.Count())
        thenBy g.Key
        select
            {|
                s_name = g.Key
                numwait = g.Count()
            |}
        take 100
    }

query |> (Query.toDataFrame >> DataFrame.show)

query.Explain() |> printfn "%s"

(*
* TPC-H Q21: Suppliers Who Kept Orders Waiting Query
*
* SELECT
*     s_name,
*     count(*) AS numwait
* FROM
*     supplier,
*     lineitem l1,
*     orders,
*     nation
* WHERE
*     s_suppkey = l1.l_suppkey
*     AND o_orderkey = l1.l_orderkey
*     AND o_orderstatus = 'F'
*     AND l1.l_receiptdate > l1.l_commitdate
*     AND EXISTS (
*         SELECT
*             *
*         FROM
*             lineitem l2
*         WHERE
*             l2.l_orderkey = l1.l_orderkey
*             AND l2.l_suppkey <> l1.l_suppkey
*     )
*     AND NOT EXISTS (
*         SELECT
*             *
*         FROM
*             lineitem l3
*         WHERE
*             l3.l_orderkey = l1.l_orderkey
*             AND l3.l_suppkey <> l1.l_suppkey
*             AND l3.l_receiptdate > l3.l_commitdate
*     )
*     AND s_nationkey = n_nationkey
*     AND n_name = 'SAUDI ARABIA'
* GROUP BY
*     s_name
* ORDER BY
*     numwait DESC,
*     s_name
* LIMIT 100;
*)