#r "nuget: Polars.FSharp, 1.0.1"
#r "nuget: Polars.NET.Native.linux-x64, 1.0.1"
#r "nuget: Polars.NET.Native.win-x64, 1.0.1"
#r "nuget: Polars.NET.Native.osx-arm64, 1.0.1"
#load "TpchData.fsx"

open Polars.FSharp
open Polars.FSharp.Query
open System.Linq
open TpchData
open System.Text.RegularExpressions

#time "on" 

let partsuppQuery = TpchData.PartSupp
let partQuery = TpchData.Part
let supplierQuery = TpchData.Supplier

let targetSizes = [49; 14; 23; 45; 19; 3; 36; 9]
let complaintsRegex = Regex("Customer.*Complaints", RegexOptions.Compiled)

let complaintsSuppliers =
    query {
        for s in supplierQuery do
        where (complaintsRegex.IsMatch s.s_comment)
        select s.s_suppkey
    }

let filteredParts =
    query {
        for p in partQuery do
        where (p.p_brand <> "Brand#45"
                && not (p.p_type.StartsWith "MEDIUM POLISHED")
                && targetSizes.Contains p.p_size)
    }

let query =
    query {
        for ps in partsuppQuery do
        where (not (complaintsSuppliers.Contains ps.ps_suppkey))
        join p in filteredParts on (ps.ps_partkey = p.p_partkey)
        groupValBy ps.ps_suppkey
            {|
                p_brand = p.p_brand
                p_type = p.p_type
                p_size = p.p_size
            |} into g
        sortByDescending (g.Distinct().Count())
        thenBy g.Key.p_brand
        thenBy g.Key.p_type
        thenBy g.Key.p_size
        select
            {|
                p_brand = g.Key.p_brand
                p_type = g.Key.p_type
                p_size = g.Key.p_size
                supplier_cnt = g.Distinct().Count()
            |}
    }

query |> (Query.toDataFrame >> DataFrame.show)

printfn "%s" (query.Explain())

(*
* TPC-H Query 16: Parts/Supplier Relationship Query
*
* SELECT
*     p_brand,
*     p_type,
*     p_size,
*     count(DISTINCT ps_suppkey) AS supplier_cnt
* FROM
*     partsupp,
*     part
* WHERE
*     p_partkey = ps_partkey
*     AND p_brand <> 'Brand#45'
*     AND p_type NOT LIKE 'MEDIUM POLISHED%'
*     AND p_size IN (49, 14, 23, 45, 19, 3, 36, 9)
*     AND ps_suppkey NOT IN (
*         SELECT
*             s_suppkey
*         FROM
*             supplier
*         WHERE
*             s_comment LIKE '%Customer%Complaints%'
*     )
* GROUP BY
*     p_brand,
*     p_type,
*     p_size
* ORDER BY
*     supplier_cnt DESC,
*     p_brand,
*     p_type,
*     p_size;
*)