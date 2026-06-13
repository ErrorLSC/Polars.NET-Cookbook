#time "on" // Enable timer
#r "nuget: Polars.FSharp, 0.6.0"
#r "nuget: Polars.NET.Native.linux-x64, 0.6.0"
#r "nuget: Polars.NET.Native.win-x64, 0.6.0"
#r "nuget: Polars.NET.Native.osx-arm64, 0.6.0"
#r "nuget: FSharp.Data"

open FSharp.Data
open Polars.FSharp

[<Literal>]
let athletePath = "athlete_events.csv"

[<Literal>]
let regionsPath = "noc_regions.csv"

type Athlete = CsvProvider<athletePath>

type Region = CsvProvider<regionsPath>

let schema = Unchecked.defaultof<Athlete.Row>

// ==============================
// Female Athlete Ratio (Polars) 
// ==============================
let lf1 = LazyFrame.ScanCsv(athletePath,nullValues=["NA"]) 

lf1 
// GroupBy NOC
|> pl.groupByLazy [ pl.col (nameof schema.NOC) ] 
// Aggregate Female 
|> pl.aggLazy [
    (pl.col (nameof schema.Sex) .== pl.lit "F").Sum().Alias "FemaleCount"
    
    pl.len().Alias "TotalCount"
]
|> pl.withColumnLazy (((pl.col "FemaleCount").Cast<float>() / pl.col "TotalCount" * pl.lit 100.0).Round(2u).Alias "FemaleRatio(%)")
|> pl.sortDescendingLazy [ pl.col "FemaleRatio(%)" ] 
|> pl.collect 
|> pl.show

// ========================================
// Gold Medal Windowed GrowthRatio (Polars)
// ========================================
let lf2 = LazyFrame.ScanCsv(athletePath, nullValues=["NA"]) 

lf2
// Cleanup
|> _.DropNulls(nameof schema.Medal)
|> pl.filterLazy(pl.col (nameof schema.Season) .== pl.lit "Summer")

// Aggregate medal numbers for each NOC 
|> pl.groupByLazy [ pl.col(nameof schema.NOC); pl.col(nameof schema.Year) ]
|> pl.aggLazy [ pl.len().Alias "MedalCount" ]

// Sort by NOC and Year
|> pl.sortAscendingLazy [ pl.col(nameof schema.NOC); pl.col(nameof schema.Year) ]

// Window Function
|> pl.withColumnLazy (
    pl.col("MedalCount").Shift(1).Over([pl.col(nameof schema.NOC)]).Alias "PrevMedalCount"
)

// Calc growth rate
|> pl.withColumnLazy (
    let change = pl.col("MedalCount").Cast<float>() - pl.col "PrevMedalCount"
    let ratio = change / pl.col "PrevMedalCount" * pl.lit 100.0
    ratio.Round(2u).Alias "GrowthRatio(%)"
)

// Filter for USA
|> pl.filterLazy (pl.col(nameof schema.NOC) .== pl.lit "USA")
|> pl.collect
|> pl.show

// =====================================================
// Best Athlete with Medals in different Sports (Polars)
// =====================================================
let athleteLf = LazyFrame.ScanCsv(athletePath, nullValues=["NA"])
let regionLf = LazyFrame.ScanCsv("noc_regions.csv",nullValues=["NA"])

athleteLf
|> _.DropNulls(nameof schema.Medal)
|> pl.groupByLazy [ pl.col(nameof schema.Name); pl.col(nameof schema.NOC) ]
|> pl.aggLazy [ pl.col(nameof schema.Sport).NUnique().Alias "UniqueSportsCount" ]
|> pl.filterLazy (pl.col "UniqueSportsCount" .>= pl.lit 3)
|> pl.joinOnLazy regionLf [ pl.col(nameof schema.NOC) ] JoinType.Left
|> pl.sortDescendingLazy [ pl.col "UniqueSportsCount" ]
|> pl.selectLazy [pl.col(nameof schema.Name);pl.col "region";pl.col "UniqueSportsCount" ]
|> pl.collect
|> pl.show

// ====================================================================
// Rolling Avg of USA Gold Medals  (Polars.FSharp)
// ====================================================================

let lf3 = LazyFrame.ScanCsv(athletePath, nullValues=["NA"])

lf3
    |> pl.filterLazy (pl.col(nameof schema.NOC) .== pl.lit "USA")
    |> pl.filterLazy (pl.col(nameof schema.Season) .== pl.lit "Summer")
    |> pl.filterLazy (pl.col(nameof schema.Medal) .== pl.lit "Gold")

    |> pl.groupByLazy [ pl.col(nameof schema.Year) ]
    |> pl.aggLazy [ pl.len().Alias "GoldCount" ]

    |> pl.sortAscendingLazy [ pl.col(nameof schema.Year) ]

    |> pl.withColumnLazy (
        pl.col("GoldCount")
          .Cast<float>()
          .RollingMean(windowSize = Dur.String "3i", minPeriod = 3) 
          .Alias "Gold_3_Edition_MovingAvg"
    )
    |> pl.collect
    |> pl.show

