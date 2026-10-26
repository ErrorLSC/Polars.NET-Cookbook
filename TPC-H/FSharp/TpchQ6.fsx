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
        where (l.l_shipdate >= DateOnly(1994, 1, 1) && l.l_shipdate < DateOnly(1995, 1, 1)
                && l.l_discount >= 0.05m && l.l_discount <= 0.07m
                && l.l_quantity < 24m)
        groupBy 1 into g
        select
            {|
                revenue = g.Sum(fun x -> x.l_extendedprice * x.l_discount)
            |}
    }

query |> (Query.toDataFrame >> DataFrame.show)

printfn "%s" (query.Explain())

(*
* TPC-H Query 6: Forecasting Revenue Change Query
*
* SELECT
*     sum(l_extendedprice * l_discount) AS revenue
* FROM
*     lineitem
* WHERE
*     l_shipdate >= CAST('1994-01-01' AS date)
*     AND l_shipdate < CAST('1995-01-01' AS date)
*     AND l_discount BETWEEN 0.05 AND 0.07
*     AND l_quantity < 24;
*)