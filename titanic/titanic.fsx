#time "on" // Enable timer
#r "nuget: Polars.FSharp, 0.6.0"
#r "nuget: Polars.NET.Native.linux-x64, 0.6.0"
#r "nuget: Polars.NET.Native.win-x64, 0.6.0"
#r "nuget: Polars.NET.Native.osx-arm64, 0.6.0"
#r "nuget: FSharp.Data"
#r "nuget: Microsoft.ML"
#r "nuget: Microsoft.ML.FastTree"
#r "nuget: Polars.NET.ML, 0.6.0"

open FSharp.Data
open Polars.FSharp
open Polars.NET.ML.DataView
open Polars.NET.ML.FSharpExtensions
open Microsoft.ML
open Microsoft.ML.Data

// Define file paths for the Kaggle Titanic dataset
[<Literal>]
let trainPath = "train.csv"

[<Literal>]
let testPath = "test.csv"

// Use FSharp.Data CsvProvider extract schema names
type train = CsvProvider<trainPath>

let schema = Unchecked.defaultof<train.Row>

// List of standard name prefixes to keep; less frequent ones will be categorized as "Rare"
let whiteList = ["Mr";"Mrs";"Master";"Miss"]
let dropList = [nameof schema.Name;nameof schema.SibSp;
            nameof schema.Parch;nameof schema.Cabin;
            nameof schema.Fare]
/// Step 1: Base Feature Engineering
/// Extracts name prefixes, handles missing values, and derives initial structural features.
let addBaseFeature(df:DataFrame) = 
    df
    // Extract title (e.g., "Mr.", "Miss.") from the Name column
    |> pl.withColumn ((pl.col (nameof schema.Name)).Str.Extract(",\s+(?:[A-Za-z]+\s+)*([A-Za-z]+\.)").Str.StripSuffix "."
            |> pl.alias "Prefix")
    
    |> pl.withColumns([
        // Combine sibling/spouse and parent/child counts into a single FamilySize metric
        pl.col (nameof schema.SibSp) + pl.col (nameof schema.Parch) + pl.lit 1 
            |> pl.alias "FamilySize"

        // Fill missing Embarked ports with the most common port 'S'
        pl.col(nameof schema.Embarked).FillNull(pl.lit "S")

        // Group rare titles into a single "Rare" category to reduce cardinality
        pl.when' (pl.col("Prefix").IsIn(pl.lit(whiteList).Implode())) 
            |> pl.then'(pl.col "Prefix") 
            |> pl.otherwise(pl.lit "Rare") 

        // Extract the deck letter from the Cabin string (e.g., "C123" -> "C")
        pl.col(nameof schema.Cabin).Str.Extract("^([A-Za-z]+)").FillNull(pl.lit "Unknown") 
            |> pl.alias "Deck"

        // Log-transform Fare to normalize its highly skewed distribution
        pl.col(nameof schema.Fare).FillNull(pl.lit 0).Log1p() 
            |> pl.alias "LogFare"

        // Create a specific domain feature: IsMother (Female, Adult, with children)
        pl.when' (pl.col (nameof schema.Sex) .== pl.lit "female" 
            .&& (pl.col (nameof schema.Age) .> pl.lit 18) 
            .&& (pl.col (nameof schema.Parch).> pl.lit 0))
            |> pl.then'(pl.lit 1)
            |> pl.otherwise(pl.lit 0)
            |> pl.alias "IsMother"

        // Separate alphabetical ticket prefixes from pure numbers
        pl.col(nameof schema.Ticket)
            .Str.Extract("^([A-Za-z./]+[0-9]*)")
            .FillNull(pl.lit "NumOnly")
            |> pl.alias "TicketPrefix"
        ])
    // Drop redundant source columns
    |> pl.drop dropList

/// Step 2: Aggregation - Calculate Median Age per Title/Sex group for target imputation
let calGroupPrefix(df:DataFrame) = 
    df
    |> pl.groupBy [pl.col "Prefix";pl.col(nameof schema.Sex)]
    |> pl.agg [
        [nameof schema.Age] |> pl.median |> pl.alias "AgeMedian"]
    |> pl.sortAscending [pl.col "Prefix";pl.col (nameof schema.Sex)]

