#time "on" // Enable timer

#r "nuget: Polars.FSharp, 1.0.1"
#r "nuget: Polars.NET.Native.linux-x64, 1.0.1"
#r "nuget: Polars.NET.Native.win-x64, 1.0.1"
#r "nuget: Polars.NET.Native.osx-arm64, 1.0.1"
#r "nuget: FSharp.Data"
#r "nuget: Microsoft.ML"
#r "nuget: Microsoft.ML.FastTree"
#r "nuget: Polars.NET.ML, 1.0.1"

open FSharp.Data
open Polars.FSharp
open Polars.FSharp.Query
open System
open System.Linq
open Polars.NET.ML.DataView
open Polars.NET.ML.FSharpExtensions
open Microsoft.ML
open Microsoft.ML.Data

// Define file paths for the Kaggle Titanic dataset
[<Literal>]
let trainPath = "train.csv"

[<Literal>]
let testPath = "test.csv"

// Strongly typed record matching passenger schema
type PassengerRecord = {
    PassengerId: int64
    Survived: int64
    Pclass: int64
    Name: string
    Sex: string
    Age: float option
    SibSp: int64
    Parch: int64
    Ticket: string
    Fare: float option
    Cabin: string option
    Embarked: string option
}

let lfTrain = LazyFrame.ScanCsv trainPath
// Align test schema by adding a dummy Survived column (-1L)
let lfTest = LazyFrame.ScanCsv testPath |> LazyFrame.withColumn (pl.lit -1L |> pl.alias "Survived")

/// Step 1: Unified Base Feature Engineering via Polars LINQ Query (Shared by Train & Test)
let basePassengerQuery (lf: LazyFrame) = 
    query {
        for row in lf.AsQueryable<PassengerRecord>() do
        let famSize = row.SibSp + row.Parch + 1L
        
        // 1. Prefix (Title) Extraction
        let startIdx = row.Name.IndexOf(", ") + 2
        let endIdx = row.Name.IndexOf(".", startIdx)
        let rawTitle = 
            if startIdx > 1 && endIdx > startIdx then
                row.Name.Substring(startIdx, endIdx - startIdx)
            else
                "Rare"
        let prefix =
            if rawTitle = "Mr" || rawTitle = "Mrs" || rawTitle = "Miss" || rawTitle = "Master" then
                rawTitle
            else
                "Rare"
        
        // 2. Embarked default "S"
        let embarked = 
            match row.Embarked with
            | Some e when not (String.IsNullOrEmpty e) -> e
            | _ -> "S"
            
        // 3. Deck
        let deck = 
            match row.Cabin with
            | Some c when not (String.IsNullOrEmpty c) -> c.Substring(0, 1)
            | _ -> "Unknown"
            
        // 4. LogFare: log1p(Fare)
        let fare = match row.Fare with Some f -> f | None -> 0.0
        let logFare = Math.Log(fare + 1.0)
        
        // 5. IsMother: Female, Adult (> 18), with children (Parch > 0)
        let isMother = 
            let isAdult = match row.Age with Some a -> a > 18.0 | None -> false
            if row.Sex = "female" && isAdult && row.Parch > 0L then 1
            else 0
            
        // 6. TicketPrefix: alphabetic ticket prefixes or NumOnly
        let ticketPrefix =
            if Char.IsDigit(row.Ticket.[0]) then "NumOnly"
            else
                let spaceIdx = row.Ticket.LastIndexOf(' ')
                if spaceIdx > 0 then row.Ticket.Substring(0, spaceIdx)
                else row.Ticket

        // Type-safe projection (replaces manual dropList & dropMany)
        select {|
            PassengerId = row.PassengerId
            Survived = row.Survived
            Pclass = row.Pclass
            Sex = row.Sex
            Age = row.Age
            Ticket = row.Ticket
            Prefix = prefix
            FamilySize = famSize
            Embarked = embarked
            Deck = deck
            LogFare = logFare
            IsMother = isMother
            TicketPrefix = ticketPrefix
        |}
    }

/// Step 2: Aggregation - Calculate Group Size based on shared Ticket numbers via LINQ
let ticketGroupSizeQuery (lf: LazyFrame) =
    query {
        for row in lf.AsQueryable<PassengerRecord>() do
        groupBy row.Ticket into g
        select {|
            Ticket = g.Key
            TicketGroupSize = int64 (g.Count())
        |}
    }

/// Step 3: Aggregation - Calculate Median Age per Title/Sex group using native Polars aggregation
let calGroupPrefix (lf: LazyFrame) =
    lf
    |> LazyFrame.groupBy [pl.col "Prefix"; pl.col "Sex"]
    |> LazyFrame.agg [
        ["Age"] |> pl.median |> pl.alias "AgeMedian"
    ]
    |> LazyFrame.sortAscending [pl.col "Prefix"; pl.col "Sex"]

