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

let partQuery = TpchData.Part
let supplierQuery = TpchData.Supplier
let partsuppQuery = TpchData.PartSupp
let nationQuery =  TpchData.Nation
let regionQuery = TpchData.Region

let minCostQuery =
    query {
        for ps in partsuppQuery do
        join s in supplierQuery on (ps.ps_suppkey = s.s_suppkey)
        join n in nationQuery on (s.s_nationkey = n.n_nationkey)
        join r in regionQuery on (n.n_regionkey = r.r_regionkey)
        where (r.r_name = "EUROPE")
        groupValBy ps ps.ps_partkey into g
        select
            {|
                ps_partkey = g.Key
                min_cost = g.Min(fun (x: TpchData.PartSupp) -> x.ps_supplycost)
            |}
    }

let query =
    query {
        for p in partQuery do
        where (p.p_size = 15 && p.p_type.EndsWith "BRASS")
        join ps in partsuppQuery on (p.p_partkey = ps.ps_partkey)
        join mc in minCostQuery on ((ps.ps_partkey, ps.ps_supplycost) = (mc.ps_partkey, mc.min_cost))
        join s in supplierQuery on (ps.ps_suppkey = s.s_suppkey)
        join n in nationQuery on (s.s_nationkey = n.n_nationkey)
        join r in regionQuery on (n.n_regionkey = r.r_regionkey)
        where (r.r_name = "EUROPE")
        sortByDescending s.s_acctbal
        thenBy n.n_name
        thenBy s.s_name
        thenBy p.p_partkey
        take 100
        select
            {|
                s_acctbal = s.s_acctbal
                s_name = s.s_name
                n_name = n.n_name
                p_partkey = p.p_partkey
                p_mfgr = p.p_mfgr
                s_address = s.s_address
                s_phone = s.s_phone
                s_comment = s.s_comment
            |}
    }
    
query |> (Query.toDataFrame >> DataFrame.show)

printfn "%s" (query.Explain())

#time "off" 

(*
    * TPC-H Query 2: Minimum Cost Supplier Query
    *
    * SELECT
    *     s_acctbal,
    *     s_name,
    *     n_name,
    *     p_partkey,
    *     p_mfgr,
    *     s_address,
    *     s_phone,
    *     s_comment
    * FROM
    *     part,
    *     supplier,
    *     partsupp,
    *     nation,
    *     region
    * WHERE
    *     p_partkey = ps_partkey
    *     AND s_suppkey = ps_suppkey
    *     AND p_size = 15
    *     AND p_type LIKE '%BRASS'
    *     AND s_nationkey = n_nationkey
    *     AND n_regionkey = r_regionkey
    *     AND r_name = 'EUROPE'
    *     AND ps_supplycost = (
    *         SELECT
    *             min(ps_supplycost)
    *         FROM
    *             partsupp,
    *             supplier,
    *             nation,
    *             region
    *         WHERE
    *             p_partkey = ps_partkey
    *             AND s_suppkey = ps_suppkey
    *             AND s_nationkey = n_nationkey
    *             AND n_regionkey = r_regionkey
    *             AND r_name = 'EUROPE'
    *     )
    * ORDER BY
    *     s_acctbal DESC,
    *     n_name,
    *     s_name,
    *     p_partkey
    * LIMIT 100;
    *)