/// Step 3: Aggregation - Calculate Group Size based on shared Ticket numbers
let calTicketGroupSize(df:DataFrame) =
    df
    |> pl.groupBy [pl.col(nameof schema.Ticket)]
    |> pl.agg [ pl.len() |> pl.alias "TicketGroupSize" ]

/// Step 4: Advanced Feature Engineering & Imputation
/// Joins aggregate metrics back to the main DataFrame, bucketizes age, and casts numeric cols to single type
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
    |> pl.drop ["AgeMedian";nameof schema.Ticket;nameof schema.Age]
    |> pl.withColumn(pl.cs.numeric().ToExpr() |> pl.castWithNetType<single>)

/// Step 5: Finalize Training Data
/// Formats the target label column as Boolean as expected by ML.NET Binary Classification
let trainFinalize(df:DataFrame) = 
    df 
    |> pl.withColumns([
        pl.col "Survived" |> pl.castWithNetType<bool> |> pl.alias "Label"]
    )
    |> pl.drop ["Survived";nameof schema.PassengerId]

// Execute Pipeline: Training Data Preparation
let dfTrainBase = DataFrame.ReadCsv trainPath |> addBaseFeature
// Configure Polars formatting options for console output
Config.withConfig [Config.tableCols (NumSet.Set 20);Config.tableRows (NumSet.Set 10)] 
    (
        fun ()-> dfTrainBase |> pl.show
        )
let trainGroupPrefix = dfTrainBase |> calGroupPrefix
let trainTicketGroupSize = dfTrainBase |> calTicketGroupSize
let dfTrainFinal = dfTrainBase |> addExtraFeature trainGroupPrefix trainTicketGroupSize |> trainFinalize

// --- ML.NET Machine Learning Pipeline ---
let mlContext = MLContext(seed = 42)

// Convert Polars DataFrame into ML.NET IDataView
let fullData = dfTrainFinal.AsDataView()

// Split data into 80% Train and 20% Validation sets
let splits = mlContext.Data.TrainTestSplit(fullData, testFraction = 0.2)

// Define categorical columns that require encoding
let categoricalCols = [| nameof schema.Sex; nameof schema.Embarked; "Prefix"; "Deck"; "TicketPrefix" |]
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

// Helper function to avoid explict interface conversion
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
printfn "AUC: %.4f" metrics.AreaUnderRocCurve
printfn "F1 Score:         %.4f" metrics.F1Score

// --- Inference Pipeline & Submission Generation ---
let testPredictions = 
    DataFrame.ReadCsv testPath 
    |> addBaseFeature 
    |> addExtraFeature trainGroupPrefix trainTicketGroupSize
    |> _.AsDataView()
    |> model.Transform

// ML.NET will generate duplicated column names in some cases, we can check and decide which columns should be exported
// testPredictions.Schema |> Seq.iter (fun col -> printfn $"{col.Name} : {col.Type}")
let keepCols = [| nameof schema.PassengerId; "PredictedLabel"|]
let exportCols = [| nameof schema.PassengerId; nameof schema.Survived|]
// Extract predictions, transform columns back to Polars, and format for Kaggle submission
mlContext.Transforms.SelectColumns(keepCols)
    .Fit(testPredictions)
    .Transform(testPredictions)
    .ToDataFrame() 
// Map over seq<Series>, casting to int and renaming according to Kaggle's schema 
|> Seq.mapi (fun i s -> s.Cast<int>().Rename(exportCols.[i]))
|> pl.dataframe
|> _.WriteCsv("submission.csv",quoteStyle=QuoteStyle.Never)
#time "off"

// === Training Results ===
// Accuracy:  77.71%
// AUC: 0.8324
// F1 Score:         0.7176
// Real: 00:00:01.074, CPU: 00:00:02.401, GC gen0: 3, gen1: 3, gen2: 3