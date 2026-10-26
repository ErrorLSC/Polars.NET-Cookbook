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

let partQuery = TpchData.Part
let supplierQuery = TpchData.Supplier
let lineitemQuery = TpchData.LineItem
let ordersQuery = TpchData.Orders
let customerQuery = TpchData.Customer
let nationQuery = TpchData.Nation
let regionQuery = TpchData.Region

let startDate = DateOnly(1995, 1, 1)
let endDate = DateOnly(1996, 12, 31)

let n2Query = nationQuery.Select(fun n -> {| supp_nationkey = n.n_nationkey; supp_nation = n.n_name |})

let subQuery =
    query {
        for p in partQuery do
        where (p.p_type = "ECONOMY ANODIZED STEEL")
        join l in lineitemQuery on (p.p_partkey = l.l_partkey)
        join s in supplierQuery on (l.l_suppkey = s.s_suppkey)
        join n2 in n2Query on (s.s_nationkey = n2.supp_nationkey)
        join o in ordersQuery on (l.l_orderkey = o.o_orderkey)
        where (o.o_orderdate >= startDate && o.o_orderdate <= endDate)
        join c in customerQuery on (o.o_custkey = c.c_custkey)
        join n1 in nationQuery on (c.c_nationkey = n1.n_nationkey)
        join r in regionQuery on (n1.n_regionkey = r.r_regionkey)
        where (r.r_name = "AMERICA")
        select
            {|
                o_year = o.o_orderdate.Year
                volume = l.l_extendedprice * (1.0m - l.l_discount)
                nation = n2.supp_nation
            |}
    }

let baseQuery =
    query {
        for a in subQuery do
        groupBy a.o_year into g
        sortBy g.Key
        select
            {|
                o_year = g.Key
                brazil_volume = g.Sum(fun x -> if x.nation = "BRAZIL" then x.volume else 0.0m)
                total_volume = g.Sum(fun x -> x.volume)
            |}
    }

let query =
    query {
        for x in baseQuery do
        select
            {|
                o_year = x.o_year
                mkt_share = double (x.brazil_volume / x.total_volume)
            |}
    }

query |> (Query.toDataFrame >> DataFrame.show)

printfn "%s" (query.Explain())
(*
* TPC-H Query 8: National Market Share Query
*
* SELECT
*     o_year,
*     sum(CASE
*         WHEN nation = 'BRAZIL' THEN volume
*         ELSE 0
*     END) / sum(volume) AS mkt_share
* FROM (
*     SELECT
*         extract(year FROM o_orderdate) AS o_year,
*         l_extendedprice * (1 - l_discount) AS volume,
*         n2.n_name AS nation
*     FROM
*         part,
*         supplier,
*         lineitem,
*         orders,
*         customer,
*         nation n1,
*         nation n2,
*         region
*     WHERE
*         p_partkey = l_partkey
*         AND s_suppkey = l_suppkey
*         AND l_orderkey = o_orderkey
*         AND o_custkey = c_custkey
*         AND c_nationkey = n1.n_nationkey
*         AND n1.n_regionkey = r_regionkey
*         AND r_name = 'AMERICA'
*         AND s_nationkey = n2.n_nationkey
*         AND o_orderdate BETWEEN CAST('1995-01-01' AS date) AND CAST('1996-12-31' AS date)
*         AND p_type = 'ECONOMY ANODIZED STEEL'
* ) AS all_nations
* GROUP BY
*     o_year
* ORDER BY
*     o_year;
*)