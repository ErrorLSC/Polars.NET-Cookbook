# Polars.NET Cookbooks

Practical recipes and examples for Polars.NET and Polars.FSharp, provided as executable F# scripts (`.fsx`).

---

## Cookbooks

### 1. Titanic ML Pipeline (F# with ML.NET)

An end-to-end binary classification pipeline combining Polars data processing with ML.NET.

- **Dynamic Expression Version**: [`titanic/titanic.fsx`](titanic/titanic.fsx)  
  Uses standard Polars expression APIs (`DataFrame.withColumn`, `pl.when'`, regex extraction, median imputation, column selectors).
- **Strongly Typed Version**: [`titanic/titanic_strong_type.fsx`](titanic/titanic_strong_type.fsx)  
  Uses `Polars.FSharp.Query` and F# `query { ... }` computation expressions for compile-time schema validation, standard library string/pattern matching pushdown, and strongly-typed record projections, seamlessly transitioning to native Polars `LazyFrame` for group median imputation and numeric binning (`Cut`).

**Pipeline Overview**:
- **Feature Engineering**: Title extraction, missing value imputation, log-transformation, ticket group aggregation, and categorical encoding.
- **ML.NET Interop**: Conversion from Polars `DataFrame` to ML.NET `IDataView` via `.AsDataView()`.
- **Model Training & Evaluation**: FastTree binary classifier evaluated with accuracy, AUC, and F1 score.
- **Inference & Export**: Test set scoring and Kaggle-compliant `submission.csv` export.

**Execution**:
```bash
cd titanic
# Requires train.csv and test.csv in the directory
dotnet fsi titanic.fsx

# Or run the strongly-typed LINQ variant:
dotnet fsi titanic_strong_type.fsx
```

---

### 2. Olympic Historical EDA (Expressions & Window Functions)

Exploratory data analysis on historical Olympic data (~270,000 records).

- **Implementation**: [`olympic/olympic.fsx`](olympic/olympic.fsx)

**Techniques Demonstrated**:
- Multi-column lazy aggregations and conditional counters.
- Window functions (`.Over()`) and frame shifting (`.Shift()`) for edition-over-edition growth rate calculations.
- Relational joins (`JoinType.Left`) and distinct counting (`.NUnique()`).
- Comparison between Polars native expressions and `Polars.FSharp.Query` LINQ syntax.

**Execution**:
```bash
cd olympic
# Requires athlete_events.csv and noc_regions.csv in the directory
dotnet fsi olympic.fsx
```

---

### 3. TPC-H Benchmark Queries (Q1 – Q22 in F# LINQ)

Full implementation of all 22 TPC-H analytical queries written in F# using `Polars.FSharp.Query` LINQ syntax, running against bundled SF 0.01 Parquet data.

- **Query Scripts**: [`TPC-H/FSharp/`](TPC-H/FSharp/) (`TpchQ1.fsx` – `TpchQ22.fsx`)
- **Data Definition & Schemas**: [`TPC-H/FSharp/TpchData.fsx`](TPC-H/FSharp/TpchData.fsx)
- **Dataset**: [`TPC-H/Data/`](TPC-H/Data/) (SF 0.01, 8 tables in Parquet format)

**Key Features**:
- **Complete Coverage**: All 22 queries implemented via strongly-typed records and F# `query { ... }` computation expressions.
- **Engine Pushdown**: LINQ expressions compile directly to native Polars logical plans (predicate pushdown, projection pushdown, hash joins, aggregations).
- **Execution Plan Inspection**: Each script outputs both the query result and the compiled engine plan (`query.Explain()`).

**Execution**:
```bash
cd TPC-H/FSharp

# Run individual queries (e.g., Q1, Q6, Q22)
dotnet fsi TpchQ1.fsx
dotnet fsi TpchQ6.fsx
dotnet fsi TpchQ22.fsx
```
