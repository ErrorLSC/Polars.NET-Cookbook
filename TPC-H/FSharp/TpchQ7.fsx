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


let supplierQuery = TpchData.Supplier
let lineitemQuery = TpchData.LineItem
let ordersQuery = TpchData.Orders
let customerQuery = TpchData.Customer
let nationQuery = TpchData.Nation

let startDate = DateOnly(1995, 1, 1)
let endDate = DateOnly(1996, 12, 31)

let n1Query = nationQuery.Select(fun n -> {| supp_nationkey = n.n_nationkey; supp_nation = n.n_name |})
let n2Query = nationQuery.Select(fun n -> {| cust_nationkey = n.n_nationkey; cust_nation = n.n_name |})

let query =
    query {
        for s in supplierQuery do
        join n1 in n1Query on (s.s_nationkey = n1.supp_nationkey)
        join l in lineitemQuery on (s.s_suppkey = l.l_suppkey)
        where (l.l_shipdate >= startDate && l.l_shipdate <= endDate)
        join o in ordersQuery on (l.l_orderkey = o.o_orderkey)
        join c in customerQuery on (o.o_custkey = c.c_custkey)
        join n2 in n2Query on (c.c_nationkey = n2.cust_nationkey)
        where (n1.supp_nation = "FRANCE" && n2.cust_nation = "GERMANY"
                || n1.supp_nation = "GERMANY" && n2.cust_nation = "FRANCE")
        groupValBy l {| supp_nation = n1.supp_nation; cust_nation = n2.cust_nation; l_year = l.l_shipdate.Year |} into g
        sortBy g.Key.supp_nation
        thenBy g.Key.cust_nation
        thenBy g.Key.l_year
        select
            {|
                supp_nation = g.Key.supp_nation
                cust_nation = g.Key.cust_nation
                l_year = g.Key.l_year
                revenue = g.Sum(fun x -> x.l_extendedprice * (1.0m - x.l_discount))
            |}
    }

query |> (Query.toDataFrame >> DataFrame.show)

printfn "%s" (query.Explain())

(*
* TPC-H Query 7: Volume Shipping Query
*
* SELECT
*     supp_nation,
*     cust_nation,
*     l_year,
*     sum(volume) AS revenue
* FROM (
*     SELECT
*         n1.n_name AS supp_nation,
*         n2.n_name AS cust_nation,
*         extract(year FROM l_shipdate) AS l_year,
*         l_extendedprice * (1 - l_discount) AS volume
*     FROM
*         supplier,
*         lineitem,
*         orders,
*         customer,
*         nation n1,
*         nation n2
*     WHERE
*         s_suppkey = l_suppkey
*         AND o_orderkey = l_orderkey
*         AND c_custkey = o_custkey
*         AND s_nationkey = n1.n_nationkey
*         AND c_nationkey = n2.n_nationkey
*         AND (
*             (n1.n_name = 'FRANCE' AND n2.n_name = 'GERMANY')
*             OR (n1.n_name = 'GERMANY' AND n2.n_name = 'FRANCE')
*         )
*         AND l_shipdate BETWEEN CAST('1995-01-01' AS date) AND CAST('1996-12-31' AS date)
* ) AS shipping
* GROUP BY
*     supp_nation,
*     cust_nation,
*     l_year
* ORDER BY
*     supp_nation,
*     cust_nation,
*     l_year;
*)