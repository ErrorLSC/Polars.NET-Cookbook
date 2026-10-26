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
let supplierQuery = TpchData.Supplier
let nationQuery = TpchData.Nation
let regionQuery = TpchData.Region

let query =
    query {
        for c in customerQuery do
        join o in ordersQuery on (c.c_custkey = o.o_custkey)
        where (o.o_orderdate >= DateOnly(1994, 1, 1) && o.o_orderdate < DateOnly(1995, 1, 1))
        join l in lineitemQuery on (o.o_orderkey = l.l_orderkey)
        join s in supplierQuery on ((l.l_suppkey, c.c_nationkey) = (s.s_suppkey, s.s_nationkey))
        join n in nationQuery on (s.s_nationkey = n.n_nationkey)
        join r in regionQuery on (n.n_regionkey = r.r_regionkey)
        where (r.r_name = "ASIA")
        groupValBy l n.n_name into g
        sortByDescending (g.Sum(fun x -> x.l_extendedprice * (1.0m - x.l_discount)))
        select
            {|
                n_name = g.Key
                revenue = g.Sum(fun x -> x.l_extendedprice * (1.0m - x.l_discount))
            |}
    }

query |> (Query.toDataFrame >> DataFrame.show)

printfn "%s" (query.Explain())

(*
* TPC-H Query 5: Local Supplier Volume Query
*
* SELECT
*     n_name,
*     sum(l_extendedprice * (1 - l_discount)) AS revenue
* FROM
*     customer,
*     orders,
*     lineitem,
*     supplier,
*     nation,
*     region
* WHERE
*     c_custkey = o_custkey
*     AND l_orderkey = o_orderkey
*     AND l_suppkey = s_suppkey
*     AND c_nationkey = s_nationkey
*     AND s_nationkey = n_nationkey
*     AND n_regionkey = r_regionkey
*     AND r_name = 'ASIA'
*     AND o_orderdate >= CAST('1994-01-01' AS date)
*     AND o_orderdate < CAST('1995-01-01' AS date)
* GROUP BY
*     n_name
* ORDER BY
*     revenue DESC;
*)