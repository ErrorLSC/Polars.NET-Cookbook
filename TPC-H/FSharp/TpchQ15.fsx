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

let revenue =
    query {
        for l in TpchData.LineItem do
        where (l.l_shipdate >= DateOnly(1996, 1, 1) && l.l_shipdate < DateOnly(1996, 4, 1))
        groupValBy (l.l_extendedprice * (1.0m - l.l_discount)) l.l_suppkey into g
        select
            {|
                supplier_no = g.Key
                total_revenue = g.Sum()
            |}
    }

let query =
    query {
        for s in TpchData.Supplier do
        join r in revenue on (s.s_suppkey = r.supplier_no)
        where (r.total_revenue = revenue.Select(fun x -> x.total_revenue).Max())
        sortBy s.s_suppkey
        select
            {|
                s_suppkey = s.s_suppkey
                s_name = s.s_name
                s_address = s.s_address
                s_phone = s.s_phone
                total_revenue = r.total_revenue
            |}
    }

query |> (Query.toDataFrame >> DataFrame.show)

printfn "%s" (query.Explain())

(*
* TPC-H Query 15: Top Supplier Query
*
* WITH revenue AS (
*     SELECT
*         l_suppkey AS supplier_no,
*         sum(l_extendedprice * (1 - l_discount)) AS total_revenue
*     FROM
*         lineitem
*     WHERE
*         l_shipdate >= date '1996-01-01'
*         AND l_shipdate < CAST('1996-04-01' AS date)
*     GROUP BY
*         l_suppkey
* )
* SELECT
*     s_suppkey,
*     s_name,
*     s_address,
*     s_phone,
*     total_revenue
* FROM
*     supplier,
*     revenue
* WHERE
*     s_suppkey = supplier_no
*     AND total_revenue = (
*         SELECT
*             max(total_revenue)
*         FROM
*             revenue
*     )
* ORDER BY
*     s_suppkey;
*)