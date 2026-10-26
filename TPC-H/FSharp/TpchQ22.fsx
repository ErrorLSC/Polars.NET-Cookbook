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

let countryCodes = [| "13"; "31"; "23"; "29"; "30"; "18"; "17" |]

let query =
    query {
        for c in TpchData.Customer do
        where (
            countryCodes.Contains(c.c_phone.Substring(0, 2))
            && c.c_acctbal > TpchData.Customer
                                .Where(fun c2 ->
                                    c2.c_acctbal > 0.0m
                                    && countryCodes.Contains(c2.c_phone.Substring(0, 2)))
                                .Average(fun c2 -> c2.c_acctbal)
            && not (TpchData.Orders.Any(fun o -> o.o_custkey = c.c_custkey))
        )
        groupBy (c.c_phone.Substring(0, 2)) into g
        sortBy g.Key
        select
            {|
                cntrycode = g.Key
                numcust = g.Count()
                totacctbal = g.Sum(fun x -> x.c_acctbal)
            |}
    }

query |> (Query.toDataFrame >> DataFrame.show)

query.Explain() |> printfn "%s"

(*
* TPC-H Q22: Global Sales Opportunity Query
*
* SELECT
*     cntrycode,
*     count(*) AS numcust,
*     sum(c_acctbal) AS totacctbal
* FROM
*     (
*         SELECT
*             substring(c_phone FROM 1 FOR 2) AS cntrycode,
*             c_acctbal
*         FROM
*             customer
*         WHERE
*             substring(c_phone FROM 1 FOR 2) IN
*                 ('13', '31', '23', '29', '30', '18', '17')
*             AND c_acctbal > (
*                 SELECT
*                     avg(c_acctbal)
*                 FROM
*                     customer
*                 WHERE
*                     c_acctbal > 0.00
*                     AND substring(c_phone FROM 1 FOR 2) IN
*                         ('13', '31', '23', '29', '30', '18', '17')
*             )
*             AND NOT EXISTS (
*                 SELECT
*                     *
*                 FROM
*                     orders
*                 WHERE
*                     o_custkey = c_custkey
*             )
*     ) AS custsale
* GROUP BY
*     cntrycode
* ORDER BY
*     cntrycode;
*)