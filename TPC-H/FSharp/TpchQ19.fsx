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

let smContainers = [ "SM CASE"; "SM BOX"; "SM PACK"; "SM PKG" ]
let medContainers = [ "MED BAG"; "MED BOX"; "MED PKG"; "MED PACK" ]
let lgContainers = [ "LG CASE"; "LG BOX"; "LG PACK"; "LG PKG" ]
let shipModes = [ "AIR"; "AIR REG" ]

let query =
    query {
        for l in TpchData.LineItem do
        join p in TpchData.Part on (l.l_partkey = p.p_partkey)
        where (
            p.p_brand = "Brand#12"
            && smContainers.Contains p.p_container
            && l.l_quantity >= 1.0m && l.l_quantity <= 11.0m
            && p.p_size >= 1 && p.p_size <= 5
            && shipModes.Contains l.l_shipmode
            && l.l_shipinstruct = "DELIVER IN PERSON"
            ||
            p.p_brand = "Brand#23"
            && medContainers.Contains p.p_container
            && l.l_quantity >= 10.0m && l.l_quantity <= 20.0m
            && p.p_size >= 1 && p.p_size <= 10
            && shipModes.Contains l.l_shipmode
            && l.l_shipinstruct = "DELIVER IN PERSON"
            ||
            p.p_brand = "Brand#34"
            && lgContainers.Contains p.p_container
            && l.l_quantity >= 20.0m && l.l_quantity <= 30.0m
            && p.p_size >= 1 && p.p_size <= 15
            && shipModes.Contains l.l_shipmode
            && l.l_shipinstruct = "DELIVER IN PERSON"
        )
        groupValBy l 1 into g
        select
            {|
                revenue = g.Sum(fun x -> x.l_extendedprice * (1.0m - x.l_discount))
            |}
    }

query |> (Query.toDataFrame >> DataFrame.show)

query.Explain() |> printfn "%s"

(*
* TPC-H Q19: Discounted Revenue Query
*
* SELECT
*     sum(l_extendedprice * (1 - l_discount)) AS revenue
* FROM
*     lineitem,
*     part
* WHERE
*     (
*         p_partkey = l_partkey
*         AND p_brand = 'Brand#12'
*         AND p_container IN ('SM CASE', 'SM BOX', 'SM PACK', 'SM PKG')
*         AND l_quantity >= 1 AND l_quantity <= 1 + 10
*         AND p_size BETWEEN 1 AND 5
*         AND l_shipmode IN ('AIR', 'AIR REG')
*         AND l_shipinstruct = 'DELIVER IN PERSON'
*     )
*     OR
*     (
*         p_partkey = l_partkey
*         AND p_brand = 'Brand#23'
*         AND p_container IN ('MED BAG', 'MED BOX', 'MED PKG', 'MED PACK')
*         AND l_quantity >= 10 AND l_quantity <= 10 + 10
*         AND p_size BETWEEN 1 AND 10
*         AND l_shipmode IN ('AIR', 'AIR REG')
*         AND l_shipinstruct = 'DELIVER IN PERSON'
*     )
*     OR
*     (
*         p_partkey = l_partkey
*         AND p_brand = 'Brand#34'
*         AND p_container IN ('LG CASE', 'LG BOX', 'LG PACK', 'LG PKG')
*         AND l_quantity >= 20 AND l_quantity <= 20 + 10
*         AND p_size BETWEEN 1 AND 15
*         AND l_shipmode IN ('AIR', 'AIR REG')
*         AND l_shipinstruct = 'DELIVER IN PERSON'
*     );
*)