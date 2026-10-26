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

let query = 
    query {
        for l in TpchData.LineItem do
        where (l.l_shipdate <= DateOnly(1998, 9, 2))
        groupBy (l.l_returnflag, l.l_linestatus) into g
        sortBy (fst g.Key)
        thenBy (snd g.Key)
        select
            {|
                l_returnflag = fst g.Key
                l_linestatus = snd g.Key
                sum_qty = g.Sum(fun x -> x.l_quantity)
                sum_base_price = g.Sum(fun x -> x.l_extendedprice)
                sum_disc_price = g.Sum(fun x -> x.l_extendedprice * (1.0m - x.l_discount))
                sum_charge = g.Sum(fun x -> x.l_extendedprice * (1.0m - x.l_discount) * (1.0m + x.l_tax))
                avg_qty = g.Average(fun x -> double x.l_quantity)
                avg_price = g.Average(fun x -> double x.l_extendedprice)
                avg_disc = g.Average(fun x -> double x.l_discount)
                count_order = g.Count()
            |}
    }

query |> (Query.toDataFrame >> DataFrame.show)

printfn "%s" (query.Explain())

#time "off"

(*
* TPC-H Query 1: Pricing Summary Report Query
*
* SELECT
*     l_returnflag,
*     l_linestatus,
*     sum(l_quantity) AS sum_qty,
*     sum(l_extendedprice) AS sum_base_price,
*     sum(l_extendedprice * (1 - l_discount)) AS sum_disc_price,
*     sum(l_extendedprice * (1 - l_discount) * (1 + l_tax)) AS sum_charge,
*     avg(l_quantity) AS avg_qty,
*     avg(l_extendedprice) AS avg_price,
*     avg(l_discount) AS avg_disc,
*     count(*) AS count_order
* FROM
*     lineitem
* WHERE
*     l_shipdate <= CAST('1998-09-02' AS date)
* GROUP BY
*     l_returnflag,
*     l_linestatus
* ORDER BY
*     l_returnflag,
*     l_linestatus;
*)