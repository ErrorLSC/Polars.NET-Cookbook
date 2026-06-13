# Polars.NET Cookbooks

A repository for Polars.NET & Polars.FSharp practical recipes and real-world examples.

This repository is designed to be a living documentation site. Instead of reading theoretical API docs, you can explore fully reproducible .fsx (F# Scripts) and C# snippets that solve complex, production-grade data engineering and machine learning problems.

## Cookbook Menu

- Titanic ML Pipeline(F# with ML.NET)

Demonstrates how to build a complete, high-performance machine learning pipeline from scratch in 100 lines of F# code.

Data Wrangling: Advanced feature engineering using Polars.FSharp (Regex extraction, missing value imputation, Log1p normalization, conditional bucketization via pl.when').

ML.NET Interop: Converting a Polars DataFrame into an ML.NET IDataView via .AsDataView().

Model Training: Training a Binary Classification model using ML.NET's FastTree trainer.

Submission: Exporting predictions back to Polars and writing out a Kaggle-compliant submission.csv.

Performance : **Real: 00:00:01.074, CPU: 00:00:02.401, GC gen0: 3, gen1: 3, gen2: 3**

```bash

# Clone the repository
git clone https://github.com/ErrorLSC/Polars.NET-Cookbooks.git
cd Polars.NET-Cookbooks
cd titanic

# Download the Titanic dataset (train.csv and test.csv) into the directory, then run:
dotnet fsi titanic.fsx
```

- Olympic Historical EDA (F# Expression & Window Function)

Demonstrates how to conduct complex Exploratory Data Analysis (EDA) on a 270,000-row historical dataset without allocating heavy managed objects.

Data Wrangling: Showcases multi-column lazy group-by, conditional aggregation counters, and cross-row time-series tracking.

Advanced Features: Harnesses Polars' native Window Functions (`.Over()`), frame shifting (`.Shift()`), and distinct counting (`.NUnique()`) through a declarative F# pipeline.

Ecosystem Interop: Shows how to link relational tables using `pl.JoinOnLazy` and manage missing values dynamically by passing native tokens.

Performance: **Real: 00:00:00.260, CPU: 00:00:00.693, GC gen0: 0, gen1: 0, gen2: 0**

```bash
cd ..
cd olympic

# Ensure athlete_events.csv and noc_regions.csv are in the directory, then run:
dotnet fsi olympic.fsx
```
