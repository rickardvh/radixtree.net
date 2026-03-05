namespace Majako.Collections.RadixTree.Tests.Utils

open FsCheck
open FsCheck.Xunit
open Majako.Collections.RadixTree

type private HashCodeObject(hashCode: int) =
    override _.GetHashCode() = hashCode
    override this.Equals(other) = obj.ReferenceEquals(this, other)

type StripedReaderWriterLockPropertyTests() =
    [<Property>]
    member _.``GetLock should not throw for negative hash codes``(hashCode: NegativeInt) =
        let sut = StripedReaderWriterLock(17)
        let o = HashCodeObject(hashCode.Get)

        try
            let lockRef = sut.GetLock(o)
            not (isNull lockRef)
        with _ ->
            false

    [<Property>]
    member _.``GetLock should throw ArgumentNullException for null object``(nLocks: PositiveInt) =
        let sut = StripedReaderWriterLock(nLocks.Get)

        try
            sut.GetLock(null) |> ignore
            false
        with
        | :? System.ArgumentNullException -> true
        | _ -> false
