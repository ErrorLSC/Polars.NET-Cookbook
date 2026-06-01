#time "on" // Enable timer
#r "nuget: Polars.FSharp, 0.5.0"
#r "nuget: Polars.NET.Core, 0.5.0"
#r "nuget: Polars.NET.Native.linux-x64, 0.5.0"
#r "nuget: FSharp.Data"
#r "nuget: Microsoft.ML"
#r "nuget: Microsoft.ML.FastTree"
#r "nuget: Polars.NET.ML, 0.5.0"

open FSharp.Data
open Polars.FSharp
open Polars.NET.ML.DataView
open Polars.NET.ML.FSharpExtensions
open Microsoft.ML
open Microsoft.ML.Data

[<Literal>]
let trainPath = "train.csv"

[<Literal>]
let testPath = "test.csv"

type train = CsvProvider<trainPath>

let dfTrain = DataFrame.ReadCsv trainPath

let schema = Unchecked.defaultof<train.Row>
pl.setEnvVar "POLARS_FMT_MAX_COLS" "15"
pl.setEnvVar "POLARS_FMT_MAX_ROWS" "10"

let whiteList = ["Mr";"Mrs";"Master";"Miss"]

let addBaseFeature(df:DataFrame) = 
    df
    |> pl.withColumn ((pl.col (nameof schema.Name)).Str.Extract(",\s+(?:[A-Za-z]+\s+)*([A-Za-z]+\.)").Str.StripSuffix "."
            |> pl.alias "Prefix")

    |> pl.withColumns([
        pl.col (nameof schema.SibSp) + pl.col (nameof schema.Parch) + pl.lit 1 
            |> pl.alias "FamilySize"

        pl.col(nameof schema.Embarked).FillNull(pl.lit "S")

        pl.when' (pl.col("Prefix").IsIn(pl.lit(whiteList).Implode())) 
            |> pl.then'(pl.col "Prefix") 
            |> pl.otherwise(pl.lit "Rare") 
        
        pl.col(nameof schema.Cabin).Str.Extract("^([A-Za-z]+)").FillNull(pl.lit "Unknown") 
            |> pl.alias "Deck"

        pl.col(nameof schema.Fare).FillNull(pl.lit 0).Log1p() 
            |> pl.alias "LogFare"

        pl.when' (pl.col (nameof schema.Sex) .== pl.lit "female" .&& (pl.col (nameof schema.Age) .> pl.lit 18) .&& (pl.col (nameof schema.Parch).> pl.lit 0))
            |> pl.then'(pl.lit 1)
            |> pl.otherwise(pl.lit 0)
            |> pl.alias "IsMother"

        pl.col(nameof schema.Ticket)
            .Str.Extract("^([A-Za-z./]+[0-9]*)")
            .FillNull(pl.lit "NumOnly")
            |> pl.alias "TicketPrefix"
        ])
    |> _.Drop(nameof schema.Name,nameof schema.SibSp,nameof schema.Parch,nameof schema.Cabin,nameof schema.Fare)

let calGroupPrefix(df:DataFrame) = 
    df
    |> pl.groupBy [pl.col "Prefix";pl.col(nameof schema.Sex)]
    |> pl.agg [
        [nameof schema.Age] |> pl.median |> pl.alias "AgeMedian"]
    |> pl.sortAscending [pl.col "Prefix";pl.col (nameof schema.Sex)]

let calTicketGroupSize(df:DataFrame) =
    df
    |> pl.groupBy [pl.col(nameof schema.Ticket)]
    |> pl.agg [ pl.len() |> pl.alias "TicketGroupSize" ]

