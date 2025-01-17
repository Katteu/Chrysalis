using Chrysalis.Cbor;

namespace Chrysalis.Cardano.Models.Plutus;

[CborSerializable(CborType.Constr, Index = 0)]
public record Some<T>([CborProperty(0)] T Value) : Option<T>;