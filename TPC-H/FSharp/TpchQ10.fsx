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

let customerQuery = TpchData.Customer
let ordersQuery = TpchData.Orders
let lineitemQuery = TpchData.LineItem
let nationQuery = TpchData.Nation

let startDate = DateOnly(1993, 10, 1)
let endDate = DateOnly(1994, 1, 1)

let baseQuery =
    query {
        for c in customerQuery do
        join o in ordersQuery on (c.c_custkey = o.o_custkey)
        where (o.o_orderdate >= startDate && o.o_orderdate < endDate)
        join l in lineitemQuery on (o.o_orderkey = l.l_orderkey)
        where (l.l_returnflag = "R")
        join n in nationQuery on (c.c_nationkey = n.n_nationkey)
        groupValBy l
            {|
                c_custkey = c.c_custkey
                c_name = c.c_name
                c_acctbal = c.c_acctbal
                n_name = n.n_name
                c_address = c.c_address
                c_phone = c.c_phone
                c_comment = c.c_comment
            |} into g
        select
            {|
                c_custkey = g.Key.c_custkey
                c_name = g.Key.c_name
                revenue = g.Sum(fun x -> x.l_extendedprice * (1.0m - x.l_discount))
                c_acctbal = g.Key.c_acctbal
                n_name = g.Key.n_name
                c_address = g.Key.c_address
                c_phone = g.Key.c_phone
                c_comment = g.Key.c_comment
            |}
    }

let query =
    query {
        for x in baseQuery do
        sortByDescending x.revenue
        take 20
    }

query |> (Query.toDataFrame >> DataFrame.show)

printfn "%s" (query.Explain())

(*
* TPC-H Query 10: Returned Item Reporting Query
*
* SELECT
*     c_custkey,
*     c_name,
*     sum(l_extendedprice * (1 - l_discount)) AS revenue,
*     c_acctbal,
*     n_name,
*     c_address,
*     c_phone,
*     c_comment
* FROM
*     customer,
*     orders,
*     lineitem,
*     nation
* WHERE
*     c_custkey = o_custkey
*     AND l_orderkey = o_orderkey
*     AND o_orderdate >= CAST('1993-10-01' AS date)
*     AND o_orderdate < CAST('1994-01-01' AS date)
*     AND l_returnflag = 'R'
*     AND c_nationkey = n_nationkey
* GROUP BY
*     c_custkey,
*     c_name,
*     c_acctbal,
*     c_phone,
*     n_name,
*     c_address,
*     c_comment
* ORDER BY
*     revenue DESC
* LIMIT 20;
*)