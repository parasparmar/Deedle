﻿namespace Deedle.Math

open System
open Deedle
open MathNet.Numerics.LinearAlgebra
open MathNet.Numerics.Statistics

/// Correlation method (Pearson or Spearman)
///
/// [category:Statistical Analysis]
type CorrelationMethod =
  /// Pearson correlation
  | Pearson = 0
  /// Spearman correlation
  | Spearman = 1

/// Statistical analysis using MathNet.Numerics
///
/// [category:Statistical Analysis]
type Stats =

  /// Exponentially Weighted Standard Deviation
  ///
  /// [category: Exponentially Weighted]
  static member emaStdDev alpha (x:Series<'K, float>) =
    let x = x |> Series.dropMissing
    if x.KeyCount < 2 then
      x |> Series.mapValues(fun _ -> nan)
    else
      let init = x |> Stats.stdDev
      let data = x.Values |> Array.ofSeq
      let res = Array.zeroCreate x.KeyCount
      for i in [|0..x.KeyCount-1|] do
        if i = 0 then
          res.[i] <- init
        else
          res.[i] <- (
              alpha * res.[i-1] * res.[i-1] + 
              (1. - alpha) * data.[i] * data.[i] )
              |> Math.Sqrt
      Series(x.Keys, res)

  /// Exponentially Weighted Covariance Matrix
  ///
  /// [category: Exponentially Weighted]
  static member emaCovMatrix alpha (df:Frame<'R, 'C>): Series<'R, Matrix<float>> =
    let nCol = df.ColumnCount
    let matrix = df |> Frame.toMatrix
    let res = Array.init df.RowCount (fun _ -> Matrix<float>.Build.Dense(nCol, nCol))
    for i in [|0..df.RowCount-1|] do
      if i = 0 then
        res.[i] <- Stats.covMatrix df
      else
        res.[i] <-
          let vector = matrix.Row(i)
          let inc = vector.ToColumnMatrix().Multiply(vector.ToRowMatrix())
          inc * (1. - alpha) + alpha * res.[i-1]
    Series(df.RowKeys, res)
  
  /// Convert covariance matrix to correlation matrix
  ///
  /// [category: Correlation and Covariance]
  static member cov2corr (cov:Matrix<float>) =
    let stdDev = cov.Diagonal() |> Vector.map Math.Sqrt
    let dInv =
      stdDev
      |> DenseMatrix.ofDiag
      |> Matrix.inverse
    stdDev, dInv.Multiply(cov).Multiply(dInv)
  
  /// Convert standard deviation and correlation to covariance
  ///
  /// [category: Correlation and Covariance]
  static member corr2Cov(sigma:Vector<float>, cov:Matrix<float>) =
    let sigmaVector = sigma |> DenseMatrix.ofDiag
    sigmaVector * cov * sigmaVector

  /// Get correlation matrix
  ///
  /// [category: Correlation and Covariance]
  static member corrMatrix (df:Frame<'R, 'C>, ?method:CorrelationMethod): Matrix<float> =
    let method = defaultArg method CorrelationMethod.Pearson
    let arr =
      df
      |> Frame.toArray2D
      |> DenseMatrix.ofArray2
      |> fun x -> x.ToColumnArrays()
    match method with
    | CorrelationMethod.Pearson -> Correlation.PearsonMatrix arr
    | CorrelationMethod.Spearman -> Correlation.SpearmanMatrix arr
    | _ -> invalidArg "method" "Unknown correlation method"
  
  /// Get correlation frame
  ///
  /// [category: Correlation and Covariance]
  static member corr (df:Frame<'R, 'C>, ?method:CorrelationMethod): Frame<'C, 'C> =
    let method = defaultArg method CorrelationMethod.Pearson
    Stats.corrMatrix(df, method)
    |> Frame.ofMatrix df.ColumnKeys df.ColumnKeys

  /// Get covariance matrix
  ///
  /// [category: Correlation and Covariance]
  static member covMatrix (df:Frame<'R, 'C>): Matrix<float> =
    // Treat nan correlation as zero
    let corr = Stats.corrMatrix(df) |> Matrix.map(fun x -> if Double.IsNaN(x) then 0. else x)
    let stdev = df |> Stats.stdDev |> Series.values |> Array.ofSeq
    let stdevDiag = DenseMatrix.ofDiagArray stdev
    stdevDiag.Multiply(corr).Multiply(stdevDiag) 

  /// Get covariance frame
  ///
  /// [category: Correlation and Covariance]
  static member cov (df:Frame<'R, 'C>): Frame<'C, 'C> =
    df
    |> Stats.covMatrix
    |> Frame.ofMatrix df.ColumnKeys df.ColumnKeys

  /// Quantile
  ///
  /// [category: Descriptive Statistics]
  static member inline quantile (series:Series<'R, 'V>, tau:float, ?definition:QuantileDefinition): float =
    let definition = defaultArg definition QuantileDefinition.Excel
    series.Values
    |> Seq.map float
    |> Array.ofSeq
    |> fun x -> Statistics.QuantileCustom(x, tau, definition)
  
  /// Ranks of Series
  ///
  /// [category: Descriptive Statistics]
  static member inline ranks (series:Series<'R, 'V>): Series<'R, float> =
    series.Values
    |> Seq.map float
    |> Array.ofSeq
    |> fun x ->
      Seq.zip series.Keys (Statistics.Ranks(x))
      |> Series.ofObservations
  
  /// Median of Series
  ///
  /// [category: Descriptive Statistics]
  static member inline median (series:Series<'R, 'V>): float =
    series.Values
    |> Seq.map float
    |> Array.ofSeq
    |> Statistics.Median

  /// Median of Frame
  ///
  /// [category: Descriptive Statistics]
  static member median (df:Frame<'R, 'C>): Series<'C, float> =
    df
    |> Frame.getNumericCols
    |> Series.mapValues Stats.median

  /// Quantile of Frame
  ///
  /// [category: Descriptive Statistics]
  static member quantile (df:Frame<'R, 'C>, tau:float, ?definition:QuantileDefinition): Series<'C, float> =
    let definition = defaultArg definition QuantileDefinition.Excel
    df
    |> Frame.getNumericCols
    |> Series.mapValues(fun series -> Stats.quantile(series, tau, definition))

  /// Ranks of Frame
  ///
  /// [category: Descriptive Statistics]
  static member ranks (df:Frame<'R, 'C>): Frame<'R, 'C> =
    df
    |> Frame.getNumericCols
    |> Series.mapValues Stats.ranks
    |> Frame.ofColumns

  /// Moving standard deviation (parallel implementation)
  ///
  /// [category: Moving statistics]
  static member movingStdDevParallel window (df:Frame<'R, 'C>) =
    let rowKeys = df.RowKeys |> Array.ofSeq
    let len = rowKeys |> Array.length
    [|window..len|]
    |> Array.Parallel.map(fun i ->
      rowKeys.[i-1],
      df.Rows.[rowKeys.[i-window..i-1]] |> Stats.stdDev)   
    |> Frame.ofColumns
    |> LinearAlgebra.transpose
  
  /// Moving variance of frame (parallel implementation)
  ///
  /// [category: Moving statistics]
  static member movingVarianceParallel window (df:Frame<'R, 'C>) =
    let rowKeys = df.RowKeys |> Array.ofSeq
    let len = rowKeys |> Array.length
    [|window..len|]
    |> Array.Parallel.map(fun i ->
      rowKeys.[i-1],
      df.Rows.[rowKeys.[i-window..i-1]] |> Stats.variance)   
    |> Frame.ofColumns
    |> LinearAlgebra.transpose

  /// Moving covariance of frame (parallel implementation)
  ///
  /// [category: Moving statistics]
  static member movingCovarianceParallel window (df:Frame<'R, 'C>) =
    let rowKeys = df.RowKeys |> Array.ofSeq
    let len = rowKeys |> Array.length
    [|window..len|]
    |> Array.Parallel.map(fun i ->
      rowKeys.[i-1],
      df.Rows.[rowKeys.[i-window..i-1]]
      |> Stats.covMatrix )
    |> Series.ofObservations    