#r "nuget: Polars.FSharp, 1.0.1"
#r "nuget: Polars.NET.Native.linux-x64, 1.0.1"
#r "nuget: Polars.NET.Native.win-x64, 1.0.1"
#r "nuget: Polars.NET.Native.osx-arm64, 1.0.1"
#load "TpchData.fsx"

open Polars.FSharp
open Polars.FSharp.Query
open System.Linq
open System
open TpchData

#time "on" 

let supplierQuery = TpchData.Supplier
let nationQuery = TpchData.Nation
let partsuppQuery = TpchData.PartSupp
let partQuery = TpchData.Part
let lineitemQuery = TpchData.LineItem

let startDate = DateOnly(1994, 1, 1)
let endDate = DateOnly(1995, 1, 1)

let forestParts =
    query {
        for p in partQuery do
        where (p.p_name.StartsWith "forest")
        select p.p_partkey
    }

let eligiblePartSupp =
    query {
        for ps in partsuppQuery do
        where (forestParts.Contains(ps.ps_partkey))
        where (
            decimal ps.ps_availqty > 0.5m * lineitemQuery
                                                .Where(fun l ->
                                                    l.l_partkey = ps.ps_partkey
                                                    && l.l_suppkey = ps.ps_suppkey
                                                    && l.l_shipdate >= startDate
                                                    && l.l_shipdate < endDate)
                                                .Sum(fun l -> l.l_quantity)
        )
        select ps.ps_suppkey
    }

let query =
    query {
        for s in supplierQuery do
        where (eligiblePartSupp.Contains(s.s_suppkey))
        join n in nationQuery on (s.s_nationkey = n.n_nationkey)
        where (n.n_name = "CANADA")
        sortBy s.s_name
        select
            {|
                s_name = s.s_name
                s_address = s.s_address
            |}
    }

query |> (Query.toDataFrame >> DataFrame.show)

query.Explain() |> printfn "%s"

(*
* TPC-H Q20: Potential Part Promotion Query
*
* SELECT
*     s_name,
*     s_address
* FROM
*     supplier,
*     nation
* WHERE
*     s_suppkey IN (
*         SELECT
*             ps_suppkey
*         FROM
*             partsupp
*         WHERE
*             ps_partkey IN (
*                 SELECT
*                     p_partkey
*                 FROM
*                     part
*                 WHERE
*                     p_name LIKE 'forest%'
*             )
*             AND ps_availqty > (
*                 SELECT
*                     0.5 * sum(l_quantity)
*                 FROM
*                     lineitem
*                 WHERE
*                     l_partkey = ps_partkey
*                     AND l_suppkey = ps_suppkey
*                     AND l_shipdate >= CAST('1994-01-01' AS date)
*                     AND l_shipdate < CAST('1995-01-01' AS date)
*             )
*     )
*     AND s_nationkey = n_nationkey
*     AND n_name = 'CANADA'
* ORDER BY
*     s_name;
*)