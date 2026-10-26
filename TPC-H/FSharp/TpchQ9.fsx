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

let subQuery =
    query {
        for p in TpchData.Part do
        where (p.p_name.Contains "green")
        join l in TpchData.LineItem on (p.p_partkey = l.l_partkey)
        join ps in TpchData.PartSupp on ((l.l_partkey, l.l_suppkey) = (ps.ps_partkey, ps.ps_suppkey))
        join s in TpchData.Supplier on (l.l_suppkey = s.s_suppkey)
        join o in TpchData.Orders on (l.l_orderkey = o.o_orderkey)
        join n in TpchData.Nation on (s.s_nationkey = n.n_nationkey)
        select
            {|
                nation = n.n_name
                o_year = o.o_orderdate.Year
                amount = l.l_extendedprice * (1.0m - l.l_discount) - ps.ps_supplycost * l.l_quantity
            |}
    }

let query =
    query {
        for pr in subQuery do
        groupBy (pr.nation, pr.o_year) into g
        sortBy (fst g.Key)
        thenByDescending (snd g.Key)
        select
            {|
                nation = fst g.Key
                o_year = snd g.Key
                sum_profit = g.Sum(fun x -> x.amount)
            |}
    }

query |> (Query.toDataFrame >> DataFrame.show)

printfn "%s" (query.Explain())
(*
* TPC-H Query 9: Product Type Profit Measure Query
*
* SELECT
*     nation,
*     o_year,
*     sum(amount) AS sum_profit
* FROM (
*     SELECT
*         n_name AS nation,
*         extract(year FROM o_orderdate) AS o_year,
*         l_extendedprice * (1 - l_discount) - ps_supplycost * l_quantity AS amount
*     FROM
*         part,
*         supplier,
*         lineitem,
*         partsupp,
*         orders,
*         nation
*     WHERE
*         s_suppkey = l_suppkey
*         AND ps_suppkey = l_suppkey
*         AND ps_partkey = l_partkey
*         AND p_partkey = l_partkey
*         AND o_orderkey = l_orderkey
*         AND s_nationkey = n_nationkey
*         AND p_name LIKE '%green%'
* ) AS profit
* GROUP BY
*     nation,
*     o_year
* ORDER BY
*     nation,
*     o_year DESC;
*)