/// Step 4: Advanced Feature Engineering & Imputation
let addExtraFeature (groupPrefix: LazyFrame) (ticketGroupSize: LazyFrame) (lf: LazyFrame) =
    lf
    |> LazyFrame.joinOn groupPrefix [pl.col "Prefix"; pl.col "Sex"] JoinType.Left
    |> LazyFrame.joinOn ticketGroupSize [pl.col "Ticket"] JoinType.Left
    |> LazyFrame.withColumn (pl.col("Age").Coalesce [pl.col "AgeMedian"])
    |> LazyFrame.withColumn (
        pl.col("Age").Cut [12; 19; 39; 59]
        |> _.ToPhysical()
        |> pl.alias "AgeBucket"
    )
    |> LazyFrame.withColumn ((pl.col "FamilySize" .== pl.lit 1L).Cast<int>() |> pl.alias "IsAlone")
    |> LazyFrame.dropMany [pl.col "AgeMedian"; pl.col "Ticket"; pl.col "Age"]
    |> LazyFrame.withColumn (pl.cs.numeric().ToExpr().Cast<single>())

/// Step 5: Finalize Training Data
let trainFinalize (lf: LazyFrame) = 
    lf 
    |> LazyFrame.withColumns [
        pl.col("Survived").Cast<bool>().Alias "Label"
    ]
    |> LazyFrame.dropMany [pl.col "Survived"; pl.col "PassengerId"]

// === Execute Pipeline: Training Data Preparation ===
let baseTrainLf = basePassengerQuery lfTrain |> Query.toLazyFrame

// Display base training data sample
Config.withConfig [Config.tableCols (NumSet.Set 20); Config.tableRows (NumSet.Set 10)] (fun () ->
    baseTrainLf |> LazyFrame.collect |> DataFrame.show
)

let trainGroupPrefix = baseTrainLf |> calGroupPrefix
let trainTicketGroupSize = ticketGroupSizeQuery lfTrain |> Query.toLazyFrame

let dfTrainFinal = 
    baseTrainLf 
    |> addExtraFeature trainGroupPrefix trainTicketGroupSize 
    |> trainFinalize
    |> LazyFrame.collect

// --- ML.NET Machine Learning Pipeline ---
let mlContext = MLContext(seed = 42)

// Convert Polars DataFrame into ML.NET IDataView
let fullData = dfTrainFinal.AsDataView()

// Split data into 80% Train and 20% Validation sets
let splits = mlContext.Data.TrainTestSplit(fullData, testFraction = 0.2)

// Define categorical columns that require encoding
let categoricalCols = [| "Sex"; "Embarked"; "Prefix"; "Deck"; "TicketPrefix" |]
let encodedCols   = categoricalCols |> Array.map (fun c -> c + "_Encoded")

// Filter out features that are purely numeric
let numericCols = 
    dfTrainFinal.Columns 
    |> Array.filter (fun c -> c <> "Label" && not (Array.contains c categoricalCols))

// Combine numeric and newly encoded features for the trainer
let allFeatures = Array.append numericCols encodedCols

// Map original categorical columns to One-Hot Encoded column outputs
let ohePairs = 
    categoricalCols 
    |> Array.zip encodedCols 
    |> Array.map (fun (enc, raw) -> InputOutputColumnPair(enc, raw))

// Helper function to avoid explicit interface conversion
let inline append estimator (chain: EstimatorChain<#ITransformer>) =
    chain.Append estimator

// Build the ML.NET training pipeline
let pipeline =
    EstimatorChain<ITransformer>()
    |> append (mlContext.Transforms.Categorical.OneHotEncoding ohePairs)
    |> append (mlContext.Transforms.Concatenate("Features", allFeatures))
    |> append (mlContext.BinaryClassification.Trainers.FastTree())

// Train the model
let model = pipeline.Fit splits.TrainSet

// Evaluate performance on the validation split
let predictions = model.Transform splits.TestSet
let metrics = mlContext.BinaryClassification.Evaluate(predictions, labelColumnName = "Label")

// Print out out-of-sample performance validation metrics
printfn "=== Training Results ==="
printfn "Accuracy:  %.2f%%" (metrics.Accuracy * 100.0)
printfn "AUC:       %.4f" metrics.AreaUnderRocCurve
printfn "F1 Score:  %.4f" metrics.F1Score

// --- Inference Pipeline & Submission Generation ---
let testPredictions = 
    basePassengerQuery lfTest
    |> Query.toLazyFrame
    |> addExtraFeature trainGroupPrefix trainTicketGroupSize
    |> LazyFrame.collect
    |> _.AsDataView()
    |> model.Transform

let keepCols = [| "PassengerId"; "PredictedLabel" |]
let exportCols = [| "PassengerId"; "Survived" |]

mlContext.Transforms.SelectColumns(keepCols)
    .Fit(testPredictions)
    .Transform(testPredictions)
    .ToDataFrame() 
|> Seq.mapi (fun i s -> s.Cast<int>().Rename(exportCols.[i]))
|> pl.dataframe
|> DataFrame.sortAscending [pl.col "PassengerId"]
|> _.WriteCsv("submission_linq.csv", quoteStyle = QuoteStyle.Never)

printfn "Generated submission_linq.csv successfully!"
#time "off"