// ==============================
// Female Athlete Ratio (Native)
// ==============================
// let rows = Athlete.Load(athletePath).Rows

// rows
// |> Seq.groupBy (fun row -> row.NOC) 
// |> Seq.map (fun (noc, group) ->
//     let femaleCount = group |> Seq.filter (fun row -> row.Sex = "F") |> Seq.length
//     let totalCount = group |> Seq.length
//     let femaleRatio = float femaleCount / float totalCount * 100.0

//     {| NOC = noc
//        FemaleCount = femaleCount
//        TotalCount = totalCount
//        FemaleRatio = femaleRatio |}) 
// |> Seq.sortByDescending (fun result -> result.FemaleRatio) 
// |> Seq.take 5
// |> Seq.iter (fun r -> 
//     printfn "NOC: %s | Female: %d | Total: %d | Ratio: %.2f%%" r.NOC r.FemaleCount r.TotalCount r.FemaleRatio)

// =========================================
// Gold Medal Windowed GrowthRatio (Native)
// =========================================
// let rows = Athlete.Load(athletePath).Rows

// rows
// |> Seq.filter (fun row -> row.Medal <> "NA" && row.Season = "Summer")

// |> Seq.groupBy (fun row -> row.NOC, row.Year)
// |> Seq.map (fun ((noc, year), group) -> 
//     {| NOC = noc; Year = year; MedalCount = uint32 (Seq.length group) |})

// |> Seq.groupBy (fun r -> r.NOC)
// |> Seq.collect (fun (noc, mCountGroup) ->
//     let sortedByYear = mCountGroup |> Seq.sortBy (fun r -> r.Year) |> Seq.toArray
    
//     sortedByYear 
//     |> Seq.mapi (fun index current ->
//         let prevCount = 
//             if index = 0 then System.Nullable() 
//             else System.Nullable(sortedByYear.[index - 1].MedalCount)
            
//         let currentCountFloat = float current.MedalCount
        
//         let growthRatio = 
//             if prevCount.HasValue then 
//                 let prevFloat = float prevCount.Value
//                 System.Nullable((currentCountFloat - prevFloat) / prevFloat * 100.0)
//             else 
//                 System.Nullable() 

//         {| NOC = current.NOC
//            Year = current.Year
//            MedalCount = current.MedalCount
//            PrevMedalCount = prevCount
//            GrowthRatio = growthRatio |})
// )

// |> Seq.filter (fun r -> r.NOC = "USA")
// |> Seq.sortBy (fun r -> r.Year)
// |> Seq.iter (fun r ->
//     let prevStr = if r.PrevMedalCount.HasValue then string r.PrevMedalCount.Value else "null"
//     let ratioStr = if r.GrowthRatio.HasValue then sprintf "%.6f" r.GrowthRatio.Value else "null"
//     printfn "NOC: %s | Year: %d | Medal: %d | Prev: %s | Growth: %s" 
//         r.NOC r.Year r.MedalCount prevStr ratioStr)

// =====================================================
// Best Athlete with Medals in different Sports (Native)
// =====================================================
// let regionMap = 
//     Region.Load("noc_regions.csv").Rows
//     |> Seq.map (fun r -> r.NOC, r.Region)
//     |> Map.ofSeq

// let athleteRows = Athlete.Load(athletePath).Rows

// athleteRows
// // DropNulls:
// |> Seq.filter (fun row -> row.Medal <> "NA")

// // GroupByLazy:
// |> Seq.groupBy (fun row -> row.Name, row.NOC)

// // aggLazy [ NUnique ]:
// |> Seq.map (fun ((name, noc), group) ->
//     let uniqueSportsCount = 
//         group 
//         |> Seq.map (fun row -> row.Sport) 
//         |> Seq.distinct 
//         |> Seq.length
        
//     {| Name = name; NOC = noc; UniqueSportsCount = uint32 uniqueSportsCount |})

// // filterLazy:
// |> Seq.filter (fun r -> r.UniqueSportsCount >= 3u)

// // Join:
// |> Seq.map (fun leftRow ->
//     let regionName = 
//         match regionMap.TryFind leftRow.NOC with
//         | Some reg -> reg
//         | None -> null

//     {| Name = leftRow.Name
//        Region = regionName
//        UniqueSportsCount = leftRow.UniqueSportsCount |})

// // sortDescendingLazy:
// |> Seq.sortByDescending (fun r -> r.UniqueSportsCount)

// // selectLazy + show:
// |> Seq.iter (fun r -> 
//     printfn "Name: %s | Region: %s | UniqueSports: %d" r.Name r.Region r.UniqueSportsCount)

#time "off" 
