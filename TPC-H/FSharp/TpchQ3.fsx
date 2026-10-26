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


let customerQuery = TpchData.Customer
let ordersQuery = TpchData.Orders
let lineitemQuery = TpchData.LineItem

let cutoff = DateOnly(1995, 3, 15)
let baseQuery =
    query {
        for c in customerQuery do
        where (c.c_mktsegment = "BUILDING")
        join o in ordersQuery on (c.c_custkey = o.o_custkey)
        where (o.o_orderdate < cutoff)
        join l in lineitemQuery on (o.o_orderkey = l.l_orderkey)
        where (l.l_shipdate > cutoff)
        groupValBy l {| l_orderkey = l.l_orderkey; o_orderdate = o.o_orderdate; o_shippriority = o.o_shippriority |} into g
        select
            {|
                l_orderkey = g.Key.l_orderkey
                revenue = g.Sum(fun x -> x.l_extendedprice * (1.0m - x.l_discount))
                o_orderdate = g.Key.o_orderdate
                o_shippriority = g.Key.o_shippriority
            |}
    }

let query = 
    query {
        for x in baseQuery do
        sortByDescending x.revenue
        thenBy x.o_orderdate
        take 10
    }
    
query |> (Query.toDataFrame >> DataFrame.show)

printfn "%s" (query.Explain())

#time "off" 

(*
    * TPC-H Query 3: Shipping Priority Query
    *
    * SELECT
    *     l_orderkey,
    *     sum(l_extendedprice * (1 - l_discount)) AS revenue,
    *     o_orderdate,
    *     o_shippriority
    * FROM
    *     customer,
    *     orders,
    *     lineitem
    * WHERE
    *     c_mktsegment = 'BUILDING'
    *     AND c_custkey = o_custkey
    *     AND l_orderkey = o_orderkey
    *     AND o_orderdate < CAST('1995-03-15' AS date)
    *     AND l_shipdate > CAST('1995-03-15' AS date)
    * GROUP BY
    *     l_orderkey,
    *     o_orderdate,
    *     o_shippriority
    * ORDER BY
    *     revenue DESC,
    *     o_orderdate
    * LIMIT 10;
    *)