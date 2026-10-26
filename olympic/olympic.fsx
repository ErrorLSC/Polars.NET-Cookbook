#time "on" // Enable timer
#r "nuget: Polars.FSharp, 1.0.1"
#r "nuget: Polars.NET.Native.linux-x64, 1.0.1"
#r "nuget: Polars.NET.Native.win-x64, 1.0.1"
#r "nuget: Polars.NET.Native.osx-arm64, 1.0.1"
#r "nuget: FSharp.Data"

open FSharp.Data
open Polars.FSharp
open Polars.FSharp.Query
open System
open System.Linq

[<Literal>]
let athletePath = "athlete_events.csv"

type AthleteRecord = {
    ID: int64
    Name: string
    Sex: string
    Age: int option
    Height: float option
    Weight: float option
    Team: string
    NOC: string
    Games: string
    Year: int
    Season: string
    City: string
    Sport: string
    Event: string
    Medal: string
}

type NOCRegion = {
    NOC: string
    region: string
    notes: string option
}

[<Literal>]
let regionsPath = "noc_regions.csv"

type Athlete = CsvProvider<athletePath>

type Region = CsvProvider<regionsPath>

let schema = Unchecked.defaultof<Athlete.Row>

// ==============================
// Female Athlete Ratio (Polars) 
// ==============================
let lf1 = LazyFrame.ScanCsv(athletePath,nullValues=["NA"]) 

let baseAgg =
    query {
        for row in lf1.AsQueryable<AthleteRecord>() do
        groupBy row.NOC into g
        select {|
            NOC = g.Key
            FemaleCount = g.Count(fun r -> r.Sex = "F")
            TotalCount = g.Count()
        |}
    }

let femaleRatioQuery =
    query {
        for r in baseAgg do
        let ratio = Math.Round(double r.FemaleCount / double r.TotalCount * 100.0, 2)
        sortByDescending ratio
        take 10
        select {|
            NOC = r.NOC
            FemaleCount = r.FemaleCount
            TotalCount = r.TotalCount
            FemaleRatio = ratio
        |}
    }

femaleRatioQuery |> Query.toDataFrame |> DataFrame.show


// ========================================
// Gold Medal Windowed GrowthRatio (Polars)
// ========================================
let lf2 = LazyFrame.ScanCsv(athletePath, nullValues=["NA"]) 

lf2
// Cleanup
|> _.DropNulls(nameof schema.Medal)
|> LazyFrame.filter (pl.col (nameof schema.Season) .== pl.lit "Summer")

// Aggregate medal numbers for each NOC 
|> LazyFrame.groupBy [ pl.col(nameof schema.NOC); pl.col(nameof schema.Year) ]
|> LazyFrame.agg [ pl.len().Alias "MedalCount" ]

// Sort by NOC and Year
|> LazyFrame.sortAscending [ pl.col(nameof schema.NOC); pl.col(nameof schema.Year) ]

// Window Function
|> LazyFrame.withColumn (
    pl.col("MedalCount").Shift(1).Over([pl.col(nameof schema.NOC)]).Alias "PrevMedalCount"
)

// Calc growth rate
|> LazyFrame.withColumn (
    let change = pl.col("MedalCount").Cast<float>() - pl.col "PrevMedalCount"
    let ratio = change / pl.col "PrevMedalCount" * pl.lit 100.0
    ratio.Round(2u).Alias "GrowthRatio(%)"
)

// Filter for USA
|> LazyFrame.filter (pl.col(nameof schema.NOC) .== pl.lit "USA")
|> LazyFrame.collect
|> DataFrame.show

// =====================================================
// Best Athlete with Medals in different Sports (Polars)
// =====================================================
let athleteLf = LazyFrame.ScanCsv(athletePath, nullValues=["NA"])
let regionLf = LazyFrame.ScanCsv(regionsPath,nullValues=["NA"])

let topAthletes =
    query {
        for a in athleteLf.AsQueryable<AthleteRecord>() do
        where (not (String.IsNullOrEmpty a.Medal))
        groupBy (a.Name, a.NOC) into g
        let sportsCount = g.Select(fun r -> r.Sport).Distinct().Count()
        where (sportsCount >= 3)
        select {|
            Name = fst g.Key
            NOC = snd g.Key
            UniqueSportsCount = sportsCount
        |}
    }
let athleteCrossSportStars =
    query {
        for a in topAthletes do
        leftOuterJoin reg in regionLf.AsQueryable<NOCRegion>() 
            on (a.NOC = reg.NOC) into joinedRegions
        for reg in joinedRegions.DefaultIfEmpty() do
        sortByDescending a.UniqueSportsCount
        select {|
            Name = a.Name
            Region = reg.region
            UniqueSportsCount = a.UniqueSportsCount
        |}
    }

athleteCrossSportStars |> Query.toDataFrame |> DataFrame.show


// ====================================================================
// Rolling Avg of USA Gold Medals  (Polars.FSharp)
// ====================================================================

let lf3 = LazyFrame.ScanCsv(athletePath, nullValues=["NA"])

lf3
    |> LazyFrame.filter (pl.col(nameof schema.NOC) .== pl.lit "USA")
    |> LazyFrame.filter (pl.col(nameof schema.Season) .== pl.lit "Summer")
    |> LazyFrame.filter (pl.col(nameof schema.Medal) .== pl.lit "Gold")

    |> LazyFrame.groupBy [ pl.col(nameof schema.Year) ]
    |> LazyFrame.agg [ pl.len().Alias "GoldCount" ]

    |> LazyFrame.sortAscending [ pl.col(nameof schema.Year) ]

    |> LazyFrame.withColumn (
        pl.col("GoldCount")
          .Cast<float>()
          .RollingMean(windowSize = Dur.String "3i", minPeriod = 3) 
          .Alias "Gold_3_Edition_MovingAvg"
    )
    |> LazyFrame.collect
    |> DataFrame.show

#time "off" 
