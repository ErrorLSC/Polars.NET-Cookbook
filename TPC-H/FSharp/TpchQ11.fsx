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

let partsuppQuery = TpchData.PartSupp
let supplierQuery = TpchData.Supplier
let nationQuery = TpchData.Nation

let totalCostQuery =
    query {
        for ps2 in partsuppQuery do
        join s2 in supplierQuery on (ps2.ps_suppkey = s2.s_suppkey)
        join n2 in nationQuery on (s2.s_nationkey = n2.n_nationkey)
        where (n2.n_name = "GERMANY")
        select (ps2.ps_supplycost * decimal ps2.ps_availqty)
    }

let threshold = totalCostQuery.Sum() * 0.0001m

let query =
    query {
        for ps in partsuppQuery do
        join s in supplierQuery on (ps.ps_suppkey = s.s_suppkey)
        join n in nationQuery on (s.s_nationkey = n.n_nationkey)
        where (n.n_name = "GERMANY")
        groupValBy (ps.ps_supplycost * decimal ps.ps_availqty) ps.ps_partkey into g
        where (g.Sum() > threshold)
        sortByDescending (g.Sum())
        select
            {|
                ps_partkey = g.Key
                value = g.Sum()
            |}
    }
    
query |> (Query.toDataFrame >> DataFrame.show)

printfn "%s" (query.Explain())
(*
* TPC-H Query 11: Important Stock Identification Query
*
* SELECT
*     ps_partkey,
*     sum(ps_supplycost * ps_availqty) AS value
* FROM
*     partsupp,
*     supplier,
*     nation
* WHERE
*     ps_suppkey = s_suppkey
*     AND s_nationkey = n_nationkey
*     AND n_name = 'GERMANY'
* GROUP BY
*     ps_partkey
* HAVING
*     sum(ps_supplycost * ps_availqty) > (
*         SELECT
*             sum(ps_supplycost * ps_availqty) * 0.0001000000
*         FROM
*             partsupp,
*             supplier,
*             nation
*         WHERE
*             ps_suppkey = s_suppkey
*             AND s_nationkey = n_nationkey
*             AND n_name = 'GERMANY'
*     )
* ORDER BY
*     value DESC;
*)