let addExtraFeature(groupPrefix:DataFrame) (ticketGroupSize:DataFrame) (df:DataFrame) = 
    df
    |> pl.joinOn groupPrefix [pl.col "Prefix";pl.col (nameof schema.Sex)] JoinType.Left
    |> pl.joinOn ticketGroupSize [pl.col (nameof schema.Ticket)] JoinType.Left
    |> pl.withColumn(pl.col(nameof schema.Age).Coalesce [pl.col "AgeMedian"])
    |> pl.withColumn(pl.col(nameof schema.Age).Cut [12;19;39;59]
        |> _.ToPhysical() 
        |> pl.alias "AgeBucket")
    |> pl.withColumn(pl.col "FamilySize" .== pl.lit 1L |> pl.castWithNetType<int> 
        |> pl.alias "IsAlone")
    |> _.Drop("AgeMedian",nameof schema.Ticket,nameof schema.Age)
    |> pl.withColumn(pl.cs.numeric().ToExpr() |> pl.castWithNetType<single>)
    

let dfTrainBase = dfTrain |> addBaseFeature
let trainGroupPrefix = dfTrainBase |> calGroupPrefix
let trainTicketGroupSize = dfTrainBase |> calTicketGroupSize
let dfTrainInter = dfTrainBase |> addExtraFeature trainGroupPrefix trainTicketGroupSize


let trainFinalize(df:DataFrame) = 
    df 
    |> pl.withColumns([
        pl.col "Survived" |> pl.castWithNetType<bool> |> pl.alias "Label"]
    )
    |> _.Drop("Survived",nameof schema.PassengerId)

let dfTrainFinal = dfTrainInter |> trainFinalize

let mlContext = MLContext(seed = 42)
let fullData = dfTrainFinal.AsDataView()

let splits = mlContext.Data.TrainTestSplit(fullData, testFraction = 0.2)

let allColumns = dfTrainFinal.Columns

let categoricalCols = [| nameof schema.Sex; nameof schema.Embarked; "Prefix"; "Deck"; "TicketPrefix" |]
let encodedCols   = categoricalCols |> Array.map (fun c -> c + "_Encoded")

let numericCols = 
    allColumns 
    |> Array.filter (fun c -> c <> "Label" && not (Array.contains c categoricalCols))

let allFeatures = Array.append numericCols encodedCols
let inline append estimator (chain: EstimatorChain<#ITransformer>) =
        chain.Append estimator
let ohePairs = 
    categoricalCols 
    |> Array.zip encodedCols 
    |> Array.map (fun (enc, raw) -> InputOutputColumnPair(enc, raw))

let pipeline =
    EstimatorChain<ITransformer>()
    |> append (mlContext.Transforms.Categorical.OneHotEncoding ohePairs)
    |> append (mlContext.Transforms.Concatenate("Features", allFeatures))
    |> append (mlContext.BinaryClassification.Trainers.FastTree())

let model = pipeline.Fit splits.TrainSet

let predictions = model.Transform splits.TestSet
let metrics = mlContext.BinaryClassification.Evaluate(predictions, labelColumnName = "Label")

printfn "=== Training Results ==="
printfn "Accuracy:  %.2f%%" (metrics.Accuracy * 100.0)
printfn "AUC: %.4f" metrics.AreaUnderRocCurve
printfn "F1 Score:         %.4f" metrics.F1Score

let testPredictions = 
    DataFrame.ReadCsv testPath 
    |> addBaseFeature 
    |> addExtraFeature trainGroupPrefix trainTicketGroupSize
    |> _.AsDataView()
    |> model.Transform

// testPredictions.Schema |> Seq.iter (fun col -> printfn $"{col.Name} : {col.Type}")
let keepCols = [| nameof schema.PassengerId; "PredictedLabel"|]
mlContext.Transforms.SelectColumns(keepCols)
    .Fit(testPredictions)
    .Transform(testPredictions)
    .ToDataFrame() 
|> pl.select [pl.cs.all().ToExpr() |> pl.castWithNetType<int>]
|> pl.select [
    pl.col keepCols.[0]
    pl.col keepCols.[1] |> pl.alias (nameof schema.Survived)]
|> _.WriteCsv("submission.csv",quoteStyle=QuoteStyle.Never)
#time "off"