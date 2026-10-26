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

let customerQuery = TpchData.Customer
let ordersQuery = TpchData.Orders
let lineitemQuery = TpchData.LineItem

let bigOrders =
    query {
        for l in lineitemQuery do
        groupValBy l.l_quantity l.l_orderkey into g
        where (g.Sum() > 300.0m)
        select g.Key
    }

let filteredOrders =
    query {
        for o in ordersQuery do
        where (bigOrders.Contains(o.o_orderkey))
        select o
    }

let query =
    query {
        for c in customerQuery do
        join o in filteredOrders on (c.c_custkey = o.o_custkey)
        join l in lineitemQuery on (o.o_orderkey = l.l_orderkey)
        groupValBy l
            {|
                c_name = c.c_name
                c_custkey = c.c_custkey
                o_orderkey = o.o_orderkey
                o_orderdate = o.o_orderdate
                o_totalprice = o.o_totalprice
            |} into g
        sortByDescending g.Key.o_totalprice
        thenBy g.Key.o_orderdate
        select
            {|
                c_name = g.Key.c_name
                c_custkey = g.Key.c_custkey
                o_orderkey = g.Key.o_orderkey
                o_orderdate = g.Key.o_orderdate
                o_totalprice = g.Key.o_totalprice
                sum_qty = g.Sum(fun x -> x.l_quantity)
            |}
        take 100
    }

query |> (Query.toDataFrame >> DataFrame.show)

query.Explain() |> printfn "%s"

(*
* TPC-H Query 18: Large Volume Customer Query
*
* SELECT
*     c_name,
*     c_custkey,
*     o_orderkey,
*     o_orderdate,
*     o_totalprice,
*     sum(l_quantity)
* FROM
*     customer,
*     orders,
*     lineitem
* WHERE
*     o_orderkey IN (
*         SELECT
*             l_orderkey
*         FROM
*             lineitem
*         GROUP BY
*             l_orderkey
*         HAVING
*             sum(l_quantity) > 300
*     )
*     AND c_custkey = o_custkey
*     AND o_orderkey = l_orderkey
* GROUP BY
*     c_name,
*     c_custkey,
*     o_orderkey,
*     o_orderdate,
*     o_totalprice
* ORDER BY
*     o_totalprice DESC,
*     o_orderdate
* LIMIT 100;
*)