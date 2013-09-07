﻿namespace FSharp.DataFrame

// --------------------------------------------------------------------------------------
// Indexing - index provides access to data in vector via keys. Optionally, the keys
// can be sorted. The index provides various operations for joining indices (etc.) that
// produce a new index, together with transformations to be applied on the data vector.
// Index is an interface and so you can define your own. 
// --------------------------------------------------------------------------------------

type Lookup = 
  | Exact = 0
  | NearestGreater = 1
  | NearestSmaller = 2

type Aggregation<'K> =
  | WindowSize of int
  | ChunkSize of int
  | WindowWhile of ('K -> 'K -> bool)
  | ChunkWhile of ('K -> 'K -> bool)
  | GroupBy of ('K -> System.IComparable)

namespace FSharp.DataFrame.Indices

open FSharp.DataFrame
open FSharp.DataFrame.Common
open FSharp.DataFrame.Addressing
open FSharp.DataFrame.Vectors

/// An interface that represents index mapping keys of type 'T to locations
/// of address Address.
type IIndex<'K when 'K : equality> = 
  abstract Keys : seq<'K>
  abstract Lookup : 'K * Lookup -> OptionalValue<Address>  
  abstract Mappings : seq<'K * Address>
  abstract Range : Address * Address
  abstract Ordered : bool
  abstract Comparer : System.Collections.Generic.Comparer<'K>
  
/// A builder represents various ways of constructing index
type IIndexBuilder =
  abstract Create : seq<'K> * Option<bool> -> IIndex<'K>
    
  abstract GetRange : 
    IIndex<'K> * option<'K> * option<'K> * VectorConstruction ->
    IIndex<'K> * VectorConstruction 

  abstract Union : 
    IIndex<'K> * IIndex<'K> * VectorConstruction * VectorConstruction -> 
    IIndex<'K> * VectorConstruction * VectorConstruction

  abstract Intersect :
    IIndex<'K> * IIndex<'K> * VectorConstruction * VectorConstruction -> 
    IIndex<'K> * VectorConstruction * VectorConstruction

  abstract Append :
    IIndex<'K> * IIndex<'K> * VectorConstruction * VectorConstruction * IVectorValueTransform -> 
    IIndex<'K> * VectorConstruction

  abstract Reindex :
    IIndex<'K> * IIndex<'K> * Lookup * VectorConstruction -> VectorConstruction

  abstract WithIndex :
    IIndex<'K> * (Address -> OptionalValue<'TNewKey>) * VectorConstruction -> 
    IIndex<'TNewKey> * VectorConstruction

  abstract DropItem : IIndex<'K> * 'K * VectorConstruction -> 
    IIndex<'K> * VectorConstruction 

  abstract OrderIndex : IIndex<'K> * VectorConstruction ->
    IIndex<'K> * VectorConstruction

  abstract Aggregate : IIndex<'K> * Aggregation<'K> * VectorConstruction *
    (IIndex<'K> * VectorConstruction -> OptionalValue<'R>) *
    (IIndex<'K> * VectorConstruction -> 'K) -> IIndex<'K> * IVector<'R> // Returning vector might be too concrete?
