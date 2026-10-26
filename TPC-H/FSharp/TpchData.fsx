#r "nuget: Polars.FSharp, 1.0.1"
#r "nuget: Polars.NET.Native.linux-x64, 1.0.1"
#r "nuget: Polars.NET.Native.win-x64, 1.0.1"
#r "nuget: Polars.NET.Native.osx-arm64, 1.0.1"

module TpchData = 
    open System
    open Polars.FSharp
    open Polars.FSharp.Query
    open System.IO
    
    // =========================================================================
    // TPC-H Schema Records
    // =========================================================================

    type Customer =
        {
            c_custkey: int64
            c_name: string
            c_address: string
            c_nationkey: int
            c_phone: string
            c_acctbal: decimal
            c_mktsegment: string
            c_comment: string
        }

    type LineItem =
        {
            l_orderkey: int64
            l_partkey: int64
            l_suppkey: int64
            l_linenumber: int64
            l_quantity: decimal
            l_extendedprice: decimal
            l_discount: decimal
            l_tax: decimal
            l_returnflag: string
            l_linestatus: string
            l_shipdate: DateOnly
            l_commitdate: DateOnly
            l_receiptdate: DateOnly
            l_shipinstruct: string
            l_shipmode: string
            l_comment: string
        }

    type Nation =
        {
            n_nationkey: int
            n_name: string
            n_regionkey: int
            n_comment: string
        }

    type Orders =
        {
            o_orderkey: int64
            o_custkey: int64
            o_orderstatus: string
            o_totalprice: decimal
            o_orderdate: DateOnly
            o_orderpriority: string
            o_clerk: string
            o_shippriority: int
            o_comment: string
        }

    type Part =
        {
            p_partkey: int64
            p_name: string
            p_mfgr: string
            p_brand: string
            p_type: string
            p_size: int
            p_container: string
            p_retailprice: decimal
            p_comment: string
        }

    type PartSupp =
        {
            ps_partkey: int64
            ps_suppkey: int64
            ps_availqty: int64
            ps_supplycost: decimal
            ps_comment: string
        }

    type Region =
        {
            r_regionkey: int
            r_name: string
            r_comment: string
        }

    type Supplier =
        {
            s_suppkey: int64
            s_name: string
            s_address: string
            s_nationkey: int
            s_phone: string
            s_acctbal: decimal
            s_comment: string
        }

    let DataDir = Path.Combine(__SOURCE_DIRECTORY__, "../Data")

    let CustomerPath = Path.Combine(DataDir, "customer.parquet")
    let LineItemPath = Path.Combine(DataDir, "lineitem.parquet")
    let NationPath = Path.Combine(DataDir, "nation.parquet")
    let OrdersPath = Path.Combine(DataDir, "orders.parquet")
    let PartPath = Path.Combine(DataDir, "part.parquet")
    let PartSuppPath = Path.Combine(DataDir, "partsupp.parquet")
    let RegionPath = Path.Combine(DataDir, "region.parquet")
    let SupplierPath = Path.Combine(DataDir, "supplier.parquet")

    let Customer = LazyFrame.ScanParquet(CustomerPath).AsQueryable<Customer>()
    let LineItem = LazyFrame.ScanParquet(LineItemPath).AsQueryable<LineItem>()
    let Nation = LazyFrame.ScanParquet(NationPath).AsQueryable<Nation>()
    let Orders = LazyFrame.ScanParquet(OrdersPath).AsQueryable<Orders>()
    let Part = LazyFrame.ScanParquet(PartPath).AsQueryable<Part>()
    let PartSupp = LazyFrame.ScanParquet(PartSuppPath).AsQueryable<PartSupp>()
    let Region = LazyFrame.ScanParquet(RegionPath).AsQueryable<Region>()
    let Supplier = LazyFrame.ScanParquet(SupplierPath).AsQueryable<Supplier>()